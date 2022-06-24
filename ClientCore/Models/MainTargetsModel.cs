using Clutch.DeltaModel;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Models.Chats;
using Streamster.ClientCore.Services;
using Streamster.ClientCore.Support;
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
        private readonly StreamingSourcesModel _streamingSourcesModel;
        private readonly MainVpnModel _mainVpnModel;

        public TargetFilterModel[] Filters { get; private set; }

        public ITarget CustomTarget { get; private set; }

        public ObservableCollection<TargetModel> Targets { get; } = new ObservableCollection<TargetModel>();

        public CoreData CoreData { get; }

        public AppData AppData { get; }

        public TranscodingModel Transcoding { get; }

        public PlatformsModel PlatformsModel { get; }

        public Property<object> Popup { get; } = new Property<object>();

        public Action AddTarget { get; }

        public ObservableCollection<IChannel> VisibleChannels { get; } = new ObservableCollection<IChannel>();

        public MainTargetsModel(StaticFilesCacheService staticFilesCacheService, CoreData coreData, ConnectionService connectionService, 
            IAppResources resources,
            TranscodingModel transcoding, PlatformsModel platformsModel, StreamingSourcesModel streamingSourcesModel, MainVpnModel mainVpnModel)
        {
            _staticFilesCacheService = staticFilesCacheService;
            CoreData = coreData;
            _connectionService = connectionService;
            _resources = resources;
            
            AppData = resources.AppData;
            Transcoding = transcoding;
            PlatformsModel = platformsModel;
            _streamingSourcesModel = streamingSourcesModel;
            _mainVpnModel = mainVpnModel;
            CustomTarget = CoreData.Create<ITarget>(s =>
            {
                s.Flags = TargetFlags.Key | TargetFlags.Url;
                s.Name = "Custom";
                s.WebUrl = "";
            });

            AddTarget = () => { Popup.Value = new TargetSelectPopup { Content = this }; };
        }

        private void RefreshAllChannels() => CoreData.Root.Channels.Values.ToList().ForEach(s => RefreshChannelState(s));

        internal void Start()
        {
            if (CoreData.Root.Settings.ResetKeys)
            {
                int onlineDevices = CoreData.Root.Devices.Values.Count(s => s.State == DeviceState.Online);
                if (onlineDevices <= 1)
                {
                    CoreData.Root.Channels.Values.ToList().ForEach(s => s.Key = "");
                    Log.Information("Resetting all the keys");
                }
                else
                    Log.Information($"Cannot reset keys as number of online devices = {onlineDevices} > 1");
            }

            Transcoding.Start();

            _initialTargets = CoreData.Root.Targets.Values.OrderBy(s => s.Name).ToArray();

            Targets.Add(new TargetModel { Source = CustomTarget, OnSelected = () => CreateChannelFromTarget(null), Tooltip = "Custom target where you can set any rtmp Url" });

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
            UpdateVisibleChannels();
            RefreshAllChannels();

            CoreData.Subscriptions.SubscribeForAnyProperty<IChannel>((s, c, p, v) => { RefreshChannelState(s); UpdateVisibleChannels(); });
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.StreamingToCloudStarted, (a, b, c) => RefreshAllChannels());
            CoreData.Subscriptions.SubscribeForAnyProperty<ISettings>((a, b, c, d) => RefreshAllChannels());
        }

        private void UpdateVisibleChannels()
        {
            ListHelper.UpdateCollection(CoreData, CoreData.Root.Channels.Values.Where(s => !s.Temporary).ToList(), VisibleChannels, t => CoreData.GetId(t), (a, b) => a);
        }

        public void StartChannel(ChannelModel channelModel)
        {
            var status = channelModel.Status.Value;
            if (CoreData.Root.Channels.Count(s => s.Value.IsOn) >= _connectionService.Claims.MaxChannels)
            {
                channelModel.StartError.Value = $"Your plan does not allow more than {_connectionService.Claims.MaxChannels} channels";
                ClearStartError(channelModel);
            }
            else if (status.State == ChannelModelState.IdleError || status.State == ChannelModelState.IdleLoginError)
            {
                channelModel.StartError.Value = $"Restreaming not possible: {status.TextState}";
                ClearStartError(channelModel);
            }
            else if (status.State == ChannelModelState.Idle)
            {
                if (channelModel.Source.TargetMode == TargetMode.AutoLogin)
                    channelModel.Source.AutoLoginState = AutoLoginState.Unknown; // enforce update state

                channelModel.Source.IsOn = true;
            }
            else
            {
                channelModel.StartError.Value = $"Bad state";
                ClearStartError(channelModel);
            }
        }

        private void ClearStartError(ChannelModel channelModel) => TaskHelper.RunUnawaited(async () =>
        {
            await Task.Delay(5000);
            channelModel.StartError.Value = "";
        }, "");

        private void RefreshChannelState(IChannel s)
        {
            var model = CoreData.GetLocal<ChannelModel>(s);

            if (model == null)
                return;

            model.AutoLoginMode.SilentAndCompared = model.SupportsAutoLogin && s.TargetMode == TargetMode.AutoLogin;
            model.Name.SilentAndCompared = s.Name == null ? model.Target.Name : s.Name; 
            model.WebUrl.SilentAndCompared = s.WebUrl == null ? model.Target.WebUrl : s.WebUrl;
            model.RtmpKey.SilentAndCompared = s.Key == null ? "" : s.Key;
            model.RtmpUrl.SilentAndCompared = s.RtmpUrl == null ? (model.Target.DefaultRtmpUrl ?? "") : s.RtmpUrl;
            model.RtmpUrlHasWrongFormat.SilentAndCompared = !IsOkRtmpFormat(model.RtmpUrl.Value);
            model.IsTranscoded.SilentAndCompared = Transcoding.IsTranscoded(s);

            model.Status.Value = GetStatus(s, model);
        }

        private ChannelModelStatus GetStatus(IChannel ch, ChannelModel model)
        {
            if (!ch.IsOn) // OFF
            {
                if (ch.State != ChannelState.Idle)
                    return new ChannelModelStatus(ChannelModelState.InProgress, "Disconnecting...");

                if (model.AutoLoginMode.Value)
                {
                    var als = "?";
                    var state = ChannelModelState.Idle;
                    switch (ch.AutoLoginState)
                    {
                        case AutoLoginState.Unknown:
                            als = "State is unknown";
                            break;
                        case AutoLoginState.InProgress:
                            als = "Authenticating...";
                            break;
                        case AutoLoginState.Authenticated:
                            als = "Getting config...";
                            break;
                        case AutoLoginState.NotAuthenticated:
                            als = "Not authenticated";
                            state = ChannelModelState.IdleLoginError;
                            break;
                        case AutoLoginState.KeyObtained:
                            als = "Config obtained";
                            break;
                        case AutoLoginState.KeyNotFound:
                            als = "Config not obtained";
                            state = ChannelModelState.IdleError;
                            break;
                    }
                    
                    var status = new ChannelModelStatus(state, ch.AutoLoginState == AutoLoginState.KeyObtained ? "" : als);
                    status.AutoLoginStateText = als;
                    status.AutoLoginState = ch.AutoLoginState;
                    return status;
                }
                else
                {
                    if (string.IsNullOrEmpty(model.RtmpUrl.Value))
                        return new ChannelModelStatus(ChannelModelState.IdleError, "Rtmp url is not set");
                    if (model.RtmpUrlHasWrongFormat.Value)
                        return new ChannelModelStatus(ChannelModelState.IdleError, "Wrong Rtmp url format");
                    if (string.IsNullOrEmpty(model.Source.Key))
                        return new ChannelModelStatus(ChannelModelState.IdleError, "Stream key is not set");
                }

                if (model.IsTranscoded.Value) 
                {
                    if (Transcoding.Message.Value == TranscodingMessageType.HighInputFps ||
                        Transcoding.Message.Value == TranscodingMessageType.HighInputResolution)
                    {
                        return new ChannelModelStatus(ChannelModelState.IdleError, "Bad transcoding config");
                    }
                }

                return new ChannelModelStatus(ChannelModelState.Idle, "");
            }
            else // Is on
            {
                if (!_streamingSourcesModel.IsSomeOneStreaming())
                    return new ChannelModelStatus(ChannelModelState.RunningWait, "Waiting for stream to cloud");

                switch (ch.State)
                {
                    case ChannelState.Idle:
                        return new ChannelModelStatus(ChannelModelState.InProgress, "Connecting...");
                    case ChannelState.RunningOk:
                        return new ChannelModelStatus(ChannelModelState.RunningOk, "Connected", ch.Bitrate, ch.Timer);
                    case ChannelState.RunningConnectError:
                        return new ChannelModelStatus(ChannelModelState.RunningError, "Failed. Check your key", ch.Bitrate, ch.Timer);
                    case ChannelState.RunningInitError:
                        return new ChannelModelStatus(ChannelModelState.RunningError, "Unknown error", ch.Bitrate, ch.Timer);
                    default:
                        return new ChannelModelStatus(ChannelModelState.RunningError, "Unknown failure", ch.Bitrate, ch.Timer);
                }
            }
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
                        n.OnSelected = () => CreateChannelFromTarget(n);
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

        private void CreateChannelFromTarget(TargetModel model)
        {
            CleanTemporary();

            var id = IdGenerator.New();
            var channel = CoreData.Create<IChannel>(s => s.TargetId = model?.Source?.Id);
            channel.Temporary = true;
            CoreData.Root.Channels[id] = channel;

            var local = CoreData.GetLocal<ChannelModel>(channel);
            var title = model?.Source?.Name ?? "Custom channel";
            Popup.Value = new TargetConfig
            {
                Add = true,
                ChannelModel = local,
                Ok = () => { channel.Temporary = false; },
                Cancel = () => { CleanTemporary(); }
            };
        }

        private void CleanTemporary()
        {
            var toRemove = CoreData.Root.Channels.Where(c => c.Value.Temporary).ToList();
            toRemove.ForEach(s => CoreData.Root.Channels.Remove(s.Key));
        }

        internal void Remove(ChannelModel channelModel)
        {
            CoreData.Root.Channels.Remove(CoreData.GetId(channelModel.Source));
        }

        private bool IsOkRtmpFormat(string rtmpUrl)
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

    public class TargetConfig : ICloseAware
    {
        public bool Add { get; set; }

        public ChannelModel ChannelModel { get; set; }

        public Action Cancel { get; set; }

        public Action Ok { get; set; }

        public void Close() => Cancel();
    }

    public class TargetSelectPopup
    {
        public object Content { get; set; }
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

            var targetId = source.TargetId;

            if (targetId != null)
            {
                Target = coreData.Root.Targets[targetId];
                NeedsRtmpUrl = (Target.Flags & TargetFlags.Url) > 0;
                var platformInfo = coreData.Root.Platforms.PlatformInfos.FirstOrDefault(s => s.TargetId == targetId);
                if (platformInfo != null)
                    SupportsAutoLogin = ((platformInfo.Flags & PlatformInfoFlags.GetKey) > 0) && ClientConstants.ChatsEnabled;
            }
            else
            {
                Target = parent.CustomTarget;
                NeedsRtmpUrl = true;
            }

            Delete = () => parent.Remove(this);
            Start = () => Parent.StartChannel(this);
            Stop = () => Source.IsOn = false;
            GoToHelp = () => environment.OpenUrl(string.Format(parent.AppData.TargetHintTemplate, source.TargetId ?? "Custom"));
            GoToWebUrl = () => environment.OpenUrl(WebUrl.Value);
            Authenticate = () => parent.PlatformsModel.Authenticate(targetId);

            ShowSettings = () => parent.Popup.Value = new TargetConfig
            {
                Add = false,
                ChannelModel = this,
                Ok = () => { },
                Cancel = () => { }
            };

            WebUrl.OnChange = (o, n) => Source.WebUrl = n == Target.WebUrl ? null : n;
            Name.OnChange = (o, n) => Source.Name = n == Target.Name ? null : n;
            RtmpUrl.OnChange = (o, n) => Source.RtmpUrl = n == Target.DefaultRtmpUrl ? null : n;
            RtmpKey.OnChange = (o, n) => Source.Key = n == "" ? null : n;
            AutoLoginMode.OnChange = (o, n) => Source.TargetMode = n ? TargetMode.AutoLogin : TargetMode.ManualKey;
            IsTranscoded.OnChange = (o, n) => Parent.Transcoding.SetTranscoding(Source, n);

            TaskHelper.RunUnawaited(() => parent.GetImageAsync(Logo, source.TargetId), "Get image for channel");
        }

        public ITarget Target { get; }

        public IChannel Source { get; }

        public MainTargetsModel Parent { get; }

        public bool NeedsRtmpUrl { get; }

        public bool SupportsAutoLogin { get; }

        public Property<byte[]> Logo { get; } = new Property<byte[]>();


        public Property<bool> IsTranscoded { get; } = new Property<bool>();

        public Property<string> WebUrl { get; } = new Property<string>();

        public Property<string> RtmpUrl { get; } = new Property<string>();

        public Property<bool> RtmpUrlHasWrongFormat { get; } = new Property<bool>();

        public Property<string> Name { get; } = new Property<string>();

        public Property<string> RtmpKey { get; } = new Property<string>();

        public Property<bool> AutoLoginMode { get; } = new Property<bool>();


        public Property<ChannelModelStatus> Status { get; } = new Property<ChannelModelStatus>();

        public Property<string> StartError { get; } = new Property<string>("");

        public Action Start { get; }

        public Action Stop { get; }

        public Action Delete { get; }

        public Action GoToHelp { get; }

        public Action GoToWebUrl { get; }

        public Action ShowSettings { get; }

        public Action Authenticate { get; }
    }

    public class ChannelModelStatus
    {
        public ChannelModelState State { get; set; }

        public string TextState { get; set; } 

        public string TimerState { get; set; } 

        public string Bitrate { get; set; }

        public AutoLoginState AutoLoginState { get; set; }

        public string AutoLoginStateText { get; set; }

        public ChannelModelStatus(ChannelModelState state, string textState)
        {
            State = state;
            TextState = textState;
        }

        public ChannelModelStatus(ChannelModelState state, string textState, int bitrate, string timer) : this(state, textState)
        {
            Bitrate = $"{bitrate} Kb/s";
            TimerState = timer;
        }
    }

    public enum ChannelModelState
    {
        Idle,
        IdleError,
        IdleLoginError,

        InProgress,

        RunningOk,
        RunningWait,
        RunningError
    }
}
