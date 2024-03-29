﻿using DeltaModel;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Models.Chats;
using Streamster.ClientCore.Services;
using Streamster.ClientCore.Support;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class MainTargetsModel
    {
        private TargetModel[] _initialTargets;
        private readonly StaticFilesCacheService _staticFilesCacheService;
        private readonly ConnectionService _connectionService;
        private readonly IAppResources _resources;
        private readonly StreamingSourcesModel _streamingSourcesModel;
        private readonly IAppEnvironment _appEnvironment;
        private ChannelModel _channelBeingCreated;

        public TargetFilterModel[] Filters { get; private set; }

        public ITarget CustomTarget { get; private set; }

        public Property<TargetModel[]> Targets { get; } = new Property<TargetModel[]>();

        public Property<TargetModel[]> RecommendedTargets { get; } = new Property<TargetModel[]>();

        public CoreData CoreData { get; }

        public AppData AppData { get; }

        public TranscodingModel Transcoding { get; }

        public PlatformsModel PlatformsModel { get; }

        public Property<object> Popup { get; } = new Property<object>();

        public Action AddTarget { get; }

        private TargetModel _custom;

        public ObservableCollection<ChannelModel> Channels { get; } = new ObservableCollection<ChannelModel>();

        public MainTargetsModel(StaticFilesCacheService staticFilesCacheService, CoreData coreData, ConnectionService connectionService, 
            IAppResources resources,
            TranscodingModel transcoding, PlatformsModel platformsModel, StreamingSourcesModel streamingSourcesModel, 
            IAppEnvironment appEnvironment)
        {
            _staticFilesCacheService = staticFilesCacheService;
            CoreData = coreData;
            _connectionService = connectionService;
            _resources = resources;
            
            AppData = resources.AppData;
            Transcoding = transcoding;
            PlatformsModel = platformsModel;
            _streamingSourcesModel = streamingSourcesModel;
            _appEnvironment = appEnvironment;
            CustomTarget = CoreData.Create<ITarget>(s =>
            {
                s.Flags = TargetFlags.Key | TargetFlags.Url;
                s.Name = "Custom";
                s.WebUrl = "";
            });

            AddTarget = () => { Popup.Value = new TargetSelectPopup { Content = this }; };
            _custom = new TargetModel { Source = CustomTarget, OnSelected = () => CreateChannelFromTarget(null), Tooltip = "Custom target where you can set any rtmp Url" };
        }

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

            EvaluatePreferance();

            Transcoding.Start();

            _initialTargets = CoreData.Root.Targets.Values.OrderBy(s => s.Name).Select(s => CreateModel(s)).ToArray();

            Filters = new TargetFilterModel[]
            {
                new TargetFilterModel { Name = "Adult", IsSelected = new Property<bool>(true), Flags = TargetFlags.Adult },
                new TargetFilterModel { Name = "Education", IsSelected = new Property<bool>(true), Flags = TargetFlags.Education },
                new TargetFilterModel { Name = "Gaming", IsSelected = new Property<bool>(true), Flags = TargetFlags.Gaming },
                new TargetFilterModel { Name = "Music", IsSelected = new Property<bool>(true), Flags = TargetFlags.Music },
                new TargetFilterModel { Name = "Religion", IsSelected = new Property<bool>(true), Flags = TargetFlags.Religion },
                new TargetFilterModel { Name = "Vlog", IsSelected = new Property<bool>(true), Flags = TargetFlags.Vlog },
            };

            var filter = CoreData.Settings.TargetFilter;

            if (CoreData.Settings == null)
                Log.Error("CoreData.Settings is null");

            Filters.ToList().ForEach(s =>
            {
                s.IsSelected.Value = (filter & (int)s.Flags) == 0;
                s.IsSelected.OnChange = (o, n) => UpdateFilter();
            });

            UpdateFilter();

            RefreshChannelList();

            CoreData.Subscriptions.SubscribeForType<IChannel>((s, c) => { RefreshChannelList(); });
            CoreData.Subscriptions.SubscribeForAnyProperty<IChannel>((s, c, p, v) => 
            {
                if (_channelBeingCreated != null && _channelBeingCreated.Source == s)
                    RefreshChannelState(_channelBeingCreated);
                else 
                    RefreshChannelState(Channels.FirstOrDefault(a => a.Source == s)); 
            });
            CoreData.Subscriptions.SubscribeForAnyProperty<ISettings>((a, b, c, d) => RefreshAllChannelStates());
        }

        private void RefreshAllChannelStates()
        {
            Channels.ToList().ForEach(s => RefreshChannelState(s));
            RefreshChannelState(_channelBeingCreated);
        }

        private void RefreshChannelList()
        {
            ListHelper.UpdateCollection(CoreData, CoreData.Root.Channels.Values.ToList(), Channels, t => t.Id, (a, b) =>
            {
                var model = new ChannelModel(a, b, _appEnvironment, CoreData, this);
                RefreshChannelState(model);
                return model;
            }, s => s.Source);
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
            else if (status.State == ChannelModelState.Idle || status.State == ChannelModelState.BitrateWarning)
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

        public void RefreshChannelState(ChannelModel model)
        {
            if (model == null)
                return;

            var channel = model.Source;

            model.AutoLoginMode.SilentAndCompared = model.SupportsAutoLogin && channel.TargetMode == TargetMode.AutoLogin;
            model.Name.SilentAndCompared = channel.Name == null ? model.Target.Name : channel.Name; 
            model.WebUrl.SilentAndCompared = channel.WebUrl == null ? model.Target.WebUrl : channel.WebUrl;
            model.RtmpKey.SilentAndCompared = channel.Key == null ? "" : channel.Key;
            model.RtmpUrl.SilentAndCompared = channel.RtmpUrl == null ? (model.Target.DefaultRtmpUrl ?? "") : channel.RtmpUrl;
            model.RtmpUrlHasWrongFormat.SilentAndCompared = !IsOkRtmpFormat(model.RtmpUrl.Value);
            model.IsTranscoded.SilentAndCompared = Transcoding.IsTranscoded(channel);

            model.Status.Value = GetStatus(channel, model);
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

                    if (ch.AutoLoginState == AutoLoginState.KeyObtained)
                    {
                        var issue = GetTranscodingAndBitrateIssues(model, ch);
                        if (issue != null)
                        {
                            issue.AutoLoginStateText = als;
                            issue.AutoLoginState = ch.AutoLoginState;
                            return issue;
                        }
                    }

                    return new ChannelModelStatus(state, ch.AutoLoginState == AutoLoginState.KeyObtained ? "" : als)
                    {
                        AutoLoginStateText = als,
                        AutoLoginState = ch.AutoLoginState,
                    };
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

                

                return GetTranscodingAndBitrateIssues(model, ch) ?? new ChannelModelStatus(ChannelModelState.Idle, "");
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

        private ChannelModelStatus GetTranscodingAndBitrateIssues(ChannelModel model, IChannel ch)
        {
            if (model.IsTranscoded.Value)
            {
                if (Transcoding.Message.Value == TranscodingMessageType.HighInputFps ||
                    Transcoding.Message.Value == TranscodingMessageType.HighInputResolution)
                {
                    return new ChannelModelStatus(ChannelModelState.IdleError, "Bad transcoding config");
                }
            }

            if (ch.BitrateWarning != null)
            {
                return new ChannelModelStatus(ChannelModelState.BitrateWarning, "Stream may be incompatible")
                {
                    Tooltip = ch.BitrateWarning
                };
            }
            return null;
        }

        private void UpdateFilter()
        {
            TargetFlags filter = 0;
            TargetFlags excluded = 0;
            Filters.Where(s => s.IsSelected.Value).ToList().ForEach(s => filter |= s.Flags);
            Filters.Where(s => !s.IsSelected.Value).ToList().ForEach(s => excluded |= s.Flags);

            if (CoreData.Settings.TargetFilter != (int)excluded)
                CoreData.Settings.TargetFilter = (int)excluded;

            var filtered = new[] { _custom }.Concat( _initialTargets.Where(s =>
            {
                bool add = (s.Source.Flags & filter) > 0;
                if (AppData.HideTargetFilter)
                    add = _resources.TargetFilter(s.Source);
                return add;
            })).ToList();


            TargetModel[] recommended = Array.Empty<TargetModel>();
            if (CoreData.Settings.TargetPreference == TargetPreference.Adult || filter == TargetFlags.Adult)
            {
                recommended = filtered.Where(s => s.Source?.Promotion?.Recommended == true && 
                                                    (s.Source.Flags & TargetFlags.Adult) > 0).ToArray();

                filtered.ForEach(s => s.ShowTags.Value = true);
            }
            else 
            {
                recommended = filtered.Where(s => s.Source?.Promotion?.Recommended == true &&
                                                    (s.Source.Flags & TargetFlags.Adult) == 0).ToArray();

                filtered.ForEach(s => s.ShowTags.Value = (s.Source.Flags & TargetFlags.Adult) == 0 );
            }
            Targets.Value = filtered.Except(recommended).ToArray();
            RecommendedTargets.Value = recommended;
        }

        private TargetModel CreateModel(ITarget source)
        {
            var n = new TargetModel { Source = source };
            if ((source.Flags & TargetFlags.Adult) > 0)
                n.Tooltip = source.Hint + " All performers must be over 18 years old.";
            else
                n.Tooltip = source.Hint;
            _ = GetImageAsync(n.Logo, source.Id);
            n.OnSelected = () => CreateChannelFromTarget(n);
            return n;
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
            var root = CoreData.Root;

            if (model != null &&
                root.Settings.TargetPreference == TargetPreference.Unknown)
            {
                root.Settings.TargetPreference = (model.Source.Flags & TargetFlags.Adult) > 0 ?
                    TargetPreference.Adult : TargetPreference.Vlog;

                if (root.Settings.TargetPreference == TargetPreference.Vlog)
                {
                    var filter = root.Settings.TargetFilter | (int)TargetFlags.Adult; // it is removing Adult
                    if (root.Settings.TargetFilter != filter)
                    {
                        Filters.ToList().ForEach(s => s.IsSelected.SilentValue = (filter & (int)s.Flags) == 0);
                        UpdateFilter();
                    }
                }
            }

            var id = IdGenerator.New();
            var channel = CoreData.Create<IChannel>(s => s.TargetId = model?.Source?.Id);

            var local = new ChannelModel(channel, id, _appEnvironment, CoreData, this);
            RefreshChannelState(local);
            _channelBeingCreated = local;
            var title = model?.Source?.Name ?? "Custom channel";

            root.Settings.ChannelBeingCreated = channel;

            Popup.Value = new TargetConfig
            {
                Add = true,
                ChannelModel = local,
                Ok = () => 
                {
                    root.Settings.ChannelBeingCreated = null;
                    root.Channels[id] = channel;
                    _channelBeingCreated = null;
                },
                Cancel = () => { }
            };
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

        private void EvaluatePreferance()
        {
            var root = CoreData.Root;

            if (root.Settings.TargetPreference == TargetPreference.None)
            {
                root.Settings.TargetPreference = GetPreferance(root);
                if (root.Settings.TargetPreference == TargetPreference.Vlog)
                    root.Settings.TargetFilter = root.Settings.TargetFilter | (int)TargetFlags.Adult;
            }
        }

        private TargetPreference GetPreferance(IRoot root)
        {
            if (root.Channels.Count > 0)
            {
                var targets = root.Channels.Values.Select(s => GetTarget(root, s.TargetId)).Where(s => s != null).ToList();
                if (targets.Count > 0)
                {
                    var adults = targets.Count(s => (s.Flags & TargetFlags.Adult) > 0);
                    if (adults > targets.Count / 2)
                        return TargetPreference.Adult;
                    else 
                        return TargetPreference.Vlog;
                }
            }

            var obs = _appEnvironment.GetObsStreamUrl(out var service); 
            if (!string.IsNullOrWhiteSpace(obs))
            {
                var target = root.Targets.Values.Where(s => s.DefaultRtmpUrl != null).
                                                 FirstOrDefault(s => s.DefaultRtmpUrl.ToLower().TrimEnd('/') == obs.ToLower().TrimEnd('/'));
                if (target != null)
                    return ((target.Flags & TargetFlags.Adult) > 0) ? TargetPreference.Adult : TargetPreference.Vlog;
            }

            if (!string.IsNullOrWhiteSpace(service))
            {
                var target = root.Targets.Values.Where(s => s.Name != null).
                                                 FirstOrDefault(s => s.Name.ToLower() == service.ToLower());
                if (target != null)
                    return ((target.Flags & TargetFlags.Adult) > 0) ? TargetPreference.Adult : TargetPreference.Vlog;
            }

            return TargetPreference.Unknown;
        }

        private ITarget GetTarget(IRoot root, string targetId)
        {
            if (targetId != null && root.Targets.TryGetValue(targetId, out var target))
                return target;
            return null;
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

        public Property<bool> ShowTags { get; } = new Property<bool>();
    }

    public class ChannelModel
    {
        public ChannelModel(IChannel source, string id, IAppEnvironment environment, CoreData coreData, MainTargetsModel parent)
        {
            Source = source;
            Parent = parent;
            Id = id;

            var targetId = source.TargetId;

            if (targetId != null && coreData.Root.Targets.TryGetValue(targetId, out var target))
            {
                Target = target;
                NeedsRtmpUrl = (Target.Flags & TargetFlags.Url) > 0;
                var platformInfo = coreData.Root.Platforms.PlatformInfos.FirstOrDefault(s => s.TargetId == targetId);
                if (platformInfo != null)
                    SupportsAutoLogin = ((platformInfo.Flags & PlatformInfoFlags.GetKey) > 0) && ClientConstants.ChatsEnabled;

                LoginDisclaimer = target.LoginDisclaimer ?? "You can login to the service and then we will get configuration automatically. Please note that we are not storing any password on our side.";
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

            WebUrl.OnChange = (o, n) => Update(() => Source.WebUrl = n == Target.WebUrl ? null : n); 
            Name.OnChange = (o, n) => Update(() => Source.Name = n == Target.Name ? null : n);
            RtmpUrl.OnChange = (o, n) => Update(() => Source.RtmpUrl = n == Target.DefaultRtmpUrl ? null : n); 
            RtmpKey.OnChange = (o, n) => Update(() => Source.Key = n == "" ? null : n);
            AutoLoginMode.OnChange = (o, n) => Update(() =>
            {
                Source.TargetMode = n ? TargetMode.AutoLogin : TargetMode.ManualKey;
                Log.Information($"Mode for channel {targetId} changed to {n}");
            });
            IsTranscoded.OnChange = (o, n) => Update(() => Parent.Transcoding.SetTranscoding(Source, n));

            TaskHelper.RunUnawaited(() => parent.GetImageAsync(Logo, source.TargetId), "Get image for channel");
        }

        private void Update(Action onUpdate)
        {
            onUpdate();
            Parent.RefreshChannelState(this);
        }



        public string Id { get; }

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

        public string LoginDisclaimer { get; }

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

        public string Tooltip { get; set; }

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
        RunningError,

        BitrateWarning,
    }
}
