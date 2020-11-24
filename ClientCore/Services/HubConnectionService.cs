
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http.Connections;

namespace Streamster.ClientCore.Services
{
    public class HubConnectionService : IDisposable
    {
        private readonly ConnectionService _connectionService;
        private HubConnection _connection;
        private CancellationTokenSource _ctsClose = new CancellationTokenSource();
        private Action<bool> _onConnectionChanged;

        public HubConnectionService(ConnectionService connectionService)
        {
            _connectionService = connectionService;
        }

        public HubConnection Connection 
        { 
            get
            {
                return _connection; 
            }
        }

        public HubConnection CreateConnection(Action<bool> onConnectionChanged)
        {
            _onConnectionChanged = onConnectionChanged;
            _connection = new HubConnectionBuilder()
                    .WithUrl($"https://{_connectionService.ConnectionServer}/Hub",
                    options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult(_connectionService.AccessToken);
                        options.SkipNegotiation = true;
                        options.Transports = HttpTransportType.WebSockets;
                    })
                    .AddMessagePackProtocol()
                    .Build();
            _connection.Closed += OnConnectionClosed;
            return _connection;
        }


        public async Task ReleaseConnection()
        {
            if (_connection != null)
            {
                await _connection.StopAsync();
            }
        }

        private async Task OnConnectionClosed(Exception e) 
        {
            if (e != null)
            {
                while (!_ctsClose.IsCancellationRequested && _connection.State == HubConnectionState.Disconnected)
                {
                    try
                    {
                        _onConnectionChanged(false);
                        await Task.Delay(500);
                        await _connection.StartAsync(_ctsClose.Token);
                        _onConnectionChanged(true);
                    }
                    catch (Exception ee)
                    {
                        Log.Error(ee, "Reconnect failed");
                    }
                }
            }
        }

        public async Task StartConnection()
        {
            int q = 0;
            while (true)
            {
                try
                {
                    await _connection.StartAsync();
                    break;
                }
                catch (Exception e)
                {
                    if (q++ == 3)
                        throw new HubConnectionException("Unable to connect to cloud. Please try later.", e);
                    else
                    {
                        Log.Error(e, "Unable to connect to Hub. Will be retried");
                        await Task.Delay(2000);
                    }
                }
            }
        }

        public void Dispose()
        {
            _ctsClose.Cancel();
        }
    }

    public class HubConnectionException : Exception
    {
        public HubConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
