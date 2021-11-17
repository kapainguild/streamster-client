using DynamicStreamer.Contexts;
using DynamicStreamer.Extension;
using DynamicStreamer.Extensions.DesktopAudio;
using DynamicStreamer.Extensions.ScreenCapture;
using DynamicStreamer.Extensions.WebBrowser;
using DynamicStreamer.Queues;
using System;
using System.Threading;

namespace DynamicStreamer.Nodes
{
    public class InputNode : IStatProvider
    {
        private readonly Action _inputOpened;
        private readonly NodeName _name;
        private readonly IStreamerBase _streamer;
        protected StatisticKeeper<StatisticDataOfInputOutput> _statisticKeeper;

        private readonly Thread _inputThread;
        private ContextVersion<IInputContext, InputSetup, Packet> _inputThreadCurrentContext;
        private ContextVersion<IInputContext, InputSetup, Packet> _inputThreadPendingContext;
        private volatile bool _inputThreadIsRunning = false;

        private Thread _openThread;
        private ContextVersion<IInputContext, InputSetup, Packet> _openThreadPendingContext;
        private ContextVersion<IInputContext, InputSetup, Packet> _openThreadRunningContext;
        private volatile bool _openThreadIsRunning = false;
        private int _analyzeAttempt;
        private TimerSubscription _observer;

        private DateTime _lastInputCycle = DateTime.MaxValue;
        private DateTime _lastOpenCycle = DateTime.MaxValue;
        private bool _openThreadDenied;

        public NodeName Name => _name;

        public InputNode(NodeName name, Action inputOpened, IStreamerBase streamer)
        {
            _name = name;
            _statisticKeeper = new StatisticKeeper<StatisticDataOfInputOutput>(name);
            _inputOpened = inputOpened;
            _streamer = streamer;
            _inputThread = new Thread(() => OnInputThread());
            _inputThread.Name = $"Streamer:{name} input thread";
            _observer = streamer.Subscribe(1000, On1Second);
        }

        private void On1Second()
        {
            ContextVersion<IInputContext, InputSetup, Packet> openContextToClose = null;
            lock (this)
            {
                DateTime now = DateTime.UtcNow;

                if (now - _lastOpenCycle > TimeSpan.FromSeconds(5)) // open thread is hanged
                {
                    openContextToClose = _openThreadRunningContext;
                    _openThreadRunningContext = null;
                    _openThreadDenied = true;
                }
            }

            if (openContextToClose != null)
            {

            }
        }

        public IInputContext CurrentContext => _inputThreadCurrentContext?.Context?.Instance;

        public IInputContext PrepareVersion(UpdateVersionContext update, ITargetQueue<Packet> outputQueue, InputSetup setup)
        {
            update.RuntimeConfig.Add(this, setup);
            update.AddDeploy(() =>
            {
                bool startOpenThread = false;
                lock (this)
                {
                    var last = _inputThreadCurrentContext;
                    bool sameConfig = last != null && last.ContextSetup.Equals(setup);
                    bool sameDevice = last != null && last.ContextSetup.Input.Equals(setup.Input);
                    if (!sameConfig)
                        Core.LogInfo($"Change {_name}: {last?.ContextSetup} >> {setup}");

                    int version = update.Version;
                    var ver = new ContextVersion<IInputContext, InputSetup, Packet>
                    {
                        Version = version,
                        ContextSetup = setup,
                        OutputQueue = outputQueue,
                    };

                    if (sameConfig)
                    {
                        ver.Context = last.Context.AddRef();

                        _inputThreadPendingContext?.Dispose();
                        _inputThreadPendingContext = ver;
                    }
                    else
                    {
                        if (sameDevice || last == null || _openThreadDenied)
                        {
                            _inputThreadPendingContext?.Dispose();
                            _inputThreadPendingContext = ver;

                            if (_inputThreadCurrentContext != null && _inputThreadCurrentContext.Context != null)
                                _inputThreadCurrentContext.Context.Instance.Interrupt();
                        }
                        else
                        {
                            _openThreadPendingContext?.Dispose();
                            _openThreadPendingContext = ver;

                            if (_openThreadRunningContext != null && _openThreadRunningContext.Context != null)
                                _openThreadRunningContext.Context.Instance.Interrupt();

                            if (!_openThreadIsRunning)
                            {
                                _openThreadIsRunning = true;
                                startOpenThread = true;
                            }
                        }
                    }
                }

                if (!_inputThreadIsRunning)
                {
                    _inputThreadIsRunning = true;
                    _inputThread.Start();
                }
                if (startOpenThread)
                {
                    _openThread = new Thread(() => OnOpenThread());
                    _openThread.Name = $"Streamer:{Name} input-open thread";
                    _openThread.Start();
                }
            });

            var last = _inputThreadCurrentContext;
            // same config and at least one time opened
            return (last != null && last.Context != null && last.Context.Instance.Config != null) ? last.Context.Instance : null;
        }

