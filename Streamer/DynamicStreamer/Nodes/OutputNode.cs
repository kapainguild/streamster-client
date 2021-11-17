using DynamicStreamer.Contexts;
using DynamicStreamer.Queues;
using System;
using System.Threading;

namespace DynamicStreamer.Nodes
{
    public class OutputNode : IDisposable, IStatProvider
    {
        private readonly Thread _thread;
        private readonly StatisticKeeper<StatisticDataOfInputOutput> _statisticKeeper;

        private bool _continueProcessing;
        private int _initialErrorCounter;
        private int _errorCounter;

        private OutputStreamQueueReader<Packet> _reader;

        private IBitrateController _bitrateController;


        private OutputContextVersion _currentContext;
        private OutputContextVersion _pendingContext;

        public IStreamerBase Streamer { get; }

        public NodeName Name { get; }

        public OutputNode(NodeName name, IStreamerBase streamer, OutputStreamQueue<Packet> inputQueue)
        {
            Name = name;
            Streamer = streamer;
            InputQueue = inputQueue;
            _statisticKeeper = new StatisticKeeper<StatisticDataOfInputOutput>(name);
            _thread = new Thread(() => OnThread());
            _thread.Name = $"Streamer:Output for {name}";
            _reader = InputQueue.CreateReader();
        }

        public OutputStreamQueue<Packet> InputQueue { get; set; }

        public StatisticItem GetStat() => _statisticKeeper.Get();

        private void ReplaceCurrentVersion()
        {
            OutputContextVersion contextToClose = null;
            lock (this)
            {
                if (_pendingContext != null)
                {
                    contextToClose = _currentContext;
                    _currentContext = _pendingContext;
                    _pendingContext = null;
                }
            }

            if (contextToClose != null)
                contextToClose.Dispose();
        }

