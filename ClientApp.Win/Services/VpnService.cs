using Serilog;
using Streamster.ClientApp.Win.Services.Vpn;
using Streamster.ClientCore.Cross;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Streamster.ClientApp.Win.Services
{
    class VpnService : IVpnService
    {
        private const string EntryName = "Streamster-VPN";

        private CancellationTokenSource _cts = new CancellationTokenSource();
        private TaskCompletionSource<bool> _disconnected;

        public Task ConnectAsync(VpnData vpnData, Action<VpnRuntimeState> onStateChanged)
        {
            Log.Information($"VPN request with {vpnData.User} to {vpnData.Url}");
            _cts = new CancellationTokenSource();
            _disconnected = new TaskCompletionSource<bool>();

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            TaskHelper.RunUnawaited(() => MaintainConnection(vpnData, _cts.Token, tcs, onStateChanged), "MaintainConnection");
            return tcs.Task;
        }

        public async Task DisconnectAsync()
        {
            Log.Information("VPN Disconnecting");
            _cts.Cancel();
            await _disconnected.Task;
            VpnApi.DeleteEntry(EntryName);
        }

        private async Task MaintainConnection(VpnData vpnData, CancellationToken cancellationToken, TaskCompletionSource<bool> firstTime, Action<VpnRuntimeState> onStateChanged)
        {
            Task awaitConnectionChanged = null;
            IntPtr handle = IntPtr.Zero;

            bool statInitialized = false;
            RAS_STATS prevStat = new RAS_STATS();

            string serverIpAddress = null;

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        if (handle == IntPtr.Zero)
                        {
                            Log.Information("VPN Connecting");
                            handle = await ReconnectAsync(vpnData, _cts.Token);

                            var info = VpnApi.GetProjectionInfoEx(handle);
                            serverIpAddress = $"{info.ipv4ServerAddress.addr[0]}.{info.ipv4ServerAddress.addr[1]}.{info.ipv4ServerAddress.addr[2]}.{info.ipv4ServerAddress.addr[3]}";


                            firstTime?.TrySetResult(true);
                            firstTime = null;
                            statInitialized = false;

                            var eventHandle = VpnApi.RegisterForTermination(handle);
                            awaitConnectionChanged = TaskHelper.WaitOneAsync(eventHandle, "VPN state changed");
                        }

                        if (handle != IntPtr.Zero)
                        {
                            var status = VpnApi.GetState(handle);
                            var stat = VpnApi.GetStatisitcs(handle);

                            if (status.connectionState != RasConnectionState.Connected)
                            {
                                if (status.errorCode == 0)
                                    throw new VpnException($"VPN in wrong '{status.connectionState}' state");
                                else
                                    throw new VpnException($"VPN error '{VpnApi.RasErrorMessage(status.errorCode)}'");
                            }

                            if (!statInitialized)
                            {
                                statInitialized = true;
                                prevStat = stat;
                            }

                            var delta = GetStatDelta(stat, prevStat);
                            prevStat = stat;

                            if (delta.dwTimeoutErr + delta.dwFramingErr + delta.dwCrcErr + delta.dwHardwareOverrunErr + delta.dwBufferOverrunErr + delta.dwAlignmentErr > 0)
                                Log.Warning($"VPN errors {delta.dwTimeoutErr},{delta.dwFramingErr},{delta.dwCrcErr},{delta.dwHardwareOverrunErr},{delta.dwBufferOverrunErr},{delta.dwAlignmentErr}");


                            onStateChanged(new VpnRuntimeState
                            {
                                Connected = true,
                                SentKbs = (int)delta.dwBytesXmited / (1024 / 8),
                                ReceivedKbs = (int)delta.dwBytesRcved / (1024 / 8),
                                ServerIpAddress = serverIpAddress
                            }); 
                        }
                    }
                    catch(Exception e) when(TaskHelper.IsCancellation(e))
                    {
                        Log.Error(e, "VPN cancellation");
                        throw;
                    }
                    catch(Exception e)
                    {
                        Log.Error(e, "VPN maintanance error");
                        firstTime?.TrySetException(e);
                        firstTime = null;
                        serverIpAddress = null;

                        onStateChanged(new VpnRuntimeState { Connected = false, ErrorMessage = e.Message });

                        await HangUpAsync(handle);
                        handle = IntPtr.Zero;
                        awaitConnectionChanged = null;
                    }

                    var delay = Task.Delay(1000, cancellationToken);
                    if (awaitConnectionChanged != null)
                    {
                        var result = await Task.WhenAny(delay, awaitConnectionChanged);
                        if (result == awaitConnectionChanged)
                        {
                            Log.Information("VPN short wait");
                            awaitConnectionChanged = null; //it is raised once and not usable anymore
                        }
                    }
                    else
                        await delay;
                }
            }
            finally
            {
                Log.Information("VPN exiting maintainance");
                await HangUpAsync(handle);
                onStateChanged(new VpnRuntimeState { Connected = false });
                _disconnected?.TrySetResult(true);
            }
        }

        private RAS_STATS GetStatDelta(RAS_STATS stat, RAS_STATS prevStat) => new RAS_STATS
        {
            dwBytesXmited = GetUintDelta(stat.dwBytesXmited, prevStat.dwBytesXmited),
            dwBytesRcved = GetUintDelta(stat.dwBytesRcved, prevStat.dwBytesRcved),
            dwBufferOverrunErr = GetUintDelta(stat.dwBufferOverrunErr, prevStat.dwBufferOverrunErr),
            dwCrcErr = GetUintDelta(stat.dwCrcErr, prevStat.dwCrcErr),
            dwFramingErr = GetUintDelta(stat.dwFramingErr, prevStat.dwFramingErr),
            dwHardwareOverrunErr = GetUintDelta(stat.dwHardwareOverrunErr, prevStat.dwHardwareOverrunErr),
            dwTimeoutErr = GetUintDelta(stat.dwTimeoutErr, prevStat.dwTimeoutErr),
            dwAlignmentErr = GetUintDelta(stat.dwAlignmentErr, prevStat.dwAlignmentErr),
        };

        private uint GetUintDelta(uint val, uint prev)
        {
            if (val < prev) // uint overflow
                return 0;
            else
                return val - prev;
        }

        private async Task<IntPtr> ReconnectAsync(VpnData vpnData, CancellationToken cancellationToken)
        {
            var connection = VpnApi.GetConnections().FirstOrDefault(s => s.szEntryName == EntryName);

            if (connection.szEntryName == EntryName && connection.hrasconn != IntPtr.Zero)
            {
                var state = VpnApi.GetState(connection.hrasconn);
                if (state.connectionState == RasConnectionState.Connected)
                {
                    Log.Information("Vpn is already connected");
                    return connection.hrasconn;
                }
                else
                {
                    Log.Information($"Vpn is active but state is '{state.connectionState}'");
                    await HangUpAsync(connection.hrasconn);
                }
            }

            var entries = VpnApi.GetEntryNames();

            var myEntry = entries.FirstOrDefault(s => s.szEntryName == EntryName);
            if (myEntry.szEntryName == EntryName)
            {
                Log.Information($"VPN removing entry");
                VpnApi.DeleteEntry(EntryName);
            }

            CreateEntry(vpnData);

            var id = await DialAsync(vpnData, cancellationToken);

            var info = VpnApi.GetProjectionInfoEx(id);

            return id;
        }

        private async Task<IntPtr> DialAsync(VpnData vpnData, CancellationToken cancellationToken)
        {
            Log.Information($"VPN dialing");

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            var handle = VpnApi.Dial(EntryName, vpnData.User, vpnData.Pwd,
               (message, state, errorCode, extendedErrorCode) =>
               {
                   Log.Information($"VPN dialing {state} ({VpnApi.RasErrorMessage(errorCode)}, {message}, {extendedErrorCode})");
                   if (state == RasConnectionState.Connected)
                       tcs.TrySetResult(true);
                   else
                   {
                       if (errorCode != 0)
                           tcs.TrySetException(new VpnException($"VPN setup failed ({VpnApi.RasErrorMessage(errorCode)})"));
                   }
               });

            try
            {
                var timeout = Task.Delay(10000, cancellationToken);

                var res = await Task.WhenAny(timeout, tcs.Task);
                if (res == timeout)
                    throw new VpnException("Timeout during VPN setup");

                return handle;
            }
            catch
            {
                await HangUpAsync(handle);
                throw;
            }
        }

        private async Task HangUpAsync(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
            {
                Log.Information("VPN hanging up");
                VpnApi.HangUp(handle);

                int counter = 0;
                while (!VpnApi.IsHangUp(handle) && counter++ < 500) // 5 seconds
                {
                    await Task.Delay(10);
                }
                Log.Information("VPN hanged up");
            }
        }

        private void CreateEntry(VpnData vpnData)
        {
            var devices = VpnApi.GetDevices();
            var device = devices.FirstOrDefault(s => s.szDeviceName.ToLower().Contains("sstp"));

            if (device.szDeviceName == null)
                throw new VpnException($"Unable to find VPN client");

            Log.Information("VPN creating entry");
            VpnApi.CreateEntry(EntryName, vpnData.Url, device);
        }
    }
}