        private void OnOpenThread()
        {
            while (true)
            {
                lock(this)
                {
                    if (_openThreadPendingContext == null)
                    {
                        _openThreadIsRunning = false;
                        _lastOpenCycle = DateTime.MaxValue;
                        return;
                    }
                    _openThreadRunningContext = _openThreadPendingContext;
                    _openThreadRunningContext.Context = new RefCounted<IInputContext>(CreateInputContext(_openThreadRunningContext.ContextSetup));
                    _openThreadPendingContext = null;
                    _lastOpenCycle = DateTime.UtcNow;
                }
                try
                {
                    Open(_openThreadRunningContext);
                }
                catch (OperationCanceledException)
                {
                    Core.LogInfo($"Open stream cancelled for {Name} on OpenThread");
                }
                catch (Exception e)
                {
                    UpdateError(e);
                    Core.LogError(e, $"Open stream failed for {Name} on OpenThread");
                }

                lock(this)
                {
                    if (_openThreadRunningContext != null)
                    {
                        if (_inputThreadCurrentContext != null && _inputThreadCurrentContext.Context != null)
                            _inputThreadCurrentContext.Context.Instance.Interrupt();

                        _inputThreadPendingContext = _openThreadRunningContext;
                        _openThreadRunningContext = null;
                    }
                }
            }
        }

        private void UpdateError(Exception e)
        {
            _statisticKeeper.Data.Errors++;
            if (e is DynamicStreamerException ds && ds.ErrorCode == -5)
                _statisticKeeper.Data.ErrorType = InputErrorType.InUse;
            else
                _statisticKeeper.Data.ErrorType = InputErrorType.Error;
        }

        private void Open(ContextVersion<IInputContext, InputSetup, Packet> context)
        {
            var instance = context.Context.Instance;

            Core.LogInfo($"Input {Name} opening");

            instance.Open(context.ContextSetup);

            Core.LogInfo($"Input {Name} opened");

            var attempt = _analyzeAttempt;
            _analyzeAttempt++;
            var delay = Math.Min(8000, 2000 * (1 << attempt)); // 2, 4, 8, 8, 8 sec
            instance.Analyze(delay, context.ContextSetup.ExpectedNumberOfStreams);
            context.IsOpened = true;
        }

