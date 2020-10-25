using Serilog;
using Streamster.ClientCore.Services;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Streamster.ClientCore.Models
{
    public class MainSettingsModel
    {
        public Property<bool> AutoLogon { get; } = new Property<bool>();

        public Property<bool> EnableVideoPreview { get; } = new Property<bool>();
        public Property<bool> EnableCameraStatusCheck { get; } = new Property<bool>();

        public CoreData CoreData { get; }
        public MainStreamerModel MainStreamerModel { get; }
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

        public Property<SettingsSelectorData<StreamingToCloudBehavior>> CurrentStreamingToCloudBehavior { get; } = new Property<SettingsSelectorData<StreamingToCloudBehavior>>();

        public Property<SettingsSelectorData<EncoderType>> CurrentEncoderType { get; } = new Property<SettingsSelectorData<EncoderType>>();

        public Property<SettingsSelectorData<EncoderQuality>> CurrentEncoderQuality { get; } = new Property<SettingsSelectorData<EncoderQuality>>();

        public Property<SettingsSelectorData<TopMostMode>> CurrentTopMostMode { get; } = new Property<SettingsSelectorData<TopMostMode>>();

        public MainSettingsModel(LocalSettingsService localSettings, CoreData coreData, MainStreamerModel mainStreamerModel)
        {
            CoreData = coreData;
            MainStreamerModel = mainStreamerModel;
            AutoLogon.SilentValue = localSettings.Settings.AutoLogon; // TODO: what is is not registred and not save password
            AutoLogon.OnChange = async (o, n) => await localSettings.ChangeSettingsUnconditionally(s => s.AutoLogon = n);

            EnableVideoPreview.Value = localSettings.Settings.EnableVideoPreview;
            EnableVideoPreview.OnChange = async (o, n) => await localSettings.ChangeSettingsUnconditionally(s => s.EnableVideoPreview = n);

            EnableCameraStatusCheck.Value = !localSettings.Settings.DisableCameraStatusCheck;
            EnableCameraStatusCheck.OnChange = async (o, n) => await localSettings.ChangeSettingsUnconditionally(s => s.DisableCameraStatusCheck = !n);

            CurrentStreamingToCloudBehavior.Value = StreamingToCloudBehaviors.First(s => s.Value == default);
            CurrentEncoderType.Value = EncoderTypes.First(s => s.Value == default);
            CurrentEncoderQuality.Value = EncoderQualities.First(s => s.Value == default);
            CurrentTopMostMode.Value = TopMostModes.First(s => s.Value == TopMostMode.WhenCompact);


            CurrentStreamingToCloudBehavior.OnChange = (o, n) => coreData.Settings.StreamingToCloud = n.Value;
            CurrentEncoderType.OnChange = (o, n) => coreData.Settings.EncoderType = n.Value;
            CurrentEncoderQuality.OnChange = (o, n) => coreData.Settings.EncoderQuality = n.Value;
            CurrentTopMostMode.OnChange = (o, n) =>
            {
                coreData.ThisDevice.DeviceSettings.DisableTopMost = TopMostModeConverter.GetDisableTopMost(n.Value);
                coreData.ThisDevice.DeviceSettings.TopMostExtendedMode = TopMostModeConverter.GetTopMostExtendedMode(n.Value);
            };

            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.StreamingToCloud, (s, c, p) => CurrentStreamingToCloudBehavior.SilentValue = StreamingToCloudBehaviors.FirstOrDefault(r => r.Value == CoreData.Settings.StreamingToCloud));
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.EncoderType, (s, c, p) => CurrentEncoderType.SilentValue = EncoderTypes.FirstOrDefault(r => r.Value == CoreData.Settings.EncoderType));
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.EncoderQuality, (s, c, p) => CurrentEncoderQuality.SilentValue = EncoderQualities.FirstOrDefault(r => r.Value == CoreData.Settings.EncoderQuality));
            CoreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(s => s.DisableTopMost, (s, c, p) => UpdateTopMost());
            CoreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(s => s.TopMostExtendedMode, (s, c, p) => UpdateTopMost());
        }

        private void UpdateTopMost() => CurrentTopMostMode.SilentValue = TopMostModes.FirstOrDefault(s => s.Value == TopMostModeConverter.ToMode(CoreData.ThisDevice.DeviceSettings));

        public void Start()
        {
            if (!IsValidRecordingPath(CoreData.ThisDevice.DeviceSettings.RecordingsPath))
                CoreData.ThisDevice.DeviceSettings.RecordingsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
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

    public class SettingsSelectorData<T>
    {
        public T Value { get; set; }

        public string DisplayName { get; set; }
    }
}
