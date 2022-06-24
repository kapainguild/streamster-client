using Clutch.DeltaModel;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Models.Chats;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class MainModel : IAsyncDisposable
    {
        private readonly HubConnectionService _hubConnectionService;
        private readonly IAppEnvironment _environment;
        private readonly CoreData _coreData;
        private readonly StateLoggerService _stateLoggerService;
        private readonly LocalSettingsService _localSettingsService;
        private readonly IUpdateManager _updateManager;
        private readonly IAppResources _appResources;
        private readonly ResourceService _resourceService;
        private readonly StreamingSourcesModel _streamingSourcesModel;
        private readonly ModelClient _serverClient;
        private readonly IWindowStateManager _windowStateManager;
        private readonly LocalSourcesModel _localSources;
        private bool _firstPatchReceived;
        private IDisposable _onPatchSubscription;
        private IDisposable _onReceiveChatSubscription;
        private TaskCompletionSource<bool> _firstPatchTcs;

        private bool _sendInProgress;

        private bool _ignoreWarningSendPatch;

        private Task _prepareTask;

        public RootModel Root { get; }

        public CoreData CoreData => _coreData;

        public StreamingSourcesModel StreamingSources => _streamingSourcesModel;

        public MainTargetsModel Targets { get; }

        public PlatformsModel Platforms { get; }

        public MainSettingsModel Settings { get; }
        
        public StreamSettingsModel StreamSettings { get; }
        public MainStreamerModel Streamer { get; }

        public MainIndicatorsModel Indicators { get; }
        public MainVpnModel Vpn { get; }
        public MainAboutModel About { get; }
        public AudioModel Audio { get; }
        public TransientMessageModel TransientMessage { get; }

        public SceneEditingModel SceneEditing { get; }

        public Property<bool> Loaded { get; } = new Property<bool>();

        public Property<bool> IsDialogShown { get; } = new Property<bool>();

        public Property<object> DialogContent { get; } = new Property<object>();

        public MainModel(RootModel root,
            MainTargetsModel targets,
            MainSettingsModel settings,
            LocalSourcesModel sources,
            StreamSettingsModel streamSettings,
            MainStreamerModel streamer,
            MainIndicatorsModel indicators,
            MainVpnModel vpn,
            MainAboutModel about,
            AudioModel audio,
            HubConnectionService hubConnectionService,
            IWindowStateManager windowStateManager,
            IAppEnvironment environment,
            CoreData coreData,
            StateLoggerService stateLoggerService,
            LocalSettingsService localSettingsService,
            IUpdateManager updateManager,
            TransientMessageModel transientMessageModel,
            IAppResources appResources,
            SceneEditingModel sceneEditingModel,
            ResourceService resourceService,
            PlatformsModel platforms,
            StreamingSourcesModel streamingSourcesModel)
        {
            Root = root;
            Targets = targets;
            Settings = settings;
            _localSources = sources;
            StreamSettings = streamSettings;
            Streamer = streamer;
            Indicators = indicators;
            Vpn = vpn;
            About = about;
            Audio = audio;
            _hubConnectionService = hubConnectionService;
            _windowStateManager = windowStateManager;
            _environment = environment;
            _coreData = coreData;
            _stateLoggerService = stateLoggerService;
            _localSettingsService = localSettingsService;
            _updateManager = updateManager;
            TransientMessage = transientMessageModel;
            _appResources = appResources;
            SceneEditing = sceneEditingModel;
            _resourceService = resourceService;
            Platforms = platforms;
            _streamingSourcesModel = streamingSourcesModel;
            _serverClient = new ModelClient(_coreData.GetManager(), new FilterConfigurator(true).Build());
            _coreData.GetManager().Register(_serverClient);
            _serverClient.SerializeAndClearChanges();

            _coreData.Subscriptions.OnChangeForSubscriptions = async () => await ProcessLocalOrRemoteChange();
            _coreData.Subscriptions.OnLocalChange = async () => await ProcessLocalChange();
        }

        internal void BeforeConnect()
        {
            if (_prepareTask == null)
                _prepareTask = PrepareAsync();
        }

        private async Task PrepareAsync()
        {
            await TaskHelper.GoToPool().ConfigureAwait(false);
            Streamer.Prepare();
            await _localSources.PrepareAsync();
        }

        internal async Task StartAsync()
        {
            _onPatchSubscription?.Dispose();
            _onReceiveChatSubscription?.Dispose();
            _firstPatchTcs = new TaskCompletionSource<bool>();
            var connection = _hubConnectionService.CreateConnection(OnConnectionChanged);
            _onPatchSubscription = connection.On<ProtocolJsonPatchPayload>(nameof(IConnectionHubClient.JsonPatch), p => OnPatchOnMainThread(p));
            _onReceiveChatSubscription = connection.On<ReceiveChatMessagesData>(nameof(IConnectionHubClient.ReceiveChatMessages), p => Platforms.OnReceiveChatMessagesData(p));

            Log.Information("Connecting to hub");
            await _hubConnectionService.StartConnection();
            Log.Information("Connected to hub");
        }

        private void OnConnectionChanged(bool connected)
        {
            if (connected)
            {
                if (DialogContent.Value is ConnectionFailedModel && IsDialogShown.Value)
                {
                    DialogContent.Value = null;
                    IsDialogShown.Value = false;
                }
            }
            else
            {
                if (DialogContent.Value is ConnectionFailedModel fail)
                {
                    fail.Attempt.Value += 1;
                }
                else
                {
                    DialogContent.Value = new ConnectionFailedModel();
                }
                IsDialogShown.Value = true;
            }
        }

        private async Task InitializeAfterFirstPatchAsync()
        {
            _coreData.ThisDevice.Name = _environment.GetDeviceName();

            _resourceService.Start();
            await Vpn.StartAsync(); // Targets and Streamer depends on it
            Targets.Start();
            _localSources.Start();
            _streamingSourcesModel.Start();
            SceneEditing.Start();
            StreamSettings.Start();
            Streamer.Start();
            Settings.Start();
            About.Start();
            _stateLoggerService.Start();
            Audio.Start();
            Platforms.Start();
        }

        public async Task DisplayAsync(Task readyToDisplay, ClientVersion[] upperVersions, string appUpdatePath)
        {
            Exception failed = null;
            try
            {
                Log.Information("Waiting for first patch");
                // readytToDisplay means animation is done.
                await Task.WhenAll(readyToDisplay, _firstPatchTcs.Task);
                Log.Information("Waiting done");
                // resize window
                _windowStateManager.Start();
                // display model
                Root.NavigateTo(this);

                // wait a bit while model is applied to view
                await Task.Delay(10);
                // start Fade in animation
                Loaded.Value = true;

                _environment.PreventSleepMode(true);

                if (upperVersions != null)
                    ProcessNewVersion(upperVersions);
                TaskHelper.RunUnawaited(_updateManager.Update(appUpdatePath), "UpdateFromMain");
            }
            catch (Exception e)
            {
                failed = e;
            }

            if (failed != null)
            {
                Log.Error(failed, "Startup failure");

                await Task.Delay(1050); 
                await _hubConnectionService.ReleaseConnection();
                throw new ConnectionServiceException("Application failed to initialize. Please contact administrator.", failed);
            }
        }

        private void ProcessNewVersion(ClientVersion[] upperVersions)
        {
            var (currentVersionInfo, currentVersionString) = ClientVersionHelper.GetCurrent(upperVersions);

            bool simulateUpdate = false;
            string custom = "4.5.0";

            if (_localSettingsService.Settings.LastRunVerion != currentVersionString || simulateUpdate)
            {
                if ((_localSettingsService.Settings.NotFirstInstall || _localSettingsService.NoSettingsFileAtLoad == false) && 
                    string.IsNullOrEmpty(_appResources.AppData.Domain) || simulateUpdate)
                {
                    string[] standard = string.IsNullOrWhiteSpace(currentVersionInfo?.WhatsNew) ? null :
                        currentVersionInfo.WhatsNew.Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => "\u2022 " + s.Trim()).ToArray();

                    if (standard != null || currentVersionString == custom)
                    {
                        NewVersionModel newVersion = new NewVersionModel
                        {
                            WhatsNew = standard,
                            Title = $"You are running new version {currentVersionString}",
                            CustomView = !string.IsNullOrWhiteSpace(custom)
                        };

                        if (DialogContent.Value == null)
                        {
                            DialogContent.Value = newVersion;
                            IsDialogShown.Value = true;
                        }
                    }
                }

                TaskHelper.RunUnawaited(_localSettingsService.ChangeSettingsUnconditionally(s =>
                {
                    s.LastRunVerion = currentVersionString;
                    s.NotFirstInstall = true;
                }), "Store LastRunVerion");
            }
        }

        private async Task OnPatchOnMainThread(ProtocolJsonPatchPayload payload)
        {
            if (SynchronizationContext.Current == null)
            {
                // we are on working thread
                _coreData.RunOnMainThread(() => { _ = OnPatch(payload); });

            }
            else
                await OnPatch(payload);
        }
        
        private async Task OnPatch(ProtocolJsonPatchPayload payload)
        {
            Log.Debug($"{{---{payload.Changes}");
            if (_firstPatchReceived)
                _coreData.GetManager().ApplyChanges(_serverClient, payload.Changes);
            else if (payload.Reset)
                await OnInitialPatch(payload);
            else
                Log.Warning("Fake initial patch");
        }

        private async Task OnInitialPatch(ProtocolJsonPatchPayload payload)
        {
            try
            {
                Log.Information($"Initializing with first patch ({payload.Changes?.Length})");
                await _prepareTask;

                Log.Information("Applying changes"); 
                // we want to initialize everything in background thread
                _coreData.GetManager().ApplyChanges(_serverClient, payload.Changes);
                ProcessSubscriptions();
                await InitializeAfterFirstPatchAsync();
                ProcessSubscriptions();
                await SendPatch();

                Log.Information("Initialized with first patch");

                _firstPatchReceived = true;
                _firstPatchTcs.TrySetResult(true);

                // process again. Required as process subscriptions can be ignored due to _firstPatchReceived = false
                ProcessSubscriptions();
            }
            catch (Exception e)
            {
                Log.Error(e, "OnInitialPatch failed");
                _firstPatchTcs.TrySetException(e);
            }
        }

        private async Task ProcessLocalChange()
        {
            _coreData.Subscriptions.ResetLocalChangesFlag();
            if (_firstPatchReceived)
            {
#if DEBUG
                if (SynchronizationContext.Current == null)
                    throw new InvalidOperationException("Change is called on none UI thread");
#endif
                if (SynchronizationContext.Current == null)
                    Log.Warning("ProcessLocalChange from none UI thread");

                if (!_sendInProgress)
                {
                    _sendInProgress = true;
                    await Task.Delay(10);

                    try
                    {
                        while (await SendPatch()) ;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "SendPatch failed");
                    }
                    finally
                    {
                        _sendInProgress = false;
                    }
                }
            }
        }

        private async Task<bool> SendPatch()
        {
            var changes = _serverClient.SerializeAndClearChanges();
            if (changes != null)
            {
                var state = _hubConnectionService.Connection.State;
                if (state == HubConnectionState.Connected || state == HubConnectionState.Connecting)
                {
                    Log.Debug($"---}} {changes}");
                    await _hubConnectionService.Connection.InvokeAsync(nameof(IConnectionHubServer.JsonPatch), new ProtocolJsonPatchPayload { Changes = changes });
                    _ignoreWarningSendPatch = false;
                }
                else
                {
                    if (!_ignoreWarningSendPatch)
                        Log.Warning($"Send patch ignored due to state '{state}'");
                    _ignoreWarningSendPatch = true;
                }

                return true;
            }
            return false;
        }

        private async Task ProcessLocalOrRemoteChange()
        {
            try
            {
                if (_firstPatchReceived)
                {
#if DEBUG
                    //check whether call is on main thread
                    if (SynchronizationContext.Current == null)
                        throw new InvalidOperationException("Change is called on none UI thread");
#endif
                    if (SynchronizationContext.Current == null)
                        Log.Warning("ProcessLocalOrRemoteChange from none UI thread");

                    // collect a set of changes
                    await Task.Delay(10);

                    ProcessSubscriptions();
                }
            }
            catch(Exception e)
            {
                Log.Error(e, "Failed to ProcessLocalOrRemoteChange");
            }
        }

        public void ProcessSubscriptions()
        {
            var subscriptions = _coreData.Subscriptions.GetAndClearNotifications();
            try
            {
                foreach (var subscription in subscriptions)
                {
                    subscription();
                }
            }
            catch(Exception e)
            {
                Log.Error(e, $"Processing subscriptions failed");
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_firstPatchReceived)
            {
                var dev = _coreData.ThisDevice;

                if (dev != null)
                    dev.DisconnectRequested = true;

                try
                {
                    await SendPatch();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Send final Patch failed");
                }
            }
        }
    }

    public class ConnectionFailedModel
    {
        public Property<int> Attempt { get; } = new Property<int>(1);
    }

    public class NewVersionModel
    {
        public string[] WhatsNew { get; set; } 

        public string Title { get; set; }

        public bool CustomView { get; set; }
    }
}
