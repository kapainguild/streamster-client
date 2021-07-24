using DynamicStreamer.Queues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DynamicStreamer.Nodes
{
    public record NodeName(string TrunkPrefix, string Trunk = null, string Name = null, int Order = 0, int Pool = -1)
    {
        public override string ToString() => Pool < 0 ? $"{TrunkPrefix}{Trunk}.{Name}" : $"{TrunkPrefix}{Trunk}.{Name}.{Pool}";

        public string SortId() => $"{TrunkPrefix}{Trunk}{Order}{Pool}";
    }

    public abstract class Node<TContext, TContextConfig, TInput, TOutput> : IStatProvider, ISourceQueueHolder where TContext : IDisposable
    {
        private int _locker = 0;
        private bool _toDispose = false;
        protected StatisticKeeper<StatisticDataOfProcessingNode> _statisticKeeper;
        protected readonly LinkedList<ContextVersion<TContext, TContextConfig, TOutput>> _versions = new LinkedList<ContextVersion<TContext, TContextConfig, TOutput>>();
        private int _uniqueVersionsLimit = int.MaxValue;

        public IStreamerBase Streamer { get; }

        public NodeName Name { get; }

        protected Node(NodeName name, IStreamerBase streamer)
        {
            Name = name;
            Streamer = streamer;
            _statisticKeeper = new StatisticKeeper<StatisticDataOfProcessingNode>(name);
        }

        public ISourceQueue<TInput> InputQueue { get; protected set; }

        public ISourceQueue InputQueueForOverload => InputQueue;

        public virtual StatisticItem GetStat()
        {
            TryRemoveOldVersions();
            return _statisticKeeper.Get();
        }

        public void Activate()
        {
            if (_toDispose)
                Core.LogWarning($"Trying to active disposed node {Name}");

            if (TryAllocate())
            {
                if (InputQueue.TryDequeue(out var data))
                {
                    Streamer.ProcessingPool.Enqueue(new ProcessingItem(() =>
                    {
                        ProcessData(data);
                        Deallocate();

                        Activate();
                    }));
                }
                else
                    Deallocate();
            }
        }

        public void ActivateNoData()
        {
            if (_toDispose)
                Core.LogWarning($"Trying to active disposed node {Name}");

            if (TryAllocate())
            {
                Streamer.ProcessingPool.Enqueue(new ProcessingItem(() =>
                {
                    ProcessData(null);
                    Deallocate();
                }));
            }
        }

        private void Deallocate()
        {
            Interlocked.Exchange(ref _locker, 0);
        }

        private bool TryAllocate()
        {
            return Interlocked.Exchange(ref _locker, 1) == 0;
        }

        public void ProcessData(Data<TInput> data)
        {
            var version = GetVersion(data?.Version ?? -1);
            ProcessData(data, version);
            if (version != null)
                version.IsInUse = false;
        }

        protected virtual RefCounted<TContext> CreateAndOpenContextRef(TContextConfig setup)
        {
            return new RefCounted<TContext>(CreateAndOpenContext(setup));
        }

        public TContext PrepareVersion(UpdateVersionContext update, ISourceQueue<TInput> inputQueue, ITargetQueue<TOutput> outputQueue, TContextConfig setup, Action<TContext, bool> prepareContext = null, int uniqueVersionsLimit = int.MaxValue)
        {
            update.RuntimeConfig.Add(this, setup);

            var last = _versions.Last?.Value;// no need to sync as it is changed from the same thread
            bool sameConfig = last != null && last.ContextSetup.Equals(setup);
            int uniqueVersion = last?.UniqueContextVersion ?? 0;
            if (!sameConfig)
            {
                uniqueVersion++;
                if (last != null)
                    Core.LogInfo($"Change {Name}: {last.ContextSetup} >> {setup}");
                else
                    Core.LogInfo($"Change {Name}:  >> {setup}");
            }

            var ctx = sameConfig ? last.Context.AddRef() : CreateAndOpenContextRef(setup);
            ContextVersion<TContext, TContextConfig, TOutput> pending = new ContextVersion<TContext, TContextConfig, TOutput>
            {
                Version = update.Version,
                Context = ctx,
                ContextSetup = setup,
                OutputQueue = outputQueue,
                UniqueContextVersion = uniqueVersion
            };
            update.AddDeploy(() =>
            {
                lock (this)
                {
                    if (_versions.Last != null)
                        _versions.Last.Value.TimeWhenOutdated = DateTime.UtcNow;
                    _versions.AddLast(pending);

                    InputQueue = inputQueue;
                    if (inputQueue != null)
                        InputQueue.OnChanged = Activate;

                    prepareContext?.Invoke(pending.Context.Instance, sameConfig);
                    _uniqueVersionsLimit = uniqueVersionsLimit;
                    TryRemoveOldVersions();
                }
            });
            return pending.Context.Instance;
        }

        protected ContextVersion<TContext, TContextConfig, TOutput> GetVersion(int payloadVersion)
        {
            ContextVersion<TContext, TContextConfig, TOutput> result = null;

            lock (this)
            {
                var last = _versions.Last;

                if (payloadVersion >= 0)
                {
                    while (last != null && last.Value.Version != payloadVersion)
                        last = last.Previous;
                }

                if (last == null)
                {
                    if (_versions.Count == 0)
                        Core.LogWarning($"Version {payloadVersion} not found for {Name}: No versions defined", "Version not found");
                    else if (payloadVersion > _versions.Last.Value.Version)
                        Core.LogWarning($"Version {payloadVersion} not found for {Name}: payload is newer", "Version not found");
                    else
                        Core.LogWarning($"Version {payloadVersion} not found for {Name}: payload is older", "Version not found");
                }
                else
                {
                    result = last.Value;
                    result.IsInUse = true;
                }
            }

            return result;
        }

        private void TryRemoveOldVersions()
        {
            if (_versions.Count > 1)
            {
                DateTime now = DateTime.UtcNow;

                List<ContextVersion<TContext, TContextConfig, TOutput>> toDispose = null;

                lock (this)
                {
                    while (_versions.Count > 1)
                    {
                        var first = _versions.First.Value;
                        var last = _versions.Last.Value;

                        if ((now - first.TimeWhenOutdated > TimeSpan.FromSeconds(1) ||
                             last.UniqueContextVersion - first.UniqueContextVersion >= _uniqueVersionsLimit) &&
                             !first.IsInUse)
                        {
                            toDispose = toDispose ?? new List<ContextVersion<TContext, TContextConfig, TOutput>>();
                            toDispose.Add(first);
                            _versions.RemoveFirst();
                        }
                        else break;
                    }
                }
                toDispose?.ForEach(s =>
                {
                    s.Dispose();
                });
            }
        }

        protected abstract TContext CreateAndOpenContext(TContextConfig config);

        public void Dispose()
        {
            _toDispose = true;
            if (TryAllocate())
                DisposeVersions();
            else
                Streamer.AddPendingDisposal(Dispose);
        }

        protected void DisposeVersions()
        {
            List<ContextVersion<TContext, TContextConfig, TOutput>> toDispose = null;
            lock (this)
            {
                toDispose = _versions.ToList();
                _versions.Clear();
            }

            foreach (var s in toDispose)
                s.Dispose();
        }

        protected abstract void ProcessData(Data<TInput> data, ContextVersion<TContext, TContextConfig, TOutput> currentContext);
    }

    public class ContextVersion<TContext, TContextConfig, TOutput> : IDisposable where TContext : IDisposable
    {
        public ContextVersion()
        {
        }

        public DateTime TimeWhenOutdated { get; set; } = DateTime.MaxValue;
        
        public int Version { get; set; }

        public int UniqueContextVersion { get; set; }

        public RefCounted<TContext> Context { get; set; }

        public ITargetQueue<TOutput> OutputQueue {get; set;}

        public TContextConfig ContextSetup { get; set; }

        public bool IsDisposed { get; private set; }

        public bool IsInterrupted { get; set; }

        public bool IsOpened { get; set; } // for inputs only

        public bool IsInUse { get; internal set; }

        public void Dispose()
        {
            IsInterrupted = true;
            if (!IsDisposed)
            {
                IsDisposed = true;
                Context?.RemoveRef();

                if (ContextSetup is IDisposable csd)
                    csd.Dispose();
            }
        }
    }
}
