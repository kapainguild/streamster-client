using DynamicStreamer.Screen;
using Serilog;
using Streamster.ClientApp.Win.Services;
using Streamster.ClientCore.Cross;
using Streamster.ClientData;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using System.Windows;

namespace Streamster.ClientApp.Win
{
    class WinEnvironment : IAppEnvironment
    {
        private string _processorName;
        private string _dataFolder;

        public WinEnvironment(IAppResources appResources)
        {
            _dataFolder = appResources.AppData.DataFolder;
        }

        public string GetClientId() => ClientConstants.WinClientId;


        public void StartObtainProcessorName() => TaskHelper.RunUnawaited(() => Task.Run(() =>
        {
            try
            {
                GetObsVersions(out var obs, out var obsCam);
                Log.Information($"OBS: {obs}, cam: {obsCam}");
            }
            catch
            { }

            try
            {
                var processorSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
                var proc = processorSearcher.Get().OfType<ManagementBaseObject>().First();

                if (proc["Name"] is string name)
                {
                    Log.Information($"Processor '{name}'");
                    _processorName = name;
                }
            }
            catch { }

            try
            {
                Log.Information($"Windows Contract {ScreenCaptureManager.GetApiContract()}");
            }
            catch
            { }
            
        }), "ObtainProcessorName");


        public string GetProcessorName() => _processorName;

        public string GetStorageFolder() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _dataFolder);

        public void SetHighPriorityToApplication()
        {
            try
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            }
            catch (Exception e)
            {
                Log.Error(e, "Setting priority failed");
            }
        }

        public void CopyToClipboard(string str)
        {
            try
            {
                Clipboard.SetText(str);
            }
            catch (Exception e)
            {
                Log.Error(e, $"Copy to clipboard failed");
            }
        }

        public void GetObsVersions(out string obs, out string obsCam)
        {
            ObsHelper.GetObsVersion(out obs, out obsCam);
        }

        public void OpenUrl(string url)
        {
            try
            {
                if (!string.IsNullOrEmpty(url))
                {
                    var lower = url.ToLower();
                    if (!lower.StartsWith("https://") && !lower.StartsWith("http://"))
                        url = "https://" + url;

                    var startInfo = new ProcessStartInfo(url) { UseShellExecute = true };
                    Process.Start(startInfo);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"Failed to open url '{url}'");
            }
        }
    }
}
