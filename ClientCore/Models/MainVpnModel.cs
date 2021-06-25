using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class MainVpnModel
    {
        private readonly CoreData _coreData;
        private readonly IVpnService _vpnService;
        private readonly ConnectionService _connectionService;

        private bool _vpnRequested = false;

        public bool IsEnabled { get; set; } 
        

        public MainVpnModel(CoreData coreData, IVpnService vpnService, ConnectionService connectionService)
        {
            _coreData = coreData;
            _vpnService = vpnService;
            _connectionService = connectionService;
        }

        public async Task StartAsync()
        {
            Log.Information($"Starting VPN service (Has={_connectionService.Claims.HasVpn}, Data={_coreData.ThisDevice.VpnData != null})");
            IsEnabled = _connectionService.Claims.HasVpn && _coreData.ThisDevice.VpnData != null;
            if (IsEnabled && _coreData.ThisDevice.DeviceSettings.VpnBehavior == VpnBehavior.AppStart)
            {
                _coreData.ThisDevice.VpnRequested = true;
                _vpnRequested = true;
                SwitchOn();

                try
                {
                    await _vpnService.ConnectAsync(_coreData.ThisDevice.VpnData, OnStateChanged);
                }
                catch { Log.Error("VPN at startup failure"); }
            }

            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.VpnRequested, (d, c, p) => OnOffVpn(d));
        }

        public async Task StopAsync()
        {
            if (_vpnRequested)
            {
                _vpnRequested = false;
                await _vpnService.DisconnectAsync();
            }
        }

        private void OnOffVpn(IDevice d)
        {
            if (d == _coreData.ThisDevice)
            {
                if (_vpnRequested != d.VpnRequested)
                {
                    _vpnRequested = d.VpnRequested;

                    if (_vpnRequested)
                    {
                        SwitchOn();
                        TaskHelper.RunUnawaited(() => _vpnService.ConnectAsync(_coreData.ThisDevice.VpnData, OnStateChanged), "ConnectAsync");
                    }
                    else
                    {
                        TaskHelper.RunUnawaited(() => _vpnService.DisconnectAsync(), "DisconnectAsync");
                    }
                }
            }
        }

        private void SwitchOn()
        {
            _coreData.ThisDevice.VpnState = VpnState.Connecting;
            _coreData.ThisDevice.KPIs.Vpn.State = IndicatorState.Warning;
        }

        private void OnStateChanged(VpnRuntimeState runtimeState)
        {
            VpnState state = VpnState.Connected;
            if (!runtimeState.Connected)
            {
                if (runtimeState.ErrorMessage == null)
                {
                    state = VpnState.Idle;
                }
                else
                    state = VpnState.Reconnecting;
            }

            if (_coreData.ThisDevice.VpnState != state)
                Log.Information($"Vpn state changing to {state} ({runtimeState.ErrorMessage})");

            _coreData.ThisDevice.VpnServerIpAddress = runtimeState.ServerIpAddress;
            _coreData.ThisDevice.VpnState = state;
            _coreData.ThisDevice.KPIs.Vpn.State = state switch
            {
                VpnState.Connecting => IndicatorState.Warning,
                VpnState.Reconnecting => IndicatorState.Error,
                VpnState.Connected => IndicatorState.Ok,
                _ => IndicatorState.Disabled,
            };

            _coreData.ThisDevice.KPIs.Vpn.Sent = runtimeState.SentKbs;
            _coreData.ThisDevice.KPIs.Vpn.Received = runtimeState.ReceivedKbs;
        }
    }
}
