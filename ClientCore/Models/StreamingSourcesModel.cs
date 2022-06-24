using Serilog;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Streamster.ClientCore.Models
{
    public class StreamingSourcesModel
    {
        private CoreData _coreData;

        public ObservableCollection<StreamingSource> Sources { get; } = new ObservableCollection<StreamingSource>();

        public Property<StreamingSource> SelectedSource { get; } = new Property<StreamingSource>();

        public Property<bool> SourcesShown { get; } = new Property<bool>();

        public StreamingSourcesModel(CoreData coreData)
        {
            _coreData = coreData; 
        }

        public void Start()
        {
            SelectedSource.OnChange = (o, n) => Select(n);

            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.SelectedScene, (a, b, c) => Refresh());
            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.State, (a, b, c) => Refresh());
            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.Name, (a, b, c) => Refresh());
            _coreData.Subscriptions.SubscribeForAnyProperty<IIndicatorIngest>((a, b, c, d) => RefreshStat(a));

            Refresh();
        }

        public bool IsSomeOneStreaming()
        {
            if (TryGetCurrentSceneDevice(out var _, out var device) && 
                device.State == DeviceState.Online)
            {
                return device.Type == ClientConstants.ExternalClientId || _coreData.Root.Settings.StreamingToCloudStarted;
            }
            return false;
        }

        public bool IsExternalEncoderStreaming()
        {
            if (TryGetCurrentSceneDevice(out var _, out var device) && 
                device.State == DeviceState.Online)
            {
                return device.Type == ClientConstants.ExternalClientId;
            }
            return false;
        }

        private void RefreshStat(IIndicatorIngest indicator)
        {
            var parent = _coreData.GetParent<IIngest>(indicator);
            var deviceId = parent?.Owner;
            if (parent != null && deviceId != null)
            {
                var source = Sources.FirstOrDefault(s => s.Id == deviceId);
                if (source != null && _coreData.Root.Devices.TryGetValue(deviceId, out var device))
                    RefreshStat(indicator, source, device);
                else 
                    Log.Warning($"Statistics for unknown device '{deviceId}' ignored");
            }
        }

        private void RefreshStat(StreamingSource source)
        {
            var deviceId = source.Id;
            var ingest = _coreData.Root.Ingests.Values.FirstOrDefault(s => s.Owner == deviceId);

            if (ingest?.In != null && _coreData.Root.Devices.TryGetValue(deviceId, out var device))
                RefreshStat(ingest.In, source, device);
            else 
                Log.Warning($"Statistics for unknown device '{deviceId}' not found");
        }

        private void RefreshStat(IIndicatorIngest indicator, StreamingSource source, IDevice device)
        {
            source.State.Value = $"{indicator.Bitrate} kb/s";
        }

        private void Select(StreamingSource ss)
        {
            if (ss != null)
            {
                var scene = _coreData.Root.Scenes.FirstOrDefault(s => s.Value.Owner == ss.Id);
                if (scene.Key != null)
                    _coreData.Root.Settings.SelectedScene = scene.Key;
            }
        }

        private void Refresh()
        {
            var raw = _coreData.Root.Devices.Where(s => ClientConstants.SupportsStreaming(s.Value.Type) && s.Value.State != DeviceState.Inactive).ToList();
            var devices = raw.Select(s => (id: s.Key, name: GetName(s.Value, raw), item: s.Value)).
                              OrderBy(s => s.name).ToList();

            ListHelper.UpdateCollectionNoId(devices, Sources, (a, b) => b.EqualTo(a.id, a.item, a.name), s => new StreamingSource { Id = s.id, Name = s.name, IsOnline = s.item.State == DeviceState.Online });

            foreach (var source in Sources)
                RefreshStat(source);

            if (TryGetCurrentScene(out var scene))
                SelectedSource.SilentValue = Sources.FirstOrDefault(s => s.Id == scene.Owner);
            else 
                SelectedSource.SilentValue = null;

            SourcesShown.Value = Sources.Count > 1;
        }

        private string GetName(IDevice value, List<KeyValuePair<string, IDevice>> raw)
        {
            var desktops = raw.Count(s => s.Value.Type == ClientConstants.WinClientId);
            if (value.Type == ClientConstants.WinClientId) 
                return desktops == 1 && !string.IsNullOrWhiteSpace(value.Name) ? "Desktop" : $"Desktop ({value.Name})";
            if (value.Type == ClientConstants.IosClientId || value.Type == ClientConstants.AndroidClientId)
                return "Mobile";
            if (value.Type == ClientConstants.ExternalClientId)
                return "External encoder";
            return "Unknown device";
        }

        public static string GetShortName(IDevice value)
        {
            if (value.Type == ClientConstants.WinClientId)
                return "desktop";
            if (value.Type == ClientConstants.IosClientId || value.Type == ClientConstants.AndroidClientId)
                return "mobile";
            if (value.Type == ClientConstants.ExternalClientId)
                return "external";
            return "unknown";
        }

        public bool TryGetCurrentScene(out IScene scene)
        {
            scene = null;
            return _coreData.Settings.SelectedScene != null &&
               _coreData.Root.Scenes.TryGetValue(_coreData.Settings.SelectedScene, out scene);
        }

        public bool TryGetCurrentSceneDevice(out IScene scene, out IDevice device)
        {
            scene = null;
            device = null;
            return _coreData.Settings.SelectedScene != null &&
               _coreData.Root.Scenes.TryGetValue(_coreData.Settings.SelectedScene, out scene) &&
               _coreData.Root.Devices.TryGetValue(scene.Owner, out device);
        }

        public bool IsMySceneSelected()
        {
            return _coreData.Root.Settings.SelectedScene != null &&
                    _coreData.Root.Scenes.TryGetValue(_coreData.Root.Settings.SelectedScene, out var scene) &&
                    scene.Owner == _coreData.ThisDeviceId;
        }

        public void SelectScene(string newId)
        {
            _coreData.Root.Settings.SelectedScene = newId;
        }
    }


    public class StreamingSource
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public bool IsOnline { get; set; } 

        public Property<string> State { get; } = new Property<string>();

        public bool EqualTo(string id, IDevice dev, string name) 
        {
            return id == Id && ((dev.State == DeviceState.Online) == IsOnline) && Name == name;
        }
    }
}
