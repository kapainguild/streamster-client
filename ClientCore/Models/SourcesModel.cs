using DynamicStreamer.Extensions.WebBrowser;
using DynamicStreamer.Screen;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Capture;

namespace Streamster.ClientCore.Models
{
    public class SourcesModel
    {
        private readonly ILocalVideoSourceManager _localVideoSourceManager;
        private readonly ILocalAudioSourceManager _localAudioSourceManager;

        private readonly ScreenCaptureManager _screenCaptureManager;
        private readonly CoreData _coreData;
        private readonly LocalSettingsService _localSettingsService;
        private readonly IWindowStateManager _windowStateManager;
        private LocalVideoSource[] _videoSources;
        private LocalAudioSource[] _audioSources;

        private CancellationTokenSource _updaterCts;
        private CaptureSource[] _windows;
        private CaptureSource[] _displays;

        private Dictionary<IntPtr, GraphicsCaptureItemWrapper> _captureCache = new Dictionary<IntPtr, GraphicsCaptureItemWrapper>();
        private long _selectionCounter = 1;

        public TransientMessageModel Message { get; } = new TransientMessageModel();

        public SourcesModel(ILocalVideoSourceManager localVideoSourceManager, ILocalAudioSourceManager localAudioSourceManager, CoreData coreData, LocalSettingsService localSettingsService,
            IWindowStateManager windowStateManager)
        {
            _localVideoSourceManager = localVideoSourceManager;
            _localAudioSourceManager = localAudioSourceManager;
            _coreData = coreData;
            _localSettingsService = localSettingsService;
            _windowStateManager = windowStateManager;
            _screenCaptureManager = ScreenCaptureManager.Instance;
        }

        public async Task PrepareAsync()
        {
            _videoSources = await _localVideoSourceManager.GetVideoSourcesAsync();
            _audioSources = await _localAudioSourceManager.GetAudioSourcesAsync();

            UpdateCapture();
        }

