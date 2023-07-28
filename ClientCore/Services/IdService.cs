using DeltaModel;
using Serilog;
using Streamster.ClientCore.Cross;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Services
{
    public class IdService
    {
        private string _deviceId;

        private Task _getDeviceId;
        private readonly LocalSettingsService _localSettingsService;

        public IdService(LocalSettingsService localSettingsService)
        {
            _localSettingsService = localSettingsService;

            _getDeviceId = Task.Run(async () => await ObtainDeviceId());
        }

        private async Task ObtainDeviceId()
        {
#if DEBUG
            var commands = Environment.GetCommandLineArgs();
            var first = commands.FirstOrDefault(s => s.StartsWith("/id:"));
            if (first != null)
            {
                _deviceId = "cmd" + first.Substring(4);
                return;
            }
#endif

            if (!string.IsNullOrEmpty(_localSettingsService.Settings.DeviceId))
            {
                _deviceId = _localSettingsService.Settings.DeviceId;
                Log.Information($"DeviceId={_deviceId}");
            }
            else
            {
                _deviceId = "x" + IdGenerator.New();
                Log.Information($"New DeviceId={_deviceId}");
                await _localSettingsService.ChangeSettingsUnconditionally(s => s.DeviceId = _deviceId);
            }
        }

        public Task WaitDeviceId() => _getDeviceId;

        public string GetDeviceId() => _deviceId;
    }
}
