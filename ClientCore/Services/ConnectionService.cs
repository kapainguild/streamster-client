using ClientData;
using IdentityModel.Client;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Logging;
using Streamster.ClientData;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace Streamster.ClientCore.Services
{
    public class ConnectionService
    {
        private readonly IAppEnvironment _environment;
        private readonly IdService _idService;
        private readonly LogService _logService;
        private readonly IAppResources _appResources;
        private readonly string _domain;
        private NetworkCredential _credentials;
        private List<string> _previousServers = new List<string>();
        private int _changeServerRequestCounter = 0;

        public string AccessToken { get; private set; }

        public string ConnectionServer { get; private set; }

        public ClientClaims Claims { get; private set; }

        public string UserName { get; private set; }

        public event EventHandler ConnectionServerChanged;

        public ConnectionService(IAppEnvironment environment, IdService idService, LogService logService, IAppResources appResources)
        {
            _environment = environment;
            _idService = idService;
            _logService = logService;
            _appResources = appResources;
            _domain = appResources.AppData.Domain;
        }

        public async Task<LoadBalancerResponse> StartAsync(NetworkCredential credential)
        {
            _credentials = credential;
            await TaskHelper.GoToPool().ConfigureAwait(false);
            return await RunWithRetries(async (server, attempt) =>
            {
                AccessToken = await AuthenticateAsync(server, credential, (attempt + 2) * 2); // 4,6,8,...
                return await GetConnectionServerAsync(server);
            }, ClientConstants.LoadBalancerServers, ClientConstants.LoadBalancerServers.Length * 2, nameof(StartAsync));
        }

        private async Task<LoadBalancerResponse> GetConnectionServerAsync(string server, LoadBalancerRequest request = null)
        {
            request = request ?? new LoadBalancerRequest();
            var oldServer = ConnectionServer;
            var result = await SendGetConnectionServerAsync($"{server}:{ClientConstants.LoadBalancerServerPort}", "LoadBalancer/GetConnectionServer", request);
            ConnectionServer = result.Server;
            if (oldServer != result.Server)
                ConnectionServerChanged?.Invoke(this, EventArgs.Empty);

            lock (_previousServers)
            {
                if (!_previousServers.Contains(result.Server))
                    _previousServers.Add(result.Server);
            }
            return result;
        }

        public async Task PrepareChangeServer()
        {
            LoadBalancerRequest request;
            lock (_previousServers)
            {
                request = new LoadBalancerRequest
                {
                    RequestCounter = _changeServerRequestCounter++,
                    TryExclude = String.Join(",", _previousServers)
                };
            }

            var oldServer = ConnectionServer;
            var result = await RunWithRetries(async (server, attempt) =>
            {
                await TryRefreshAccessToken(server);
                return await GetConnectionServerAsync(server, request);
            }, ClientConstants.LoadBalancerServers, ClientConstants.LoadBalancerServers.Length * 2, nameof(PrepareChangeServer));

            if (oldServer == result.Server)
                throw new InvalidOperationException("Same server is selected, no change server possible");
        }

        public async Task<T> RunWithRetries<T>(Func<string, int, Task<T>> action, string[] serverList, int retriesCount, [CallerMemberName] string name = null)
        {
            for (int q = 0; q < retriesCount; q++)
            {
                bool last = q == retriesCount - 1;
                var server = serverList[q % serverList.Length];
                try
                {
                    return await action(server, q);
                }
                catch (Exception e) when (e is WrongUserNamePasswordException || e is OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    if (last)
                    {
                        if (e is ConnectionServiceException) throw;
                        throw new ConnectionServiceException($"Something went wrong. Please contact service administrator ({e.Message}).", e);
                    }
                    else
                        Log.Error(e, "Not last retry exception");
                }
            }
            throw new InvalidOperationException();
        }

        private async Task<string> AuthenticateAsync(string server, NetworkCredential credential, int timeout)
        {
            using (var client = GetHttpClient(false, timeout))
            {
                var host = $"https://{server}:{ClientConstants.AuthorizationServerPort}/connect/token";

                Log.Information($"Authenticating at {host}");


                var parameters = new Dictionary<string, string>()
                        {
                            { ClientConstants.DeviceIdClaim, _idService.GetDeviceId() },
                            { ClientConstants.VersionClaim, ClientVersionHelper.GetVersion() }
                        };

                if (_domain != null)
                    parameters.Add(ClientConstants.DomainClaim, _domain);

                TokenResponse tokenResponse = null;
                if (credential != null)
                {
                    UserName = credential.UserName;
                    tokenResponse = await client.RequestPasswordTokenAsync(new PasswordTokenRequest
                    {
                        Address = host,
                        ClientId = _environment.GetClientId(),
                        ClientSecret = "xtreamer.id",

                        UserName = _appResources.AppData.UserNamePrefix + credential.UserName,
                        Password = credential.Password,
                        Scope = ClientConstants.ConnectionServerApi,

                        Parameters = parameters
                    });
                }
                else
                {
                    UserName = null;
                    tokenResponse = await client.RequestTokenAsync(new TokenRequest
                    {
                        Address = host,
                        ClientId = _environment.GetClientId(),
                        ClientSecret = "xtreamer.id",
                        GrantType = ClientConstants.AnonymousGrandType,

                        Parameters = parameters
                    });
                }

                if (tokenResponse.IsError)
                {
                    if (tokenResponse.ErrorType == ResponseErrorType.Exception && 
                        (tokenResponse.Exception is HttpRequestException &&
                        tokenResponse.Exception.InnerException is SocketException ||
                        tokenResponse.Exception is TaskCanceledException))
                    {
                        throw new ConnectionServiceException("Connection to the service failed. Please check your internet connection.", tokenResponse.Exception);
                    }
                    else if (credential != null &&
                        tokenResponse.ErrorType == ResponseErrorType.Protocol &&
                        tokenResponse.HttpResponse?.StatusCode == HttpStatusCode.BadRequest &&
                        tokenResponse.ErrorDescription == "invalid_username_or_password")
                    {
                        throw new WrongUserNamePasswordException();
                    }
                    else 
                        throw new ConnectionServiceException($"Unknown error occured ({tokenResponse.Error}). Please contact service administrator.", tokenResponse.Exception);
                }

                var jwt = tokenResponse.AccessToken;
                var handler = new JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(jwt);

                Claims = new ClientClaims(token.Claims);

                if (Claims.IsDebug)
                    _logService.EnableDebug();

                return tokenResponse.AccessToken;
            }
        }

        public async Task<LoadBalancerResponse> SendGetConnectionServerAsync(string host, string path, LoadBalancerRequest input)
        {
            string query = "";
            if (input != null)
                query = $"?{nameof(LoadBalancerRequest.RequestCounter)}={input.RequestCounter}&" +
                         $"{nameof(LoadBalancerRequest.TryExclude)}={HttpUtility.UrlEncode(input.TryExclude)}";

            using (var client = GetHttpClient(true))
            using (var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}/{path}{query}"))
            {
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

                if (response.IsSuccessStatusCode)
                {
                    response.EnsureSuccessStatusCode();
                    var stream = await response.Content.ReadAsByteArrayAsync();
                    return JsonSerializer.Deserialize<LoadBalancerResponse>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                else if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    var stream = await response.Content.ReadAsByteArrayAsync();
                    var error = JsonSerializer.Deserialize<ServerError>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    throw new ConnectionServiceException(error.Message, null);
                }
                else
                    throw new ConnectionServiceException($"Connection to service failed ({response.StatusCode}). Please contact service administrator.", null);
            }
        }

        public HttpClient GetHttpClient(bool authenticated, int timeout = 0)
        {
            var handler = new HttpClientHandler();
            handler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
            var client =  new HttpClient(handler);
            if (authenticated)
                client.SetBearerToken(AccessToken);
            if (timeout > 0)
                client.Timeout = TimeSpan.FromSeconds(timeout);
            return client;
        }

        public async Task<bool> TryRefreshConnectionServer()
        {
            try
            {
                await RunWithRetries(async (server, attempt) =>
                {
                    await TryRefreshAccessToken(server);
                    return await GetConnectionServerAsync(server);
                }, ClientConstants.LoadBalancerServers, ClientConstants.LoadBalancerServers.Length, nameof(TryRefreshConnectionServer));
                return true;
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to refresh server");
                return false;
            }
        }

        private async Task TryRefreshAccessToken(string server)
        {
            try
            {
                AccessToken = await AuthenticateAsync(server, _credentials, 4);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to access authentication server, old AccessToken will be used");
            }
        }
    }

    public class WrongUserNamePasswordException : Exception
    {
    }

    public class ConnectionServiceException : Exception
    {
        public ConnectionServiceException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
