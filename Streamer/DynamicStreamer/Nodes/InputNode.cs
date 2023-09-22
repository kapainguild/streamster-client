using DynamicStreamer.Contexts;
using DynamicStreamer.Extensions.DesktopAudio;
using DynamicStreamer.Extensions.Rtmp;
using DynamicStreamer.Extensions.ScreenCapture;
using DynamicStreamer.Extensions.WebBrowser;
using DynamicStreamer.Queues;
using System;
using System.Linq;
using System.Net.Sockets;
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

        private DateTime _lastOpenCycle = DateTime.MaxValue;
        private bool _openThreadDenied;

        private bool _initialPacketReceived;
        private int _initialStatisticsCounter;
        private int _initialPackets;
        private int _initialVideoPackets;
        private int _initialBytes;

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

        private void LogInfo(string message, string template = null) => Log(LogType.Info, message, template);

        private void Log(LogType type, string message, string template = null) => Core.LogDotNet(type, $"IIIII '{Name}'-'{_inputThreadCurrentContext?.ContextSetup}': " + message, template);


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


            if (_initialStatisticsCounter < 5)
            {
                lock (this)
                {
                    _initialStatisticsCounter++;
                    LogInfo($"Initial stat: packets={_initialPackets}, kbps={_initialBytes*8/1000}, video packets={_initialVideoPackets}");
                    _initialPackets = 0;
                    _initialBytes = 0;
                    _initialVideoPackets = 0;   
                }
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
                    bool sameConfig = last != null && Equals(last.ContextSetup, setup);
                    bool sameDevice = last != null && Equals(last.ContextSetup.Input, setup.Input);
                    if (!sameConfig)
                        LogInfo($"{last?.ContextSetup} >> {setup}");

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
                    LogInfo($"cancelled on OpenThread");
                }
                catch (Exception e)
                {
                    UpdateError(e);
                    Core.LogError(e, $"IIIII '{Name}' failed on OpenThread");
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
            _statisticKeeper.UpdateData(d => d.AddError((e is DynamicStreamerException ds && ds.ErrorCode == -5) ? InputErrorType.InUse : InputErrorType.Error));
        }

        private void Open(ContextVersion<IInputContext, InputSetup, Packet> context)
        {
            var instance = context.Context.Instance;

            LogInfo($"opening as '{context.ContextSetup}'");

            instance.Open(context.ContextSetup);

            LogInfo($"opened as '{context.ContextSetup}'");

            var attempt = Math.Min(10, _analyzeAttempt); // 0 .. 10
            _analyzeAttempt++;
            var delay = Math.Max(500, Math.Min(8000, 500 * (1 << attempt))); // 300ms - 8 sec
            instance.Analyze(delay, context.ContextSetup.ExpectedNumberOfStreams);

            LogInfo($"analyzed");
            context.IsOpened = true;

            _initialPacketReceived = false;
            _initialStatisticsCounter = 0;
            _initialPackets = 0;
            _initialBytes = 0;
            _initialVideoPackets = 0;
        }

        private void OnInputThread()
        {
            int seqNumber = 0;
            bool suspend = false;

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
                        if (contextChanged)
                            suspend = false;

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

                if (suspend)
                {
                    // rtmp closed, so no action until the context is replaced
                    Thread.Sleep(50);
                }
                else if (_inputThreadCurrentContext == null)
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
                            LogInfo("ContextChanged");
                            _inputOpened();
                            continue; // try to update context as new version is to be issued.
                        }
                        
                        //read
                        var packet = _streamer.PacketPool.Rent();
                        packetToDispose = packet;
                        ctx.Read(packet, _inputThreadCurrentContext.ContextSetup);

                        _statisticKeeper.UpdateData(d => d.AddPacket(packet.Properties.Size, packet.Properties.StreamIndex == 0));

                        if (!_initialPacketReceived)
                        {
                            _initialPacketReceived = true;
                            LogInfo($"first packet size={packet.Properties.Size}, flags={packet.Properties.Flags}, stream={packet.Properties.StreamIndex}");
                        }

                        if (_initialStatisticsCounter < 5)
                        {
                            lock (this)
                            {
                                _initialPackets++;
                                _initialBytes += packet.Properties.Size;

                                if (packet.Properties.StreamIndex == 0)
                                    _initialVideoPackets++;
                            }
                        }

                        packetToDispose = null;
                        seqNumber++;
                        _inputThreadCurrentContext.OutputQueue.Enqueue(new Data<Packet>(packet, _inputThreadCurrentContext.Version, seqNumber, PayloadTrace.Create(Name, null, seqNumber)) { SourceId = packet.Properties.StreamIndex });
                    }
                    catch (GracefulCloseException)
                    {
                        _streamer.PacketPool.Back(packetToDispose);
                        _inputThreadCurrentContext.IsOpened = false;
                        LogInfo("gracefully closed");
                        suspend = true;
                    }
                    catch (OperationCanceledException)
                    {
                        _streamer.PacketPool.Back(packetToDispose);
                        _inputThreadCurrentContext.IsOpened = false;
                        LogInfo($"open/read cancelled on InputThread");
                    }
                    catch (Exception e)
                    {
                        UpdateError(e);
                        _streamer.PacketPool.Back(packetToDispose);

                        bool wasOpened = _inputThreadCurrentContext.IsOpened;
                        _inputThreadCurrentContext.IsOpened = false;
                        if (e is SocketException && (e.Message.StartsWith("An operation was attempted on something that is not a socket")
                            || e.Message.StartsWith("The handle is invalid")))
                            Core.LogError($"IIIII '{Name}' open/read failed on InputThread for {_inputThreadCurrentContext.Context?.Instance?.GetType()}: {e.Message}");
                        else 
                            Core.LogError(e, $"IIIII '{Name}' open/read failed on InputThread");
                        waitAfterFail = !wasOpened;
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

            if (contextSetup.Type == RtmpInputContext.Name)
                return new RtmpInputContext(_streamer);

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
                Log(LogType.Error, $"Failed to stop input thread");

            if (_openThread != null && !_openThread.Join(1000))
                Log(LogType.Error, $"Failed to stop open thread");

            _inputThreadCurrentContext?.Dispose();
            _inputThreadPendingContext?.Dispose();
            _openThreadPendingContext?.Dispose();
            _openThreadRunningContext?.Dispose();
        }

        public StatisticItem[] GetStat()
        {
            var context = _inputThreadCurrentContext?.Context?.Instance as IStatProvider;
            if (context == null)
                return new[] { _statisticKeeper.Get() };
            else
            {
                var stat = context.GetStat();

                return stat?.Select(s => new StatisticItem
                {
                    Name = _name,
                    DurationMs = s.DurationMs,
                    Data = s.Data
                })?.ToArray();
            }
        }
    }
}