        private void OnInputThread()
        {
            int seqNumber = 0;

            while (_inputThreadIsRunning)
            {
                ContextVersion<IInputContext, InputSetup, Packet> contextToClose = null;
                bool contextChanged = false;
                lock (this)
                {
                    if (_inputThreadPendingContext != null)
                    {
                        //Core.LogInfo($"Updating context on {Name}");

                        contextChanged = !ReferenceEquals(_inputThreadPendingContext?.Context?.Instance, _inputThreadCurrentContext?.Context?.Instance);
                        contextToClose = _inputThreadCurrentContext;

                        if (!contextChanged && 
                            _inputThreadPendingContext != null &&
                            _inputThreadCurrentContext != null) //if this is the same instance -> copy openedflag
                        {
                            _inputThreadPendingContext.IsOpened = _inputThreadCurrentContext.IsOpened;
                        }

                        _inputThreadCurrentContext = _inputThreadPendingContext;
                        _inputThreadPendingContext = null;

                        if (_inputThreadCurrentContext.Context == null)
                            _inputThreadCurrentContext.Context = new RefCounted<IInputContext>(CreateInputContext(_inputThreadCurrentContext.ContextSetup)); 
                    }
                }

                if (contextToClose != null)
                    contextToClose.Dispose();

                if (_inputThreadCurrentContext == null)
                    Thread.Sleep(50);
                else
                {
                    Packet packetToDispose = null;
                    var ctx = _inputThreadCurrentContext.Context.Instance;
                    bool waitAfterFail = false;
                    try
                    {
                        if (!_inputThreadCurrentContext.IsOpened)
                        {
                            Open(_inputThreadCurrentContext);
                            _inputOpened();
                            continue; // try to update context as new version is to be issued.
                        }
                        else if (contextChanged) // can be passed here from open thread
                        {
                            Core.LogInfo($"ContextChanged on {Name}");
                            _inputOpened();
                            continue; // try to update context as new version is to be issued.
                        }
                        
                        //read
                        var packet = _streamer.PacketPool.Rent();
                        packetToDispose = packet;
                        ctx.Read(packet, _inputThreadCurrentContext.ContextSetup);
                        
                        lock (_statisticKeeper)
                        {
                            _statisticKeeper.Data.Frames++;
                            _statisticKeeper.Data.Bytes += packet.Properties.Size;
                        }

                        packetToDispose = null;
                        seqNumber++;
                        _inputThreadCurrentContext.OutputQueue.Enqueue(new Data<Packet>(packet, _inputThreadCurrentContext.Version, seqNumber, PayloadTrace.Create(Name, null, seqNumber)) { SourceId = packet.Properties.StreamIndex });
                    }
                    catch (OperationCanceledException)
                    {
                        _streamer.PacketPool.Back(packetToDispose);
                        _inputThreadCurrentContext.IsOpened = false;
                        Core.LogInfo($"Open/read stream cancelled for {Name} on InputThread");
                    }
                    catch (Exception e)
                    {
                        UpdateError(e);
                        _streamer.PacketPool.Back(packetToDispose);
                        _inputThreadCurrentContext.IsOpened = false;
                        Core.LogError(e, $"Open/read stream failed for {Name} on InputThread");
                        waitAfterFail = true;
                    }

                    if (waitAfterFail)
                        Thread.Sleep(250);
                }
            }
        }

        private IInputContext CreateInputContext(InputSetup contextSetup)
        {
            if (contextSetup.Type == DesktopAudioContext.Name)
                return new DesktopAudioContext();

            if (contextSetup.Type == WebBrowserContext.Name)
                return new WebBrowserContext(_streamer, ExtensionsManager.MainThreadExecutor);

            if (contextSetup.Type == PluginContext.PluginName)
                return new PluginContext(_streamer, ExtensionsManager.MainThreadExecutor);

            if (contextSetup.Type == ScreenCaptureContext.Name)
                return new ScreenCaptureContext(_streamer);

            return new InputContext();
        }

        public void Dispose()
        {
            _observer.Unsubscribe();
            lock (this)
            {
                _inputThreadIsRunning = false;
                _openThreadIsRunning = false;

                if (_inputThreadCurrentContext != null && _inputThreadCurrentContext.Context != null)
                    _inputThreadCurrentContext.Context.Instance.Interrupt();

                if (_openThreadRunningContext != null && _openThreadRunningContext.Context != null)
                    _openThreadRunningContext.Context.Instance.Interrupt();
            }

            if (_inputThread != null && !_inputThread.Join(3000))
                Core.LogError($"Failed to stop input thread for {Name}");

            if (_openThread != null && !_openThread.Join(1000))
                Core.LogError($"Failed to stop open thread for {Name}");

            _inputThreadCurrentContext?.Dispose();
            _inputThreadPendingContext?.Dispose();
            _openThreadPendingContext?.Dispose();
            _openThreadRunningContext?.Dispose();
        }

        public StatisticItem GetStat()
        {
            return _statisticKeeper.Get();
        }
    }
}