        private void OnThread()
        {
            int q = 0;

            while (_continueProcessing)
            {
                ReplaceCurrentVersion();

                if (_currentContext == null)
                    Thread.Sleep(50);
                else
                {
                    bool waitAfterFail = false;
                    try
                    {
                        if (!_currentContext.Context.Instance.IsOpened)
                        {
                            _currentContext.Context.Instance.Open(_currentContext.ContextSetup);
                            _bitrateController?.Reconnected();
                        }

                        var dataReference = InputQueue.Dequeue(_reader, ref _continueProcessing, out var dropped); 

                        if (dropped > 0)
                            Core.LogWarning($"Dropped {dropped} packets on {Name}");

                        if (dataReference != null) // break with _continueprocessing = false?
                        {
                            Packet packet = null;
                            try
                            {
                                var packetVersion = dataReference.Data.Version;
                                packet = Streamer.PacketPool.Rent();
                                var sourceId = dataReference.Data.SourceId;

                                if (packetVersion < _currentContext.Version)
                                {
                                    Core.LogWarning($"Output {Name} with version {_currentContext.Version} ignores packet {packetVersion}", "output ignores old packet");
                                }
                                else
                                {
                                    while (_currentContext.MaxVersion < packetVersion && _continueProcessing)
                                    {
                                        Core.LogWarning($"Output {Name} with version {_currentContext.Version} waits for {packetVersion}", "output waits new version");
                                        Thread.Sleep(50);
                                        ReplaceCurrentVersion();
                                    }

                                    if (_currentContext.Version <= packetVersion && packetVersion <= _currentContext.MaxVersion)
                                    {
                                        if (_currentContext.WaitForIFrame <= 0 ||
                                            (dataReference.Data.Payload.Properties.Flags & 1) > 0 && sourceId == 0) // Are waiting for I-Frame on Video stream?
                                        {
                                            _currentContext.WaitForIFrame = 0;
                                            packet.CopyContentFrom(dataReference.Data.Payload);
                                            DateTime writeStart = DateTime.UtcNow;
                                            int size = packet.Properties.Size;
                                            var writeRes = _currentContext.Context.Instance.Write(packet, sourceId);
                                            var writeTime = DateTime.UtcNow - writeStart;

                                            //if (q % 610 == 0)
                                            //    Thread.Sleep(3000);

                                            q++;

                                            if (writeTime.TotalMilliseconds > 300)
                                                Core.LogWarning($"Long send {(int)writeTime.TotalMilliseconds}ms", "Long send");

                                            if (writeRes == ErrorCodes.TimeoutOrInterrupted)
                                                throw new OperationCanceledException();

                                            ProcessWriteResult(writeRes, packet, size, sourceId);
                                        }
                                        else
                                        {
                                            Core.LogInfo($"Output {Name} waits for I-Frame", "output waits i-frame");
                                            if (sourceId == 0)
                                            {
                                                _currentContext.WaitForIFrame--;
                                                if (_currentContext.WaitForIFrame == 0)
                                                    Core.LogError($"Output {Name} failed to waits for I-Frame");
                                            }
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                dataReference.RemoveReference();
                                Streamer.PacketPool.Back(packet);
                            }
                        }
                    }
                    catch(OperationCanceledException)
                    {
                        Core.LogError($"Open stream cancelled for {Name}");
                    }
                    catch (Exception e)
                    {
                        waitAfterFail = true;
                        _statisticKeeper.Data.Errors++;
                        Core.LogError(e, $"Open stream failed for {Name}");
                    }

                    if (waitAfterFail)
                        Thread.Sleep(50);
                }
            }

            if (_reader != null)
                InputQueue.RemoveReader(_reader);
        }

        private void ProcessWriteResult(ErrorCodes writeRes, Packet packet, int size, int sourceId)
        {
            if (writeRes < 0) // note that res can be here as "Interrupted"
            {
                if (writeRes == ErrorCodes.InvalidArgument && _initialErrorCounter < 8) //TODO: check why. Is due to 'Application provided invalid, non monotonically increasing dts to muxer in stream 1' when restream is started before stream to cloud
                {
                    _initialErrorCounter++;
                    Core.LogWarning($"skip error {sourceId} {packet.Properties.Dts} {packet.Properties.Pts}");
                }
                else
                {
                    _errorCounter += 5;
                    if (_errorCounter > 50)
                    {
                        _errorCounter = 0;
                        _currentContext.Context.Instance.CloseOutput();
                    }
                }
            }
            else
            {
                if (_errorCounter > 0)
                    _errorCounter--;
            }

            lock (_statisticKeeper)
            {
                if (writeRes >= 0)
                {
                    _statisticKeeper.Data.Frames++;
                    _statisticKeeper.Data.Bytes += size;
                }
                else
                    _statisticKeeper.Data.Errors++;
            }
        }

        public bool PrepareVersion(UpdateVersionContext update, OutputSetup setup, IBitrateController bitrateController)
        {
            if (bitrateController != null && _bitrateController == null)
                bitrateController.InitOutput(_reader);
            _bitrateController = bitrateController;
            
            update.RuntimeConfig.Add(this, setup);
            var last = _currentContext;

            bool sameConfig = last != null && last.ContextSetup.Equals(setup);
            if (!sameConfig)
                Core.LogInfo($"Change {Name}: {last?.ContextSetup} >> {setup}");

            int version = update.Version;

            update.AddDeploy(() =>
            {
                lock (this)
                {
                    if (sameConfig)
                    {
                        _pendingContext = new OutputContextVersion
                        {
                            Version = last.Version,
                            MaxVersion = version,
                            ContextSetup = setup,
                            Context = last.Context.AddRef()
                        };
                    }
                    else
                    {
                        _pendingContext = new OutputContextVersion
                        {
                            Version = last == null ? version - 1 : version, // we assume that this output is just added - so accept previous version from the output queue
                            MaxVersion = version,
                            ContextSetup = setup,
                            Context = new RefCounted<OutputContext>(new OutputContext()),
                            WaitForIFrame = 125 // in case fps=60 -> gop = 120. we add 5.
                        };

                        if (_currentContext != null && _currentContext.Context != null)
                            _currentContext.Context.Instance.Interrupt();
                    }
                }

                if (!_continueProcessing)
                {
                    _continueProcessing = true;
                    _thread.Start();
                }
            });

            return !sameConfig;
        }

        public void Dispose()
        {
            lock (this)
            {
                _continueProcessing = false;

                if (_currentContext != null && _currentContext.Context != null)
                    _currentContext.Context.Instance.Interrupt();
            }

            InputQueue.PulseAll();

            if (_thread != null && !_thread.Join(3000))
                Core.LogError($"Failed to stop output thread for {Name}");

            _pendingContext?.Dispose();
            _currentContext?.Dispose();
        }
    }

    public class OutputContextVersion: IDisposable
    {
        private bool _disposed;

        public int Version { get; set; }

        public int MaxVersion { get; set; }

        public RefCounted<OutputContext> Context { get; set; }

        public OutputSetup ContextSetup { get; set; }

        public int WaitForIFrame { get; set; }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Context.RemoveRef();
            }
        }
    }
}
