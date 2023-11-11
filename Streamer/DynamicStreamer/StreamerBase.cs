using DynamicStreamer.Contexts;
using DynamicStreamer.Helpers;
using DynamicStreamer.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DynamicStreamer
{
    public interface IStreamerBase
    {
        public ResourceManager ResourceManager { get; }

        TimerSubscription Subscribe(int timeout, Action action);

        void UnsubscribeTimer(TimerSubscription sub);

        void NoneblockingUpdate();

        ProcessingPool ProcessingPool { get; }

        PayloadPool<Packet> PacketPool { get; }

        PayloadPool<Frame> FramePool { get; }

        void AddPendingDisposal(Action dispose);

        void ReinitDirectX();

        int GetDxFailureCounter();

        void WebRtcBitrateReport(IOutputContext outputContect, string id, TimeSpan runTime, int bitrate);
    }


    public abstract class StreamerBase<TConfig> : IStreamerBase, IDisposable
    {
        protected AVRational _time_base = new AVRational { num = 1, den = 10_000_000 };
        protected AVRational _sample_aspect_ratio = new AVRational { num = 0, den = 1 };
        protected ulong _default_channel_layout = 3;
        protected int _default_sample_fmt = 8; //AV_SAMPLE_FMT_FLTP

        protected bool _stopped;

        private int _currentVersion = 0;
        private int _maxVersion = 0;

        private readonly List<Action> _pendingDisposal = new List<Action>();
        

        public ResourceManager ResourceManager { get; }


        private TConfig _config;
        private bool _disposed;
        private readonly Thread _updaterThread;
        private bool _updaterThreadContinue = true;
        private readonly object _updaterMonitor = new object();

        public StreamerBase(string name)
        {
            int processors = Environment.ProcessorCount;
            if (processors < 8)
                processors = 8;

            ProcessingPool.StartProcessing(processors);

            ResourceManager = new ResourceManager(this);

            _updaterThread = new Thread(() => UpdateThreadRoutine());
            _updaterThread.Name = $"Streamer:Updater of {name}";
            _updaterThread.Start();
        }

        public ProcessingPool ProcessingPool { get; } = new ProcessingPool();

        public PayloadPool<Packet> PacketPool { get; } = new PayloadPool<Packet>(); 

        public PayloadPool<Frame> FramePool { get; } = new PayloadPool<Frame>();

        public abstract int GetDxFailureCounter();

        public void AddPendingDisposal(Action dispose)
        {
            lock (_pendingDisposal)
                _pendingDisposal.Add(dispose);
        }

        public virtual void WebRtcBitrateReport(IOutputContext outputContect, string id, TimeSpan runTime, int bitrate) { }

        private bool GetPendingDisposal(out Action[] disposal)
        {
            lock (_pendingDisposal)
            {
                if (_pendingDisposal.Count > 0)
                {
                    disposal = _pendingDisposal.ToArray();
                    _pendingDisposal.Clear();
                    return true;
                }
                disposal = null;
                return false;
            }
        }

        public TimerSubscription Subscribe(int timeout, Action action)
        {
            var sub = new TimerSubscription { Timer = new Timer(_ => action(), null, timeout, timeout) };
            sub.Unsubscribe = () => UnsubscribeTimer(sub);
            return sub;
        }

        public void UnsubscribeTimer(TimerSubscription sub)
        {
            sub.Timer.Dispose();
        }

        private void UpdateThreadRoutine()
        {
            while (true)
            {
                int versionToRun;
                TConfig configToRun;
                lock(_updaterMonitor)
                {
                    while (_currentVersion == _maxVersion && _updaterThreadContinue)
                        Monitor.Wait(_updaterMonitor);

                    if (!_updaterThreadContinue)
                        return;

                    versionToRun = _maxVersion;
                    configToRun = _config;
                }

                DisposeDeffered();

                var updateVersion = new UpdateVersionContext(versionToRun);
                Core.LogInfo($"Update {versionToRun} starting");
                if (!_stopped)
                    UpdateCore(updateVersion, configToRun);
                else
                    Core.LogWarning($"Update {versionToRun} on STOPPED streamster");
                updateVersion.DeployVersion();
                Core.LogInfo($"Update {versionToRun} deployed");

                ResourceManager.SetConfig(updateVersion.RuntimeConfig);

                lock (_updaterMonitor)
                {
                    _currentVersion = versionToRun;
                    Monitor.PulseAll(_updaterMonitor);
                    Monitor.Wait(_updaterMonitor, 60);
                }

            }
        }

        private void DisposeDeffered(int cycles = 1)
        {
            while (GetPendingDisposal(out var disposals))
            {
                Core.LogInfo($"Pending disposal: {disposals.Length}");
                foreach (var disposal in disposals)
                {
                    try
                    {
                        disposal();
                    }
                    catch (Exception e)
                    {
                        Core.LogError(e, "Disposal failed");
                    }
                }

                cycles--;

                if (cycles <= 0)
                    break;

                Thread.Sleep(10);
            }
        }

        public void StartUpdate(TConfig config)
        {
            lock (_updaterMonitor)
            {
                _config = config;
                _maxVersion += 1;
                Monitor.PulseAll(_updaterMonitor);
            }
        }

        public void TuneConfig(Func<TConfig, TConfig> tuner)
        {
            lock (_updaterMonitor)
            {
                var res = tuner(_config);

                if (!Equals(res, default(TConfig)))
                {
                    _config = res;
                    _maxVersion += 1;
                    Monitor.PulseAll(_updaterMonitor);
                }
            }
        }

        public void NoneblockingUpdate()
        {
            lock (_updaterMonitor)
            {
                 _maxVersion += 1;
                 Monitor.PulseAll(_updaterMonitor);
            }
        }

        public void BlockingUpdate(TConfig config = default)
        {
            lock (_updaterMonitor)
            {
                _maxVersion += 1;
                _config = config ?? _config;
                int waitFoVersion = _maxVersion;
                Monitor.PulseAll(_updaterMonitor);

                while (_currentVersion < waitFoVersion && _updaterThreadContinue)
                    Monitor.Wait(_updaterMonitor);
            }
        }

        public abstract void UpdateCore(UpdateVersionContext update, TConfig config);

        public abstract void ReinitDirectX();


        public void Dispose() 
        {
            if (!_disposed)
            {
                _disposed = true;
                DoDispose();
            }
        }

        public void StopFrameProcessing()
        {
            ProcessingPool.StopProcessing();
        }

        protected virtual void DoDispose()
        {
            ResourceManager.Dispose();

            _updaterThreadContinue = false;
            lock(_updaterMonitor)
                Monitor.PulseAll(_updaterMonitor);
            _updaterThread.Join(3000);

            DisposeDeffered(25);
        }
    }

    public class TimerSubscription
    {
        public Action Unsubscribe { get; set; }

        public Timer Timer { get; internal set; }
    }
}
