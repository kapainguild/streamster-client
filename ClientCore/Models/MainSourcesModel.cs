using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class MainSourcesModel
    {
        private readonly ILocalVideoSourceManager _localVideoSourceManager;
        private readonly ILocalAudioSourceManager _localAudioSourceManager;
        private readonly CoreData _coreData;
        private readonly LocalSettingsService _localSettingsService;
        private readonly SynchronizationContext _syncContext;
        private bool _remotePreviewRequested;
        private bool _isReady;
        private string _preparedDeviceId;

        public MainStreamerModel Streamer { get; private set; }

        public Property<LocalVideoInputModel> CurrentPreview { get; } = new Property<LocalVideoInputModel>();

        public Property<string> CurrentVideo { get; } = new Property<string>();

        public Property<string> CurrentAudio { get; } = new Property<string>();

        public TransientMessageModel Message { get; } = new TransientMessageModel();

        public MainSourcesModel(ILocalVideoSourceManager localVideoSourceManager, ILocalAudioSourceManager localAudioSourceManager, CoreData coreData, LocalSettingsService localSettingsService)
        {
            _localVideoSourceManager = localVideoSourceManager;
            _localAudioSourceManager = localAudioSourceManager;
            _coreData = coreData;
            _localSettingsService = localSettingsService;
            _syncContext = SynchronizationContext.Current;

        }

        public bool IsReady() => _isReady;

        public async Task PrepareAsync(MainStreamerModel mainStreamerModel)
        {
            Streamer = mainStreamerModel;
            var lastCamera = _localSettingsService.Settings.LastSelectedVideoId;

            var devices = await _localVideoSourceManager.RetrieveSourcesListAsync();
            await _localAudioSourceManager.RetrieveSourcesListAsync();

            bool hasLastCamera = devices.Any(s => s.Id == lastCamera);
            if (!hasLastCamera)
                lastCamera = devices.FirstOrDefault()?.Id;
            _preparedDeviceId = lastCamera;

            if (lastCamera != null)
                await _localVideoSourceManager.GetUpdatedVideoSourceAsync(lastCamera);
        }

        internal void Start()
        {
            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.PreviewVideo, (d, c, p) => RefreshDeviceBasedState());
            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.PreviewAudio, (d, c, p) => RefreshDeviceBasedState());
            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.State, (d, c, p) => RefreshDeviceBasedState());
            _coreData.Subscriptions.SubscribeForProperties<IVideoInput>(s => s.State, (i, c, p) => _coreData.GetLocal<LocalVideoInputModel>(i)?.State.SetValueAsMethod(i.State));
            _coreData.Subscriptions.SubscribeForProperties<IAudioInput>(s => s.State, (i, c, p) => _coreData.GetLocal<LocalAudioInputModel>(i)?.State.SetValueAsMethod(i.State));

            _coreData.Subscriptions.SubscribeForProperties<IVideoInput>(s => s.Preview, (i, c, p) =>
            {
                var model = _coreData.GetLocal<LocalVideoInputModel>(i); 
                if (model.Type == InputType.Remote) // for local cameras we have shortcut
                    model.Preview.Value = i.Preview?.Data;
            });

            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.RequestedVideo, (i, c, p) => SelectVideoInput(_coreData.Settings.RequestedVideo, false));
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.RequestedAudio, (i, c, p) => SelectAudioInput(_coreData.Settings.RequestedAudio));
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.SelectedVideo, (i, c, p) => UpdateVideoSelection(_coreData.Settings.SelectedVideo));
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.SelectedAudio, (i, c, p) => UpdateAudioSelection(_coreData.Settings.SelectedAudio));

            _coreData.Root.VideoInputs.Values.OfType<IInput>()
                .Concat(_coreData.Root.AudioInputs.Values)
                .Where(s => s.Owner == _coreData.ThisDeviceId)
                .ToList()
                .ForEach(vi => vi.State = InputState.Removed);

            _localVideoSourceManager.Start(OnLocalDeviceManagerVideoChanged, OnLocalDeviceManagerPreviewAvailable);
            _localAudioSourceManager.Start(OnLocalDeviceManagerAudioChanged);

            UpdateVideoSelection(_coreData.Settings.SelectedVideo);
            UpdateAudioSelection(_coreData.Settings.SelectedAudio);
            SelectStartupCamera();
            RefreshDeviceBasedState();
        }

        private void UpdateVideoSelection(string selectedVideo)
        {
            if (selectedVideo != null && _coreData.Root.VideoInputs.TryGetValue(selectedVideo, out var videoInput))
                CurrentVideo.Value = videoInput.Name;
            else
                CurrentVideo.Value = "No video source selected"; 
        }

        private void UpdateAudioSelection(string selectedAudio)
        {
            if (selectedAudio != null && _coreData.Root.AudioInputs.TryGetValue(selectedAudio, out var audioInput))
                CurrentAudio.Value = audioInput.Name;
            else
                CurrentAudio.Value = "No audio source selected";
        }


        internal void LocalRequest(LocalVideoInputModel videoDeviceModel)
        {
            var owner = videoDeviceModel.Source.Owner;
            if (Streamer.ChangeStreamParamsDisabled.Value &&
                _coreData.Settings.SelectedVideo != null &&
                _coreData.Root.VideoInputs.TryGetValue(_coreData.Settings.SelectedVideo, out var current) &&
                current.Owner != owner)
            {
                Message.Show("Cannot change source when restreaming or recording", TransientMessageType.Error);
            }
            else
            {
                _coreData.Settings.RequestedVideo = _coreData.GetId(videoDeviceModel.Source);
            }
        }

        internal void LocalRequest(LocalAudioInputModel localAudioSourceModel)
        {
            var audioOwner = localAudioSourceModel.Source.Owner;
            var selectedVideoId = _coreData.Settings.SelectedVideo;
            if (selectedVideoId != null && _coreData.Root.VideoInputs.TryGetValue(selectedVideoId, out var videoInput))
            {
                if (videoInput.Owner != audioOwner)
                {
                    Message.Show("Selection of audio source and video source from different devices are not currently supported", TransientMessageType.Error);
                    return;
                }
            }
            _coreData.Settings.RequestedAudio = _coreData.GetId(localAudioSourceModel.Source);
        }

        private void SelectStartupCamera()
        {
            var modelSelection = _coreData.Root.Settings.SelectedVideo;

            IVideoInput videoInput = null;
            if (modelSelection == null || !_coreData.Root.VideoInputs.ContainsKey(modelSelection))
            {
                // maybe first start or camera was removed or device with the camera is not online
                videoInput = GetLastOrFirstLocalCamera();
            }
            else
                videoInput = _coreData.Root.VideoInputs[modelSelection];
            
            // check whether owner is online
            if (videoInput != null)
            {
                if (_coreData.Root.Devices.TryGetValue(videoInput.Owner, out var owner))
                {
                    if (owner.State != DeviceState.Online && videoInput.Owner != _coreData.ThisDeviceId)
                    {
                        videoInput = GetLastOrFirstLocalCamera();
                    }
                }
                else
                {
                    Log.Warning($"Owner '{owner}' of camera not found");
                    videoInput = GetLastOrFirstLocalCamera();
                }
            }
            
            if (videoInput == null)
            {
                Message.Show("No video sources available", TransientMessageType.Error);
                _coreData.Root.Settings.SelectedVideo = null;
            }
            else
            {
                SelectVideoInput(_coreData.GetId(videoInput), startup: true);
            }
        }

        private void SelectAudioInput(string id)
        {
            if (id == null)
            {
                //TODO: any state update as this is confirmation of processed request
                return;
            }

            if (_coreData.Root.AudioInputs.TryGetValue(id, out var audioInput))
            {
                if (audioInput.Owner == _coreData.ThisDeviceId)
                {
                    _ = SelectLocalAudioInputAsync(audioInput);
                }
            }
            else
            {
                Log.Warning($"Audio input '{id}' not found");
                Message.Show("Data synchronization failed", TransientMessageType.Error);
            }
        }


        private void SelectVideoInput(string id, bool startup)
        {
            if (id == null)
            {
                //TODO: any state update as this is confirmation of processed request
                return;
            }

            if (_coreData.Root.VideoInputs.TryGetValue(id, out var videoInput))
            {
                if (videoInput.Owner == _coreData.ThisDeviceId)
                {
                    if (startup && _preparedDeviceId != GetLocalSourceId(_coreData.GetId(videoInput)))
                        Log.Warning($"Mismatch of prepared '{_preparedDeviceId}' and requested '{GetLocalSourceId(_coreData.GetId(videoInput))}' devices during startup");

                    _ = SelectLocalVideoInputAsync(videoInput, startup);
                }
                else
                {
                    _localVideoSourceManager.SetRunningSource(null);
                    _localAudioSourceManager.SetRunningSource(null);
                    _isReady = true;
                    Streamer.RefreshStreamer();
                }
            }
            else
            {
                Log.Warning($"Video input '{id}' not found");
                Message.Show("Data synchronization failed", TransientMessageType.Error);
            }
        }

        private async Task SelectLocalVideoInputAsync(IVideoInput videoInput, bool startup)
        {
            int msgId = Message.Show($"Connecting '{videoInput.Name}'...", TransientMessageType.Progress, false);

            var videoDevice = await _localVideoSourceManager.GetUpdatedVideoSourceAsync(GetLocalSourceId(_coreData.GetId(videoInput)));

            if (IsOkToSwitch(videoDevice, videoInput.Name))
            {
                var audioInput = await GetLocalAudioInputWhichIsReadyOrRunning();
                var localAudioId = audioInput == null ? null : GetLocalSourceId(_coreData.GetId(audioInput));

                TaskHelper.RunUnawaited(async () => await StoreLocalSettingsWithDelay(videoDevice.Id, localAudioId), "Delayed update of LastSelectedCameraId");

                _localVideoSourceManager.SetRunningSource(GetLocalSourceId(_coreData.GetId(videoInput)));
                _localAudioSourceManager.SetRunningSource(localAudioId);
                _isReady = true;
                var audio = audioInput == null ? null : _coreData.GetId(audioInput);
                _coreData.RunOnMainThread(() =>
                {
                    _coreData.Settings.SelectedVideo = _coreData.GetId(videoInput);
                    _coreData.Settings.SelectedAudio = audio;
                    _coreData.Settings.RequestedVideo = null;
                    _coreData.Settings.RequestedAudio = null;
                });

                await WaitForNextFrame(videoDevice.Name, msgId);
            }
            else
            {
                //we are starting and source is bad
                if (startup)
                {
                    _coreData.RunOnMainThread(() =>
                    {
                        _coreData.Settings.SelectedVideo = null;
                        _coreData.Settings.SelectedAudio = null;
                    });
                }
            }
        }

        private async Task WaitForNextFrame(string name, int msgId)
        {
            await Task.Delay(200); // give some time to streamer to switch

            if (_coreData.ThisDevice.DisplayVideoHidden)
            {
                Message.Show($"'{name}' connected", TransientMessageType.Info, true, msgId);
            }
            else
            {
                int version = Streamer.ScreenRenderer.BufferVersion;
                for(int q = 0; q < 60; q++)
                {
                    await Task.Delay(50);
                    if (version != Streamer.ScreenRenderer.BufferVersion)
                    {
                        Message.Show($"'{name}' connected", TransientMessageType.Info, true, msgId);
                        return;
                    }
                }
                Message.Show($"Something went wrong with '{name}' connection", TransientMessageType.Error, true, msgId);
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

        private async Task SelectLocalAudioInputAsync(IAudioInput audioInput)
        {
            int msgId = Message.Show($"Connecting '{audioInput.Name}'", TransientMessageType.Info);
            var audioDevices = await _localAudioSourceManager.RetrieveSourcesListAsync();
            var device = audioDevices.FirstOrDefault(s => s.Id == GetLocalSourceId(_coreData.GetId(audioInput)));
            if (IsOkToSwitch(device, device.Name))
            {
                var localAudioId = device.Id;
                TaskHelper.RunUnawaited(async () => await StoreLocalSettingsWithDelay(null, localAudioId), "Delayed update of LastSelectedAudio");

                _localAudioSourceManager.SetRunningSource(localAudioId);
                _coreData.RunOnMainThread(() =>
                {
                    _coreData.Settings.SelectedAudio = GetGlobalInputId(localAudioId);
                    _coreData.Settings.RequestedAudio = null;
                });
                await WaitForNextFrame(device.Name, msgId);
            }
        }

        private async Task<IAudioInput> GetLocalAudioInputWhichIsReadyOrRunning()
        {
            var audioDevices = await _localAudioSourceManager.RetrieveSourcesListAsync();
            var selected = _coreData.Settings.SelectedAudio;
            if (selected == null || 
                !_coreData.Root.AudioInputs.TryGetValue(selected, out var audioInput) ||
                audioInput.Owner != _coreData.ThisDeviceId)
            {
                audioInput = GetLastOrFirstLocalAudio();
            }

            if (audioInput != null)
            {
                var device = audioDevices.FirstOrDefault(s => s.Id == GetLocalSourceId(_coreData.GetId(audioInput)));
                if (device != null && (device.State == InputState.Ready || device.State == InputState.Running))
                {
                    return audioInput;
                }
            }

            foreach(var input in _coreData.Root.AudioInputs.Values.Where(s => s.Owner == _coreData.ThisDeviceId))
            {
                var device = audioDevices.FirstOrDefault(s => s.Id == GetLocalSourceId(_coreData.GetId(input)));
                if (device != null && (device.State == InputState.Ready || device.State == InputState.Running))
                {
                    return input;
                }
            }
            Message.Show($"No working audio device found", TransientMessageType.Error);
            return null;
        }

        private bool IsOkToSwitch(IBaseSource source, string sourceName)
        {
            if (source == null)
            {
                Message.Show($"Looks like '{sourceName}' is removed", TransientMessageType.Error);
                return false;
            }
            else if (source.State == InputState.Ready || source.State == InputState.Running)
            {
                return true;
            }
            else
            {
                Message.Show(GetMessageForAudioVideoState(source.State, sourceName), TransientMessageType.Error);
                return false;
            }
        }

        private IVideoInput GetLastOrFirstLocalCamera() => GetLastOrFirstLocalSource(_coreData.Root.VideoInputs.Values, _localSettingsService.Settings.LastSelectedVideoId);

        private IAudioInput GetLastOrFirstLocalAudio() => GetLastOrFirstLocalSource(_coreData.Root.AudioInputs.Values, _localSettingsService.Settings.LastSelectedAudioId);

        private T GetLastOrFirstLocalSource<T>(ICollection<T> sources, string localSetting) where T: class, IInput
        {
            T source = null;
            if (!string.IsNullOrEmpty(localSetting))
            {
                var lastLocalCameraId = GetGlobalInputId(localSetting);
                source = sources.FirstOrDefault(s => _coreData.GetId(s) == lastLocalCameraId);
            }
            // last selected camera is not available
            if (source == null)
                source = sources.FirstOrDefault(s => s.Owner == _coreData.ThisDeviceId);

            return source;
        }

        private void RefreshDeviceBasedState()
        {
            bool anyRequestedVideoPreview = false;
            bool anyRemoteRequestedVideoPreview = false;
            bool anyRequestedAudioPreview = false;
            foreach (var device in _coreData.Root.Devices.Values)
            {
                bool remote = _coreData.GetId(device) != _coreData.ThisDeviceId;
                if (device.PreviewVideo)
                {
                    anyRequestedVideoPreview = true;
                    if (remote)
                        anyRemoteRequestedVideoPreview = true;
                }

                if (device.PreviewAudio)
                    anyRequestedAudioPreview = true;

                if (remote)
                {
                    foreach(var input in _coreData.Root.VideoInputs.Values.OfType<IInput>()
                        .Concat(_coreData.Root.AudioInputs.Values).Where(s => s.Owner == _coreData.GetId(device)))
                    {
                        var local = _coreData.GetLocal<LocalBaseInputModel>(input);
                        switch (device.State)
                        {
                            case DeviceState.Inactive:
                                local.State.Value = InputState.RemoteDeviceInactive;
                                break;
                            case DeviceState.Offline:
                                local.State.Value = InputState.RemoteDeviceOffline;
                                break;
                            case DeviceState.Online:
                                local.State.Value = input.State;
                                break;
                            default:
                                local.State.Value = InputState.RemoteDeviceInactive;
                                break;
                        }
                    }
                }
            }
            RefreshAudioOwnerState();

            _remotePreviewRequested = anyRemoteRequestedVideoPreview;
            if (anyRequestedVideoPreview) _localVideoSourceManager.StartObservation();
            else _localVideoSourceManager.StopObservation();

            if (anyRequestedAudioPreview)
                _ = _localAudioSourceManager.RetrieveSourcesListAsync();
        }

        private void RefreshAudioOwnerState()
        {
            var video = _coreData.Root.Settings.SelectedVideo;
            if (video != null && _coreData.Root.VideoInputs.TryGetValue(video, out var videoInput))
            {
                var owner = videoInput.Owner;
                _coreData.Root.AudioInputs.Values.Select(s => _coreData.GetLocal<LocalAudioInputModel>(s)).ToList().ForEach(a => a.SameOwnerAsVideo.Value = a.Source.Owner == owner);
            }
            else
            {
                _coreData.Root.AudioInputs.Values.Select(s => _coreData.GetLocal<LocalAudioInputModel>(s)).ToList().ForEach(a => a.SameOwnerAsVideo.Value = true);
            }
        }

        private void OnLocalDeviceManagerPreviewAvailable(IVideoSource videoDevice, VideoInputPreview preview)
        {
            RunOnMainThread(() =>
            {
                if (_coreData.Root.VideoInputs.TryGetValue(GetGlobalInputId(videoDevice.Id), out var videoInput))
                {
                    if (_remotePreviewRequested)
                        videoInput.Preview = preview;

                    var local = _coreData.GetLocal<LocalVideoInputModel>(videoInput);
                    local.Preview.Value = preview?.Data;
                }
            });
        }

        private void OnLocalDeviceManagerAudioChanged(IAudioSource audioSource)
        {
            RunOnMainThread(() => AddOrUpdateAudio(audioSource));
        }

        private void AddOrUpdateAudio(IAudioSource audioSource)
        {
            string id = GetGlobalInputId(audioSource.Id);
            if (!_coreData.Root.AudioInputs.TryGetValue(id, out var videoInput))
            {
                videoInput = _coreData.Create<IAudioInput>();
                videoInput.Name = audioSource.Name;
                videoInput.State = audioSource.State;
                videoInput.Type = audioSource.Type;
                videoInput.Owner = _coreData.ThisDeviceId;
                _coreData.Root.AudioInputs[id] = videoInput;
            }
            else
            {
                if (videoInput.State != audioSource.State)
                    videoInput.State = audioSource.State;
            }
        }

        private void OnLocalDeviceManagerVideoChanged(IVideoSource videoDevice)
        {
            RunOnMainThread(() => AddOrUpdateLocalVideoDevice(videoDevice));
        }

        public void RunOnMainThread(Action action)
        {
            _syncContext.Post(s => action(), null);
        }

        private void AddOrUpdateLocalVideoDevice(IVideoSource videoDevice)
        {
            string id = GetGlobalInputId(videoDevice.Id);
            if (!_coreData.Root.VideoInputs.TryGetValue(id, out var videoInput))
            {
                videoInput = _coreData.Create<IVideoInput>();
                videoInput.Name = videoDevice.Name;
                videoInput.State = videoDevice.State;
                videoInput.Type = videoDevice.Type;
                videoInput.Owner = _coreData.ThisDeviceId;
                videoInput.Capabilities = new VideoInputCapabilities { Caps = videoDevice.Capabilities };
                videoInput.Filters = new VideoFilters();
                _coreData.Root.VideoInputs[id] = videoInput;
            }
            else
            {
                if (videoDevice.Capabilities != null)
                    if (videoInput.Capabilities?.Caps == null || 
                        !StructuralComparisons.StructuralEqualityComparer.Equals(videoDevice.Capabilities, videoInput.Capabilities.Caps))
                    videoInput.Capabilities = new VideoInputCapabilities { Caps = videoDevice.Capabilities };

                if (videoInput.State != videoDevice.State) 
                    videoInput.State = videoDevice.State;
            }
        }

        private string GetMessageForAudioVideoState(InputState state, string name)
        {
            switch (state)
            {
                case InputState.Unknown: return $"Failed to get state of '{name}'";
                case InputState.RemoteDeviceOffline: return $"Owning device/computer is offline";
                case InputState.RemoteDeviceInactive: return $"Owning device/computer is inactive";
                case InputState.InUseByOtherApp: return $"'{name}' is in use by another application";
                case InputState.Failed: return $"'{name}' failed or doesn't respond";
                case InputState.ObsIsNotStarted: return $"'{name}' is not started";
                default: return $"Unknown issue with '{name}'";
            }
        }

        private string GetGlobalInputId(string localId) => _coreData.ThisDeviceId + " " + localId;

        private string GetLocalSourceId(string globalId)
        {
            if (!globalId.StartsWith(_coreData.ThisDeviceId))
                throw new InvalidOperationException($"Remote id {globalId} provided");

            return globalId.Substring(_coreData.ThisDeviceId.Length + 1);
        }


    }
}
