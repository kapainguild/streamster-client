using DynamicStreamer.Queues;
using Serilog;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Streamster.ClientCore.Models
{
    public class AudioModel
    {
        public CoreData CoreData { get; set; }

        private SceneState _sceneState;
        private readonly StreamingSourcesModel _streamingSourcesModel;

        public ObservableCollection<AudioSourceModel> AudioSources { get; } = new ObservableCollection<AudioSourceModel>();

        public Property<bool> SourceSelectionOpened { get; } = new Property<bool>();

        public AudioItemModel Mic { get; private set; } 

        public AudioItemModel Desktop { get; private set; } 

        public AudioModel(CoreData coreData, StreamingSourcesModel streamingSourcesModel)
        {
            CoreData = coreData;
            _streamingSourcesModel = streamingSourcesModel;
            Mic = new AudioItemModel(false, this);
            Desktop = new AudioItemModel(true, this);
        }

        public void Start()
        {
            SourceSelectionOpened.OnChange = (o, n) => CoreData.ThisDevice.PreviewAudioSources = n;
            
            Mic.Muted.OnChange = (o, n) => GetSceneAudio(false).Muted = n;
            Mic.VolumeControl.OnChange = (o, n) => GetSceneAudio(false).Volume = GetDbFromPercent(n);
            
            Desktop.Name.Value = "Desktop Audio";
            Desktop.Muted.OnChange = (o, n) => GetSceneAudio(true).Muted = n;
            Desktop.VolumeControl.OnChange = (o, n) => GetSceneAudio(true).Volume = GetDbFromPercent(n);

            CoreData.Subscriptions.SubscribeForAnyProperty<IInputDevice>((i, c, e, r) => UpdateAudioSources());
            CoreData.Subscriptions.SubscribeForAnyProperty<ISceneAudio>((a, b, c, d) => RefreshAll());

            CoreData.Subscriptions.SubscribeForProperties<IScene>(s => s.AudioIssues, (i, c, e) => RefreshItems());
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.SelectedScene, (i, c, e) => RefreshAll());

            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshItems();
            UpdateAudioSources();
        }

        private double GetDbFromPercent(double value) // 0..0.8..1 => -40..0..6
        {
            if (value >= 1.0)
                return 6; // 6 bB - double sound

            if (value <= 0.0)
                return -1000; // Mute

            if (value > 0.8)
                return (value - 0.8) * 30; // 0 .. 6
            else
                return (value - 0.8) * 50; // 0 .. -40
        }

        private double GetPercentFromDb(double value) // -40..0..6 => 0..0.8..1
        {
            if (value >= 6.0)
                return 1; // 6 bB - double sound

            if (value <= -40.0)
                return 0; // Mute

            if (value > 0)
                return 0.8 + value / 30.0; // 0 .. 6
            else
                return 0.8 + value / 50.0; // 0 .. -40
        }

        private void UpdateAudioSources()
        {
            var selectedId = GetSceneAudio(false)?.Source?.DeviceName?.DeviceId;

            ListHelper.UpdateCollection(CoreData,
                _sceneState.Device.AudioInputs.Values.OrderBy(s => s.Name).ToList(),
                AudioSources,
                s => s.Id,
                (s, id) => new AudioSourceModel(id,
                                s,
                                () => Select(id), 
                                new Property<bool>()));

            AudioSources.ToList().ForEach(s => s.IsSelected.Value = s.Id == selectedId);
        }

        private ISceneAudio GetSceneAudio(bool desktop)
        {
            var id = desktop ? SceneAudioConsts.DesktopAudioId : SceneAudioConsts.MicrophoneId;
            _sceneState.Scene.Audios.TryGetValue(id, out var result);
            return result;
        }

        private void Select(string id)
        {
            _sceneState.Device.AudioInputs.TryGetValue(id, out var audioDevice);
            var audio = GetSceneAudio(false);
            if (audio != null && audioDevice != null)
                audio.Source = new SceneAudioSource { DeviceName = new DeviceName { DeviceId = id, Name = audioDevice.Name } };
            else
                Log.Warning($"Wrong audio selection {audio}, {audioDevice}");
        }

        private void RefreshItems()
        {
            if (_streamingSourcesModel.TryGetCurrentSceneDevice(out var scene, out var device))
            {
                _sceneState = new SceneState(scene, device, device == CoreData.ThisDevice);

                if (device.Type == ClientConstants.ExternalClientId)
                {
                    Desktop.Visible.ValueWithComparison = false;
                    Mic.Visible.ValueWithComparison = false;
                }
                else
                {
                    Desktop.Visible.ValueWithComparison = true;
                    Mic.Visible.ValueWithComparison = true;

                    var desktop = GetSceneAudio(true);
                    if (desktop != null)
                    {
                        Desktop.Muted.SilentValue = desktop.Muted;
                        Desktop.VolumeControl.SilentValue = GetPercentFromDb(desktop.Volume);
                    }
                    Desktop.VolumeLevelAvailable.Value = _sceneState.IsLocal;

                    var mic = GetSceneAudio(false);
                    if (mic != null)
                    {
                        Mic.Muted.SilentValue = mic.Muted;
                        Mic.VolumeControl.SilentValue = GetPercentFromDb(mic.Volume);
                    }
                    Mic.VolumeLevelAvailable.Value = _sceneState.IsLocal;

                    if (mic == null)
                        Mic.Name.SilentValue = "[Microphone not available]";
                    else if (mic.Source.DeviceName?.DeviceId == null)
                        Mic.Name.SilentValue = "[Microphone is not selected]";
                    else if (_sceneState.Device.AudioInputs.TryGetValue(mic.Source.DeviceName.DeviceId, out var input))
                        Mic.Name.SilentValue = input.Name;
                    else
                        Mic.Name.SilentValue = "[Microphone not found]";

                    RefreshIssues(scene);
                }
            }
        }

        private void RefreshIssues(IScene scene)
        {
            var audioIssues = scene.AudioIssues;

            var desktopIssue = audioIssues?.FirstOrDefault(s => s.Id == SceneAudioConsts.DesktopAudioId);
            Desktop.InputIssue.Value = !Desktop.Muted.Value && desktopIssue != null ? SceneEditingModel.GetIssueString(desktopIssue.Desc) : null;
            Desktop.HasInputIssue.Value = Desktop.InputIssue.Value != null;

            var micIssue = audioIssues?.FirstOrDefault(s => s.Id == SceneAudioConsts.MicrophoneId);
            Mic.InputIssue.Value = !Mic.Muted.Value && micIssue != null ? SceneEditingModel.GetIssueString(micIssue.Desc) : null;
            Mic.HasInputIssue.Value = Mic.InputIssue.Value != null;
        }

        internal void OnAudioFrame(string id, FrameOutputData data)
        {
            if (id != null) // not mixer
            {
                if (id == SceneAudioConsts.DesktopAudioId)
                    Desktop.VolumeLevel.OnAudioFrame(data);
                else if (id == SceneAudioConsts.MicrophoneId)
                    Mic.VolumeLevel.OnAudioFrame(data);
            }
        }
    }

    public record AudioSourceModel(string Id, IInputDevice Model, Action Select, Property<bool> IsSelected);

    public class AudioItemModel
    {
        public AudioItemModel(bool v, AudioModel parent)
        {
            IsDesktop = v;
            Parent = parent;
        }

        public bool IsDesktop { get; }

        public Property<bool> Visible { get; } = new Property<bool>();

        public AudioModel Parent { get; }

        public Property<bool> Muted { get; } = new Property<bool>();

        public Property<double> VolumeControl { get; } = new Property<double>();

        public AudioLevelModel VolumeLevel { get; } = new AudioLevelModel();

        public Property<bool> VolumeLevelAvailable { get; } = new Property<bool>();

        public Property<string> Name { get; } = new Property<string>();

        public Property<string> InputIssue { get; } = new Property<string>();

        public Property<bool> HasInputIssue { get; } = new Property<bool>();
    }

    public enum SoundVolumeState { Ok, Hi, VeryHi }
}
