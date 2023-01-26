
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http.Connections;
using Streamster.ClientData;

namespace Streamster.ClientCore.Services
{
    public record HubConnectionDesc(HubConnection HubConnection, string Url, IDisposable OnPatchSubscription, IDisposable OnReceiveChatSubscription);

    public class HubConnectionService : IDisposable
    {
        private readonly ConnectionService _connectionService;
        private CancellationTokenSource _ctsClose = new CancellationTokenSource();

        private Action<bool> _onConnectionChanged;
        private Func<ProtocolJsonPatchPayload, Task> _onJsonPatch;
        private Action<ReceiveChatMessagesData> _onReceiveChatMessages;
        private HubConnectionDesc _connection;
        private HubConnection _connectionInProgress;

        public HubConnectionService(ConnectionService connectionService)
        {
            _connectionService = connectionService;
        }

        private async Task OnConnectionClosed(Exception e) 
        {
            if (e != null)
            {
                while (!_ctsClose.IsCancellationRequested && _connection.HubConnection.State == HubConnectionState.Disconnected)
                {
                    try
                    {
                        _onConnectionChanged(false);
                        await Task.Delay(500);

                        if (await _connectionService.TryRefreshConnectionServer() &&
                            _connectionService.ConnectionServer != _connection.Url)
                        {
                            var oldConnection = _connection;
                            _connection = await StartAsync();
                            await DisposeConnection(oldConnection);
                        }
                        else 
                            await _connection.HubConnection.StartAsync(_ctsClose.Token);
                        _onConnectionChanged(true);
                    }
                    catch (Exception ee)
                    {
                        Log.Error(ee, "Reconnect failed");
                    }
                }
            }
        }

        private async Task<HubConnectionDesc> StartAsync()
        {
            var url = _connectionService.ConnectionServer;
            var connection = new HubConnectionBuilder()
                    .WithUrl($"https://{url}/Hub",
                    options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult(_connectionService.AccessToken);
                        options.SkipNegotiation = true;
                        options.Transports = HttpTransportType.WebSockets;
                    })
                    .AddMessagePackProtocol()
                    .Build();

            var onPatchSubscription = connection.On(nameof(IConnectionHubClient.JsonPatch), _onJsonPatch);
            var onReceiveChatSubscription = connection.On(nameof(IConnectionHubClient.ReceiveChatMessages), _onReceiveChatMessages);
            connection.Closed += OnConnectionClosed;
            var result = new HubConnectionDesc(connection, url, onPatchSubscription, onReceiveChatSubscription);
            Exception ex = null;
            try
            {
                _connectionInProgress = connection;
                await connection.StartAsync(_ctsClose.Token);
            }
            catch (Exception e)
            {
                ex = e;
            }
            finally
            {
                _connectionInProgress = null;
            }

            if (ex != null)
            {
                await DisposeConnection(result);
                throw new HubConnectionException("Unable to connect to cloud. Please try later.", ex);
            }
            else
                return result;

        }

        private async Task DisposeConnection(HubConnectionDesc result)
        {
            if (result != null)
            {
                result.HubConnection.Closed -= OnConnectionClosed;
                result.OnPatchSubscription.Dispose();
                result.OnReceiveChatSubscription.Dispose();

                await result.HubConnection.DisposeAsync();
            }
        }

        public async Task StopConnection()
        {
            await DisposeConnection(_connection);
            _connection = null;
        }

        public void Dispose()
        {
            _ctsClose.Cancel();
        }

        public async Task StartConnection(Action<bool> onConnectionChanged, Func<ProtocolJsonPatchPayload, Task> onJsonPatch, Action<ReceiveChatMessagesData> onReceiveChatMessages)
        {
            _onConnectionChanged = onConnectionChanged;
            _onJsonPatch = onJsonPatch;
            _onReceiveChatMessages = onReceiveChatMessages;

            _connection = await StartAsync();
        }

        public async Task<bool> InvokeAsync(string methodName, object arg1)
        {
            var con = _connection?.HubConnection ?? _connectionInProgress;
            var state = con?.State;
            if (state == HubConnectionState.Connected || state == HubConnectionState.Connecting)
            {
                await con.InvokeAsync(methodName, arg1);
                return true;
            }
            else
            {
                if (methodName != "Logs" || state != null)
                    Log.Warning($"Failed to Invoke '{methodName}' as connection state is '{state}'");
            }
            return false;
        }
    }

    public class HubConnectionException : Exception
    {
        public HubConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
