using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Streamster.ClientApp.Win.Services
{
    class UpdateManager : IUpdateManager
    {
        private readonly IAppEnvironment _environment;
        private readonly LocalSettingsService _localSettingsService;
        private readonly string _domain;

        [DllImport("msi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int MsiEnumRelatedProducts(string lpUpgradeCode, int dwReserved, int iProductIndex, StringBuilder lpProductBuf); 

        public UpdateManager(IAppEnvironment environment, LocalSettingsService localSettingsService, IAppResources appResources)
        {
            _environment = environment;
            _localSettingsService = localSettingsService;
            _domain = appResources.AppData.Domain;
        }

        public Task Update(string appUpdatePath)
        {
            return Task.Run(() => StartUpdater(appUpdatePath));
        }

        private async Task StartUpdater(string appUpdatePath)
        {
            try
            {
                Log.Information($"Update starting");
                var rand = new Random();
                var nextServer = rand.Next(0, ClientConstants.LoadBalancerServers.Length);
                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                path = Path.Combine(path, "..\\Update.exe");

                var suffix = _environment.GetClientId();
                if (!string.IsNullOrEmpty(_domain))
                    suffix += "." + _domain;

                if (!string.IsNullOrEmpty(appUpdatePath))
                    suffix += "." + appUpdatePath;

                string root = $"https://{ClientConstants.LoadBalancerServers[nextServer]}:{ClientConstants.LoadBalancerServerPort}{ClientConstants.LoadBalancerFilesFolder}";
                string pp = $"{root}/{ClientConstants.LoadBalancerFiles_Versions}/{suffix}";
                

                Log.Information($"Update starting({pp}, {path})");
                if (File.Exists(path))
                    Process.Start(path, $"--update={pp}");
                else
                    Log.Warning($"Path '{path}' does not exist");
            }
            catch(Exception e)
            {
                Log.Error(e, "Start updater failed");
            }

            try
            {
                if (_localSettingsService.Settings.RemovePreviousVersion < 3)
                {
                    Log.Information($"Deinstalling prev Generation");
                    await _localSettingsService.ChangeSettingsUnconditionally(s => s.RemovePreviousVersion++);

                    DeInstallPrevGeneration();
                }
            }
            catch(Exception e)
            {
                Log.Error(e, "Start DeInstallPrevGeneration failed");
            }
        }

        private void DeInstallPrevGeneration()
        {
            string upgradeCode = "{3A2165EC-AAEB-41CB-8297-EEA89049F9C4}";
            StringBuilder sbProductCode = new StringBuilder(39);
            int iRes = MsiEnumRelatedProducts(upgradeCode, 0, 0, sbProductCode);
            if (iRes != 0)
                return;
            Log.Information($"Starting Deinstalling process");
            ProcessStartInfo info = new ProcessStartInfo("msiexec") { UseShellExecute = true, Arguments = $"/x {sbProductCode} /qb" };
            Process.Start(info);
        }
    }
}
