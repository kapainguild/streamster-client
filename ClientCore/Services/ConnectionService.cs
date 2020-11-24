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

namespace Streamster.ClientCore.Services
{
    public class ConnectionService
    {
        private readonly IAppEnvironment _environment;
        private readonly IdService _idService;
        private readonly LogService _logService;
        private readonly string _domain;

        public string AccessToken { get; private set; }

        public string ConnectionServer { get; private set; }

        public ClientClaims Claims { get; private set; }

        public string UserName { get; private set; }

        public ConnectionService(IAppEnvironment environment, IdService idService, LogService logService, IAppResources appResources)
        {
            _environment = environment;
            _idService = idService;
            _logService = logService;
            _domain = appResources.AppData.Domain;
        }

        public async Task<LoadBalancerResponse> StartAsync(NetworkCredential credential)
        {
            await TaskHelper.GoToPool().ConfigureAwait(false);
            return await RunWithRetries(async server =>
            {
                AccessToken = await AuthenticateAsync(server, credential);
                return await GetConnectionServerAsync(server);
            }, ClientConstants.LoadBalancerServers, 3, nameof(AuthenticateAsync));
        }

        private async Task<LoadBalancerResponse> GetConnectionServerAsync(string server)
        {
            var request = new LoadBalancerRequest();

            var result = await ExecuteAsync<LoadBalancerRequest, LoadBalancerResponse>($"{server}:{ClientConstants.LoadBalancerServerPort}", "LoadBalancer/GetConnectionServer", request);
            ConnectionServer = result.Server;
            return result;
        }

        public async Task<T> RunWithRetries<T>(Func<string, Task<T>> action, string[] serverList, int retriesCount, [CallerMemberName] string name = null)
        {
            for (int q = 0; q < retriesCount; q++)
            {
                bool last = q == retriesCount - 1;
                var server = serverList[q % serverList.Length];
                try
                {
                    return await action(server);
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

        private async Task<string> AuthenticateAsync(string server, NetworkCredential credential)
        {
            using (var client = GetHttpClient(false))
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

                        UserName = credential.UserName,
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
                        tokenResponse.Exception is HttpRequestException &&
                        tokenResponse.Exception.InnerException is SocketException)
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

        public async Task<TResult> ExecuteAsync<T, TResult>(string host, string path, T input)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(input);

            using (var client = GetHttpClient(true))
            using (var request = new HttpRequestMessage(HttpMethod.Get, $"https://{host}/{path}"))
            using (var data = new ByteArrayContent(bytes))
            {
                data.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Content = data;
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

                if (response.IsSuccessStatusCode)
                {
                    response.EnsureSuccessStatusCode();
                    if (typeof(TResult) == typeof(bool))
                    {
                        return default;
                    }
                    else
                    {
                        var stream = await response.Content.ReadAsByteArrayAsync();
                        return JsonSerializer.Deserialize<TResult>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
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

        public HttpClient GetHttpClient(bool authenticated)
        {
            var handler = new HttpClientHandler();
            handler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
            var client =  new HttpClient(handler);
            if (authenticated)
                client.SetBearerToken(AccessToken);
            return client;
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
