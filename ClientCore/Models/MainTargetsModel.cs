using Clutch.DeltaModel;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class MainTargetsModel
    {
        private ITarget[] _initialTargets;
        private readonly StaticFilesCacheService _staticFilesCacheService;
        private readonly ConnectionService _connectionService;
        private readonly IAppResources _resources;

        public TargetFilterModel[] Filters { get; private set; }

        public ITarget CustomTarget { get; private set; }

        public ObservableCollection<TargetModel> Targets { get; } = new ObservableCollection<TargetModel>();

        public CoreData CoreData { get; }

        public AppData AppData { get; }

        public TranscodingModel Transcoding { get; }

        public MainTargetsModel(StaticFilesCacheService staticFilesCacheService, CoreData coreData, ConnectionService connectionService, IAppResources resources,
            TranscodingModel transcoding)
        {
            _staticFilesCacheService = staticFilesCacheService;
            CoreData = coreData;
            _connectionService = connectionService;
            _resources = resources;
            
            AppData = resources.AppData;
            Transcoding = transcoding;

            CustomTarget = CoreData.Create<ITarget>(s =>
            {
                s.Flags = TargetFlags.Key | TargetFlags.Url;
                s.Name = "Custom";
                s.WebUrl = "";
            });
        }

        private void RefreshAllChannels() => CoreData.Root.Channels.Values.ToList().ForEach(s => RefreshChannelState(s));

        internal void Start()
        {
            Transcoding.Start();

            _initialTargets = CoreData.Root.Targets.Values.OrderBy(s => s.Name).ToArray();

            Targets.Add(new TargetModel { Source = CustomTarget, OnSelected = () => DoSelected(null), Tooltip = "Custom target where you can set any rtmp Url" });

            Filters = new TargetFilterModel[]
            {
                new TargetFilterModel { Name = "Adult", IsSelected = new Property<bool>(true), Flags = TargetFlags.Adult },
                new TargetFilterModel { Name = "Education", IsSelected = new Property<bool>(true), Flags = TargetFlags.Education },
                new TargetFilterModel { Name = "Gaming", IsSelected = new Property<bool>(true), Flags = TargetFlags.Gaming },
                new TargetFilterModel { Name = "Music", IsSelected = new Property<bool>(true), Flags = TargetFlags.Music },
                new TargetFilterModel { Name = "Religion", IsSelected = new Property<bool>(true), Flags = TargetFlags.Religion },
                new TargetFilterModel { Name = "Vlog", IsSelected = new Property<bool>(true), Flags = TargetFlags.Vlog },
            };

            var filter = CoreData.Settings?.TargetFilter ?? 0;

            if (CoreData.Settings == null)
                Log.Error("CoreData.Settings is null");

            Filters.ToList().ForEach(s =>
            {
                s.IsSelected.Value = (filter & (int)s.Flags) == 0;
                s.IsSelected.OnChange = (o, n) => UpdateFilter();
            });

            UpdateFilter();
            RefreshAllChannels();

            CoreData.Subscriptions.SubscribeForAnyProperty<IChannel>((s, c, p, v) => RefreshChannelState(s));
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.StreamingToCloudStarted, (a, b, c) => RefreshAllChannels());
            CoreData.Subscriptions.SubscribeForAnyProperty<ISettings>((a, b, c, d) => RefreshAllChannels());
        }

        private void RefreshChannelState(IChannel s)
        {
            var model = CoreData.GetLocal<ChannelModel>(s);

            if (model == null)
                return;

            if (s.IsOn && !CoreData.Settings.StreamingToCloudStarted)
            {
                model.State.Value = ChannelModelState.WaitingForStreamToCloud;
                model.Bitrate.Value = "";
                model.TextState.Value = "Waiting for a stream to cloud...";
            }
            else if ((s.IsOn == false) != (s.State == ChannelState.Idle))
            {
                model.State.Value = ChannelModelState.InProgress;
                model.TextState.Value = s.IsOn ? "Connecting..." : "Disconnecting...";
                model.TimerState.Value = null;
            }
            else
            {
                switch (s.State)
                {
                    case ChannelState.Idle:
                        model.State.Value = ChannelModelState.Idle;
                        model.TextState.Value = "";
                        model.Bitrate.Value = "";
                        break;
                    case ChannelState.RunningOk:
                        model.State.Value = ChannelModelState.RunningOk;
                        model.TextState.Value = "Connected";
                        model.Bitrate.Value = $"{s.Bitrate} Kb/s";
                        break;
                    case ChannelState.RunningConnectError:
                        model.State.Value = ChannelModelState.RunningConnectError;
                        model.TextState.Value = "Failed. Check your stream Key";
                        model.Bitrate.Value = $"{s.Bitrate} Kb/s";
                        break;
                    case ChannelState.RunningInitError:
                        model.State.Value = ChannelModelState.RunningInitError;
                        model.TextState.Value = "Failed. Url or Key is not set";
                        model.Bitrate.Value = $"{s.Bitrate} Kb/s";
                        break;
                }

                model.TimerState.Value = s.Timer;
            }

            string name = s.Name == null ? model.Target.Name : s.Name;
            model.Name.SilentValue = name;

            string webUrl = s.WebUrl == null ? model.Target.WebUrl : s.WebUrl;
            model.WebUrl.SilentValue = webUrl;

            string rtmpUrl = s.RtmpUrl == null ? model.Target.DefaultRtmpUrl : s.RtmpUrl;
            model.RtmpUrl.SilentValue = rtmpUrl;

            model.IsTranscoded.SilentValue = Transcoding.IsTranscoded(s);
        }

        private void UpdateFilter()
        {
            TargetFlags filter = 0;
            TargetFlags excluded = 0;
            Filters.Where(s => s.IsSelected.Value).ToList().ForEach(s => filter |= s.Flags);
            Filters.Where(s => !s.IsSelected.Value).ToList().ForEach(s => excluded |= s.Flags);

            if (CoreData.Settings.TargetFilter != (int)excluded)
                CoreData.Settings.TargetFilter = (int)excluded;

            int index = 1; // custom is always on top
            foreach (var source in _initialTargets)
            {
                bool add = (source.Flags & filter) > 0;
                if (AppData.HideTargetFilter)
                    add = _resources.TargetFilter(source);
                var exists = Targets.FirstOrDefault(s => s.Source == source);
                if (add)
                {

                    if (exists == null)
                    {
                        var n = new TargetModel { Source = source };
                        if ((source.Flags & TargetFlags.Adult) > 0)
                            n.Tooltip = source.Hint + " All performers must be over 18 years old.";
                        else
                            n.Tooltip = source.Hint;
                        _ = GetImageAsync(n.Logo, source.Id);
                        n.OnSelected = () => DoSelected(n);
                        Targets.Insert(index, n);
                    }
                    index++;
                }
                else
                {
                    if (exists != null)
                        Targets.Remove(exists);
                }
            }
        }

        internal async Task GetImageAsync(Property<byte[]> logo, string targetId)
        {
            if (targetId != null)
            {
                string url = $"{ClientConstants.LoadBalancerFiles_Targets}/{targetId}.png";
                logo.Value = await _staticFilesCacheService.GetFileAsync(url);
            }
        }

        private void DoSelected(TargetModel model)
        {
            var channel = CoreData.Create<IChannel>(s => s.TargetId = model?.Source?.Id);
            CoreData.Root.Channels[IdGenerator.New()] = channel;
        }

        internal void Remove(ChannelModel channelModel)
        {
            CoreData.Root.Channels.Remove(CoreData.GetId(channelModel.Source));
        }

        internal bool IsStartPossible(ChannelModel channelModel, out string error)
        {
            error = null;
            if (channelModel.HasRtmpUrlInOptions && string.IsNullOrEmpty(channelModel.RtmpUrl.Value))
                error = "Rtmp url is not set";
            else if (channelModel.HasRtmpUrlInOptions && !IsOk(channelModel.RtmpUrl.Value))
                error = "Rtmp url is wrongly formatted";
            else if (string.IsNullOrEmpty(channelModel.Source.Key))
                error = "Stream key is not set";
            else if (CoreData.Root.Channels.Count(s => s.Value.IsOn) >= _connectionService.Claims.MaxChannels)
                error = $"Your plan does not allow more than {_connectionService.Claims.MaxChannels} channels";

            return string.IsNullOrEmpty(error);
        }

        private bool IsOk(string rtmpUrl)
        {
            if (Uri.TryCreate(rtmpUrl, UriKind.Absolute, out var uri))
            {
                var scheme = uri.Scheme.ToLower();
                if (scheme == "rtmp" || scheme == "rtmps")
                    return true;
            }
            return false;
        }

    }

    public class TargetFilterModel
    {
        public string Name { get; set; }

        public TargetFlags Flags { get; set; }

        public Property<bool> IsSelected { get; set; }
    }

    public class TargetModel
    {
        public ITarget Source { get; set; }

        public Property<byte[]> Logo { get; } = new Property<byte[]>();

        public string Tooltip { get; set; }

        public Action OnSelected { get; set; }
    }

    public class ChannelModel
    {
        public ChannelModel(IChannel source, IAppEnvironment environment, CoreData coreData, MainTargetsModel parent)
        {
            Source = source;
            Parent = parent;

            if (source.TargetId != null)
            {
                Target = coreData.Root.Targets[source.TargetId];
                HasRtmpUrlInFront = (Target.Flags & TargetFlags.Url) > 0;
            }
            else
            {
                Target = parent.CustomTarget;
                HasRtmpUrlInFront = true;
            }

            HasRtmpUrlInOptions = true;// (Target.Flags & TargetFlags.Url) > 0;

            Delete = () => parent.Remove(this);
            Start = DoStart;
            Stop = () => Source.IsOn = false;
            GoToHelp = () => environment.OpenUrl(string.Format(parent.AppData.TargetHintTemplate, source.TargetId ?? "Custom"));
            GoToWebUrl = () => environment.OpenUrl(WebUrl.Value);

            WebUrl.OnChange = (o, n) => Source.WebUrl = n == Target.WebUrl ? null : n;
            Name.OnChange = (o, n) => Source.Name = n == Target.Name ? null : n;
            RtmpUrl.OnChange = (o, n) => Source.RtmpUrl = n == Target.DefaultRtmpUrl ? null : n;

            IsTranscoded.OnChange = (o, n) => Transcoding.SetTranscoding(Source, n);

            TaskHelper.RunUnawaited(() => parent.GetImageAsync(Logo, source.TargetId), "Get image for channel");
        }

        private void DoStart()
        {
            if (Parent.IsStartPossible(this, out var error))
            {
                StartError.Value = "";
                Source.IsOn = true;

                //var dev = Parent.CoreData.ThisDevice;

                //TaskHelper.RunUnawaited(async () =>
                //{
                //    dev.RequireOutgest = false;
                //    await Task.Delay(30);
                //    dev.RequireOutgest = true;

                //    await Task.Delay(30);
                //    dev.RequireOutgest = false;
                //    await Task.Delay(30);
                //    dev.RequireOutgest = true;
                //}, "");
            }
            else
            {
                StartError.Value = error;
                TaskHelper.RunUnawaited(async () =>
                {
                    await Task.Delay(5000);
                    StartError.Value = "";
                }, "");
            }
        }

        public TranscodingModel Transcoding => Parent.Transcoding.SetCurrent(this);

        public bool TranscodingEnabled => Parent.Transcoding.TranscodingEnabled;

        public Property<bool> IsTranscoded { get; } = new Property<bool>();

        public ITarget Target { get; }

        public IChannel Source { get; }

        public MainTargetsModel Parent { get; }

        public bool HasRtmpUrlInFront { get; }

        public bool HasRtmpUrlInOptions { get; }

        public Property<string> StartError { get; } = new Property<string>("");

        public Property<byte[]> Logo { get; } = new Property<byte[]>();

        public Property<ChannelModelState> State { get; } = new Property<ChannelModelState>();

        public Property<string> TextState { get; } = new Property<string>();

        public Property<string> TimerState { get; } = new Property<string>();

        public Property<string> WebUrl { get; } = new Property<string>();

        public Property<string> RtmpUrl { get; } = new Property<string>();

        public Property<string> Name { get; } = new Property<string>();

        public Property<string> Bitrate { get; } = new Property<string>();

        public Action Start { get; }

        public Action Stop { get; }

        public Action Delete { get; }

        public Action GoToHelp { get; }

        public Action GoToWebUrl { get; }

        public Action ShowTranscoding { get; }
    }

    public enum ChannelModelState
    {
        Idle,
        InProgress,
        RunningOk,
        RunningConnectError,
        RunningInitError,
        WaitingForStreamToCloud
    }
}