        internal void Start()
        {
            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.PreviewAudioSources, (d, c, p) => RefreshDeviceBasedState());
            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.PreviewSources, (d, c, p) => RefreshDeviceBasedState());
            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.State, (d, c, p) => RefreshDeviceBasedState());

            _coreData.ThisDevice.ApiContract = ScreenCaptureManager.GetApiContract();

            UpdateModelWithLocalDevice(_videoSources, _coreData.ThisDevice.VideoInputs);
            UpdateModelWithLocalDevice(_audioSources, _coreData.ThisDevice.AudioInputs);
            UpdateCaptureModel();
            UpdatePluginModel();

            RefreshDeviceBasedState();
        }

        private void UpdatePluginModel()
        {
            bool lovense = PluginContextSetup.IsLoaded();
            _coreData.ThisDevice.PluginFlags = lovense ? (int)PluginFlags.Lovense : 0;
        }

        private void RefreshDeviceBasedState()
        {
            bool anyRequestedPreview = false;

            foreach (var device in _coreData.Root.Devices.Values)
            {
                bool remote = _coreData.GetId(device) != _coreData.ThisDeviceId;
                if ((device.PreviewSources || device.PreviewAudioSources) && device.State == DeviceState.Online)
                    anyRequestedPreview = true;
            }

            StartStopObservation(anyRequestedPreview);
        }

        private void StartStopObservation(bool start)
        {
            if (start)
            {
                if (_updaterCts == null)
                {
                    _updaterCts = new CancellationTokenSource();
                    TaskHelper.RunUnawaited(() => Observe(_updaterCts.Token), "Source Observer");
                }
            }
            else
            {
                _updaterCts?.Cancel();
                _updaterCts = null;
            }
            
        }

        private async Task Observe(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var onlineDevices = _coreData.Root.Devices.Values.Where(s => s.State == DeviceState.Online).ToList();

                if (onlineDevices.Any(s => s.PreviewSources))
                {
                    _videoSources = await _localVideoSourceManager.GetVideoSourcesAsync();
                    UpdateModelWithLocalDevice(_videoSources, _coreData.ThisDevice.VideoInputs);

                    await Task.Run(() => UpdateCapture());
                    UpdateCaptureModel();
                }

                if (onlineDevices.Any(s => s.PreviewAudioSources))
                {
                    _audioSources = await _localAudioSourceManager.GetAudioSourcesAsync();
                    UpdateModelWithLocalDevice(_audioSources, _coreData.ThisDevice.AudioInputs);
                }

                await Task.Delay(1000, token);
            }
        }

        private void UpdateCaptureModel()
        {
            if (_coreData.ThisDevice.Displays == null || !_coreData.ThisDevice.Displays.SequenceEqual(_displays))
                _coreData.ThisDevice.Displays = _displays;

            if (_coreData.ThisDevice.Windows == null || !_coreData.ThisDevice.Windows.SequenceEqual(_windows))
                _coreData.ThisDevice.Windows = _windows;
        }

        private void UpdateCapture()
        {
            _displays = _screenCaptureManager.GetDisplays().Select(s => new CaptureSource { CaptureId = s.Handle.ToInt64(), Name = s.Name, H = s.Height, W = s.Width }).OrderBy(s => s.Name).ToArray();
            _windows = _screenCaptureManager.GetPrograms().Select(s => new CaptureSource { CaptureId = s.Handle.ToInt64(), Name = s.Name, H = s.Height, W = s.Width }).OrderBy(s => s.Name).ToArray();
        }

        public async Task<CaptureSource> SelectFromUi()
        {
            var item = await _screenCaptureManager.UserSelectAsync(_windowStateManager.WindowHandle);
            if (item != null)
            {
                IntPtr p = new IntPtr((((long)int.MaxValue) << 32) | _selectionCounter);

                _captureCache.Add(p, item);
                _selectionCounter++;
                return new CaptureSource { CaptureId = p.ToInt64(), Name = item.Wrapped.DisplayName, W = item.Wrapped.Size.Width, H = item.Wrapped.Size.Height };
            }
            return null;
        }

        public GraphicsCaptureItemWrapper GetOrCreateCaptureItem(CaptureSource source, bool isWindow)
        {
            var items = isWindow ? _windows : _displays;

            var found = items.FirstOrDefault(s => s.Equals(source));
            if (found == null)
                found = items.FirstOrDefault(s => s.Name == source.Name);

            if (found != null)
            {
                var handle = new IntPtr(found.CaptureId);

                if (!_captureCache.TryGetValue(handle, out var item))
                {
                    item = _screenCaptureManager.CreateGraphicsCaptureItem(handle, isWindow);
                    _captureCache[handle] = item;
                }
                if (item.Wrapped.Size.Height == 0 || item.Wrapped.Size.Width == 0)
                {
                    Log.Warning($"{source.CaptureId}- {source.Name} has empty size");
                    return null;
                }
                return item;
            }
            return null;
        }

        public IInputDevice GetBestDefaultAudio()
        {
            // first in the list is in fact default selected in OS
            var items = _audioSources;
            if (items != null && items.Length > 0)
            {
                var first = items[0];
                if (first != null && _coreData.ThisDevice.AudioInputs.TryGetValue(first.Id, out var ai))
                {
                    Log.Information($"Selecting default audio '{first.Name}'");
                    return ai;
                }
            }
            Log.Warning($"Selecting default camera as first in the list (Count={_coreData.ThisDevice.AudioInputs.Count})");
            return _coreData.ThisDevice.AudioInputs.Values.FirstOrDefault();
        }

        public IInputDevice GetBestDefaultVideo()
        {
            var items = _videoSources;
            if (items != null && items.Length > 0)
            {
                var usbDevices = items.Where(s => s.Type == InputDeviceType.USB).ToArray();
                var usbDevicesCapabilities = usbDevices.SelectMany(s => s.Capabilities).ToArray();

                if (usbDevicesCapabilities.Length > 0)
                {
                    var maxResolution = usbDevicesCapabilities.Max(s => s.W);
                    var itemsWithScore = items.
                        Where(s => s.State == InputDeviceState.Ready).
                        Select(s => new { Item = s, Score = (s.Type == InputDeviceType.USB ? 3 : 0) + (s.Capabilities.Any(a => a.W >= maxResolution) ? 1 : 0) }).
                        ToList();

                    if (itemsWithScore.Count > 0)
                    {
                        var maxScore = itemsWithScore.Max(s => s.Score);
                        var item = itemsWithScore.FirstOrDefault(s => s.Score == maxScore)?.Item;

                        if (item != null && _coreData.ThisDevice.VideoInputs.TryGetValue(item.Id, out var vi))
                        {
                            Log.Information($"Selecting default camera '{item.Name}' with score {maxScore}");
                            return vi;
                        }
                    }
                }
            }
            Log.Warning($"Selecting default camera as first in the list (Count={_coreData.ThisDevice.VideoInputs.Count})");
            return _coreData.ThisDevice.VideoInputs.Values.FirstOrDefault();
        }

        private void UpdateModelWithLocalDevice<T>(T[] localSources, IDictionary<string, IInputDevice> model) where T: LocalSource
        {
            foreach (var s in localSources)
            {
                if (!model.TryGetValue(s.Id, out var videoInput))
                {
                    videoInput = _coreData.Create<IInputDevice>();
                    videoInput.Name = s.Name;
                    videoInput.State = s.State;
                    videoInput.Type = s.Type;
                    model[s.Id] = videoInput;
                }
                else
                    videoInput.State = s.State;
            }

            foreach (var item in model)
            {
                if (!localSources.Any(s => s.Id == item.Key))
                    item.Value.State = InputDeviceState.Removed;
            }
            
        }

        private async Task StoreLocalSettingsWithDelay(string videoId, string audioId)
        {
            await Task.Delay(2000); // wait for crash
            await _localSettingsService.ChangeSettings(s =>
            {
                bool result = false;
                if (videoId != null)
                {
                    if (s.LastSelectedVideoId != videoId)
                    {
                        s.LastSelectedVideoId = videoId;
                        result = true;
                    }
                }
                if (audioId != null)
                {
                    if (s.LastSelectedAudioId != audioId)
                    {
                        s.LastSelectedAudioId = audioId;
                        result = true;
                    }
                }
                return result;
            });
        }

        public LocalVideoSource GetLocalVideoDevice(DeviceName deviceName)
        {
            var devices = _videoSources;
            if (devices != null)
            {
                return devices.FirstOrDefault(s => s.Name == deviceName.Name && s.Id == deviceName.DeviceId) ??
                       devices.FirstOrDefault(s => s.Name == deviceName.Name) ??
                       devices.FirstOrDefault(s => s.Id == deviceName.DeviceId);
            }
            return null;
        }

        public LocalAudioSource GetLocalAudioDevice(DeviceName deviceName)
        {
            var devices = _audioSources;
            if (devices != null)
            {
                return devices.FirstOrDefault(s => s.Name == deviceName.Name && s.Id == deviceName.DeviceId) ??
                       devices.FirstOrDefault(s => s.Name == deviceName.Name) ??
                       devices.FirstOrDefault(s => s.Id == deviceName.DeviceId);
            }
            return null;
        }
    }
}
