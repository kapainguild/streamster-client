using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class MainAboutModel
    {
        private readonly ConnectionService _connectionService;
        private readonly IAppEnvironment _environment;
        private readonly IdService _idService;
        private readonly CoreData _coreData;

        public Property<bool> AsUnregistered { get; } = new Property<bool>();

        public Property<SystemInfoItem[]> SystemInfos { get; } = new Property<SystemInfoItem[]>();

        public Action CopyToClipboard { get; }

        public string License { get; set; }

        public string Credits { get; set; }

        public string Version { get; set; }

        public Property<string> FeedbackText { get; } = new Property<string>("");

        public Action FeedbackSend { get; set; }

        public Property<FeedbackStateEnum> FeedbackState { get; } = new Property<FeedbackStateEnum>(FeedbackStateEnum.Normal);

        public MainAboutModel(RootModel root, ConnectionService connectionService, IAppEnvironment environment, IdService idService, CoreData coreData)
        {
            Root = root;
            _connectionService = connectionService;
            _environment = environment;
            _idService = idService;
            _coreData = coreData;
            CopyToClipboard = () => DoCopyToClipboard();

            Version = ClientVersionHelper.GetVersion();

            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("Streamster.ClientCore.LICENSE.txt"))
            using (StreamReader reader = new StreamReader(stream))
            {
                License = reader.ReadToEnd();
            }

            using (Stream stream = assembly.GetManifestResourceStream("Streamster.ClientCore.CREDITS.txt"))
            using (StreamReader reader = new StreamReader(stream))
            {
                Credits = reader.ReadToEnd();
            }

            FeedbackSend = () => _ = SendFeedback();
        }

        private async Task SendFeedback()
        {
            if (!string.IsNullOrEmpty(FeedbackText.Value))
            {
                FeedbackState.Value = FeedbackStateEnum.Sending;
                _coreData.Root.Settings.UserFeedback = FeedbackText.Value;
                FeedbackText.SilentValue = "";

                await Task.Delay(1500);

                FeedbackState.Value = FeedbackStateEnum.Sent;

                await Task.Delay(6000);
                FeedbackState.Value = FeedbackStateEnum.Normal;
                
            }
        }

        private void DoCopyToClipboard()
        {
            if (SystemInfos.Value != null)
                _environment.CopyToClipboard(string.Join(Environment.NewLine, SystemInfos.Value.Select(s => $"{s.Name}: '{s.Value}'")));
            else
                _environment.CopyToClipboard("No system information");
        }

        public RootModel Root { get; }

        public void Start()
        {
            AsUnregistered.Value = _connectionService.UserName == null;

            try
            {
                _environment.GetObsVersions(out var obs, out var obsCam);
                SystemInfos.Value = new[]
                {
                    new SystemInfoItem { Name = "Logged as", Value =  _connectionService.UserName ?? "Unregistered"},
                    new SystemInfoItem { Name = "Processor", Value = _environment.GetProcessorName()},
                    new SystemInfoItem { Name = "Device Id", Value = _idService.GetDeviceId()},
                    new SystemInfoItem { Name = "OS", Value = System.Runtime.InteropServices.RuntimeInformation.OSDescription},
                    new SystemInfoItem { Name = "Start time UTC", Value = DateTime.UtcNow.ToString()},
                    new SystemInfoItem { Name = "Server", Value = _connectionService.ConnectionServer.Split(':')[0]},
                    new SystemInfoItem { Name = "App version", Value = ClientVersionHelper.GetVersion() },
                    new SystemInfoItem { Name = "OBS version", Value =  obs },
                    new SystemInfoItem { Name = "OBS Cam version", Value =  obsCam }
                }.Where(s => s.Value != null).ToArray();

                SystemInfos.Value.Where(s => s.Id != null).ToList().ForEach(s => Log.Information($"SystemInfo {{{s.Id}}}", s.Value));

            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to get system info");
            }
        }
    }

    public class SystemInfoItem
    {
        public string Name { get; set; }

        public string Value { get; set; }

        public string Id { get; set; }
    }

    public enum FeedbackStateEnum
    {
        Normal,
        Sending,
        Sent
    }
}
