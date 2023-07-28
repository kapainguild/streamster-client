using DynamicStreamer.DirectXHelpers;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Streamster.ClientCore.Models
{
    public class MainSettingsModel
    {
        private readonly ConnectionService _connectionService;
        private readonly IAppResources _appResources;

        public Property<bool> AutoLogon { get; } = new Property<bool>();

        public CoreData CoreData { get; }
        public StreamSettingsModel StreamSettings { get; }
        public TranscodingModel Transcoding { get; }
        public ExternalEncoderModel ExternalEncoder { get; }
        public ExternalPreviewModel ExternalPreview { get; }

        public event EventHandler ChangeServerRequested;

        public List<SettingsSelectorData<StreamingToCloudBehavior>> StreamingToCloudBehaviors { get; } = new List<SettingsSelectorData<StreamingToCloudBehavior>>
        {
            new SettingsSelectorData<StreamingToCloudBehavior> { Value = StreamingToCloudBehavior.AppStart, DisplayName = "App is started" },
            new SettingsSelectorData<StreamingToCloudBehavior> { Value = StreamingToCloudBehavior.FirstChannel, DisplayName = "First channel is On" },
            new SettingsSelectorData<StreamingToCloudBehavior> { Value = StreamingToCloudBehavior.Manually, DisplayName = "I do it manually" },
        };

        public List<SettingsSelectorData<EncoderType>> EncoderTypes { get; } = new List<SettingsSelectorData<EncoderType>>
        {
            new SettingsSelectorData<EncoderType> { Value = EncoderType.Auto, DisplayName = "Auto" },
            new SettingsSelectorData<EncoderType> { Value = EncoderType.Hardware, DisplayName = "Hardware" },
            new SettingsSelectorData<EncoderType> { Value = EncoderType.Software, DisplayName = "Software" },
        };

        public List<SettingsSelectorData<EncoderQuality>> EncoderQualities { get; } = new List<SettingsSelectorData<EncoderQuality>>
        {
            new SettingsSelectorData<EncoderQuality> { Value = EncoderQuality.Speed, DisplayName = "Speed" },
            new SettingsSelectorData<EncoderQuality> { Value = EncoderQuality.Balanced, DisplayName = "Balanced" },
            new SettingsSelectorData<EncoderQuality> { Value = EncoderQuality.BalancedQuality, DisplayName = "Balanced quality" },
            new SettingsSelectorData<EncoderQuality> { Value = EncoderQuality.Quality, DisplayName = "Quality" },
        };

        public List<SettingsSelectorData<TopMostMode>> TopMostModes { get; } = new List<SettingsSelectorData<TopMostMode>>
        {
            new SettingsSelectorData<TopMostMode> { Value = TopMostMode.Always, DisplayName = "Always" },
            new SettingsSelectorData<TopMostMode> { Value = TopMostMode.WhenCompact, DisplayName = "When compact" },
            new SettingsSelectorData<TopMostMode> { Value = TopMostMode.Manual, DisplayName = "By pin in titlebar" },
            new SettingsSelectorData<TopMostMode> { Value = TopMostMode.Never, DisplayName = "Never" },
        };

        public List<SettingsSelectorData<VpnBehavior>> VpnBehaviors { get; } = new List<SettingsSelectorData<VpnBehavior>>
        {
            new SettingsSelectorData<VpnBehavior> { Value = VpnBehavior.AppStart, DisplayName = "App is started" },
            new SettingsSelectorData<VpnBehavior> { Value = VpnBehavior.Manually, DisplayName = "I do it manually" },
        };

        public List<SettingsSelectorData<RendererType>> RendererTypes { get; } = new List<SettingsSelectorData<RendererType>>
        {
            new SettingsSelectorData<RendererType> { Value = RendererType.HardwareAuto, DisplayName = "Hardware Auto" },
            new SettingsSelectorData<RendererType> { Value = RendererType.HardwareSpecific, DisplayName = "Select HW Adapter" },
            new SettingsSelectorData<RendererType> { Value = RendererType.SoftwareFFMPEG, DisplayName = "Software" },
            //new SettingsSelectorData<RendererType> { Value = RendererType.SoftwareDirectX, DisplayName = "Software Full" },
        };

        public List<SettingsSelectorData<BlenderType>> BlenderTypes { get; } = new List<SettingsSelectorData<BlenderType>>
        {
            new SettingsSelectorData<BlenderType> { Value = BlenderType.Smart, DisplayName = "Smart" },
            new SettingsSelectorData<BlenderType> { Value = BlenderType.Linear, DisplayName = "Linear" },
            new SettingsSelectorData<BlenderType> { Value = BlenderType.Lanczos, DisplayName = "Lanczos" },
            new SettingsSelectorData<BlenderType> { Value = BlenderType.BilinearLowRes, DisplayName = "Bilinear" },
            new SettingsSelectorData<BlenderType> { Value = BlenderType.Bicubic, DisplayName = "Bicubic" },
            new SettingsSelectorData<BlenderType> { Value = BlenderType.Area, DisplayName = "Area" },
        };

        public List<SettingsSelectorData<RecordingFormat>> RecordingFormats { get; } = new List<SettingsSelectorData<RecordingFormat>>
        {
            new SettingsSelectorData<RecordingFormat> { Value = RecordingFormat.Flv, DisplayName = "flv" },
            new SettingsSelectorData<RecordingFormat> { Value = RecordingFormat.Mp4, DisplayName = "mp4" },
        };

        public ObservableCollection<string> HardwareAdapters { get; } = new ObservableCollection<string>();

        public Property<SettingsSelectorData<StreamingToCloudBehavior>> CurrentStreamingToCloudBehavior { get; } = new Property<SettingsSelectorData<StreamingToCloudBehavior>>();

        public Property<SettingsSelectorData<EncoderType>> CurrentEncoderType { get; } = new Property<SettingsSelectorData<EncoderType>>();

        public Property<SettingsSelectorData<EncoderQuality>> CurrentEncoderQuality { get; } = new Property<SettingsSelectorData<EncoderQuality>>();

        public Property<SettingsSelectorData<TopMostMode>> CurrentTopMostMode { get; } = new Property<SettingsSelectorData<TopMostMode>>();

        public Property<SettingsSelectorData<VpnBehavior>> CurrentVpnBehavior { get; } = new Property<SettingsSelectorData<VpnBehavior>>();

        public Property<SettingsSelectorData<RendererType>> CurrentRendererType { get; } = new Property<SettingsSelectorData<RendererType>>();

        public Property<SettingsSelectorData<BlenderType>> CurrentBlenderType { get; } = new Property<SettingsSelectorData<BlenderType>>();

        public Property<SettingsSelectorData<RecordingFormat>> CurrentRecordingFormat { get; } = new Property<SettingsSelectorData<RecordingFormat>>();

        public Property<string> HardwareAdapter { get; } = new Property<string>();

        public Property<bool> PreferNalHdr { get; } = new Property<bool>();

        public Property<bool> EnableQsvNv12Optimization { get; } = new Property<bool>(true);

        public Action OpenTranscoding { get; }

        public Action ChangeServer { get; }

        public Property<bool> CanServerBeChanged { get; } = new Property<bool>(true);

        public bool UserHasVpn { get; set; }

        public string TariffUrl { get; set; }

        public MainSettingsModel(LocalSettingsService localSettings, CoreData coreData, StreamSettingsModel streamSettings, ConnectionService connectionService,
            TranscodingModel transcoding, ExternalEncoderModel externalEncoder, ExternalPreviewModel externalPreview, IAppResources appResources)
        {
            CoreData = coreData;
            StreamSettings = streamSettings;
            Transcoding = transcoding;
            ExternalEncoder = externalEncoder;
            _connectionService = connectionService;
            AutoLogon.SilentValue = localSettings.Settings.AutoLogon; // TODO: what is is not registred and not save password
            AutoLogon.OnChange = async (o, n) => await localSettings.ChangeSettingsUnconditionally(s => s.AutoLogon = n);

            CurrentStreamingToCloudBehavior.Value = StreamingToCloudBehaviors.First(s => s.Value == default);
            CurrentEncoderType.Value = EncoderTypes.First(s => s.Value == default);
            CurrentEncoderQuality.Value = EncoderQualities.First(s => s.Value == default);
            CurrentTopMostMode.Value = TopMostModes.First(s => s.Value == TopMostMode.WhenCompact);
            CurrentVpnBehavior.Value = VpnBehaviors.First(s => s.Value == default);
            CurrentRendererType.Value = RendererTypes.First(s => s.Value == default);
            CurrentBlenderType.Value = BlenderTypes.First(s => s.Value == default);
            CurrentRecordingFormat.Value = RecordingFormats.First(s => s.Value == default);

            CurrentStreamingToCloudBehavior.OnChange = (o, n) => coreData.Settings.StreamingToCloud = n.Value;
            CurrentEncoderType.OnChange = (o, n) => coreData.Settings.EncoderType = n.Value;
            CurrentEncoderQuality.OnChange = (o, n) => coreData.Settings.EncoderQuality = n.Value;
            CurrentTopMostMode.OnChange = (o, n) =>
            {
                coreData.ThisDevice.DeviceSettings.DisableTopMost = TopMostModeConverter.GetDisableTopMost(n.Value);
                coreData.ThisDevice.DeviceSettings.TopMostExtendedMode = TopMostModeConverter.GetTopMostExtendedMode(n.Value);
            };
            CurrentRecordingFormat.OnChange = (o, n) => coreData.Settings.RecordingFormat = n.Value;
            CurrentVpnBehavior.OnChange = (o, n) => coreData.ThisDevice.DeviceSettings.VpnBehavior = n.Value;
            CurrentRendererType.OnChange = (o, n) => coreData.ThisDevice.DeviceSettings.RendererType = n.Value;
            CurrentBlenderType.OnChange = (o, n) => coreData.ThisDevice.DeviceSettings.BlenderType = n.Value;
            HardwareAdapter.OnChange = (o, n) => coreData.ThisDevice.DeviceSettings.RendererAdapter = n;
            PreferNalHdr.OnChange = (o, n) => coreData.Settings.PreferNalHdr = n;
            EnableQsvNv12Optimization.OnChange = (o, n) => coreData.Settings.DisableQsvNv12Optimization = !n;

            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.StreamingToCloud, (s, c, p) => CurrentStreamingToCloudBehavior.SilentValue = StreamingToCloudBehaviors.FirstOrDefault(r => r.Value == CoreData.Settings.StreamingToCloud));
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.EncoderType, (s, c, p) => CurrentEncoderType.SilentValue = EncoderTypes.FirstOrDefault(r => r.Value == CoreData.Settings.EncoderType));
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.EncoderQuality, (s, c, p) => CurrentEncoderQuality.SilentValue = EncoderQualities.FirstOrDefault(r => r.Value == CoreData.Settings.EncoderQuality));
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.PreferNalHdr, (s, c, p) => PreferNalHdr.SilentValue = CoreData.Settings.PreferNalHdr);
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.DisableQsvNv12Optimization, (s, c, p) => EnableQsvNv12Optimization.SilentValue = !CoreData.Settings.DisableQsvNv12Optimization);
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.RecordingFormat, (s, c, p) => CurrentRecordingFormat.SilentValue = RecordingFormats.FirstOrDefault(r => r.Value == CoreData.Settings.RecordingFormat));

            CoreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(s => s.DisableTopMost, (s, c, p) => UpdateTopMost());
            CoreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(s => s.TopMostExtendedMode, (s, c, p) => UpdateTopMost());
            CoreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(s => s.VpnBehavior, (s, c, p) => CurrentVpnBehavior.SilentValue = VpnBehaviors.FirstOrDefault(r => r.Value == CoreData.ThisDevice.DeviceSettings.VpnBehavior));
            CoreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(s => s.RendererType, (s, c, p) => CurrentRendererType.SilentValue = RendererTypes.FirstOrDefault(r => r.Value == CoreData.ThisDevice.DeviceSettings.RendererType));
            CoreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(s => s.BlenderType, (s, c, p) => CurrentBlenderType.SilentValue = BlenderTypes.FirstOrDefault(r => r.Value == CoreData.ThisDevice.DeviceSettings.BlenderType));
            CoreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(s => s.RendererAdapter, (s, c, p) =>
            {
                if (HardwareAdapters != null)
                    HardwareAdapter.SilentValue = HardwareAdapters.Contains(CoreData.ThisDevice.DeviceSettings.RendererAdapter) ? CoreData.ThisDevice.DeviceSettings.RendererAdapter : HardwareAdapters.FirstOrDefault();
            });
            ExternalPreview = externalPreview;
            _appResources = appResources;
            ChangeServer = () => ChangeServerRequested?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateTopMost() => CurrentTopMostMode.SilentValue = TopMostModes.FirstOrDefault(s => s.Value == TopMostModeConverter.ToMode(CoreData.ThisDevice.DeviceSettings));

        public void Start()
        {
            TariffUrl = _connectionService.UserName == null ? _appResources.AppData.PricingUrlForNotRegistered : _appResources.AppData.PricingUrl;
            UserHasVpn = _connectionService.Claims.HasVpn;

            if (!IsValidRecordingPath(CoreData.ThisDevice.DeviceSettings.RecordingsPath))
                CoreData.ThisDevice.DeviceSettings.RecordingsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

            var adapters = DirectXContextFactory.GetAdapters();

            if (string.IsNullOrEmpty(CoreData.ThisDevice.DeviceSettings.RendererAdapter) && adapters.Length > 0)
                CoreData.ThisDevice.DeviceSettings.RendererAdapter = adapters[0].Name;

            HardwareAdapters.Clear();
            adapters.ToList().ForEach(s => HardwareAdapters.Add(s.Name));
            HardwareAdapter.Value = HardwareAdapters.Contains(CoreData.ThisDevice.DeviceSettings.RendererAdapter) ? CoreData.ThisDevice.DeviceSettings.RendererAdapter : adapters.FirstOrDefault()?.Name;

            ExternalEncoder.Start();
            ExternalPreview.Start();
        }

        public static bool IsValidRecordingPath(string pathForRecordings)
        {
            if (string.IsNullOrEmpty(pathForRecordings))
            {
                Log.Information($"Recording Path '{pathForRecordings}' empty");
                return false;
            }
            try
            {
                bool result = Directory.Exists(pathForRecordings);
                if (!result)
                    Log.Warning($"Recording Path '{pathForRecordings}' doesnot exist");
                return result;
            }
            catch (Exception e){
                Log.Warning(e, $"Error while checking Recording path '{pathForRecordings}'");
            }
            return false;
        }
    }

    public enum LayoutType
    {
        NoScreen,
        Standart,
        ScreenAndIndicators,
        ScreenOnly
    }

    public class SettingsSelectorData<T>
    {
        public T Value { get; set; }

        public string DisplayName { get; set; }
    }
}
