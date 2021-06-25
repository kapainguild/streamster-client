using DynamicStreamer.Helpers;
using DynamicStreamer.Nodes;
using System;

namespace DynamicStreamer.Queues
{
    public class FpsQueue<TPayload> : ITargetQueue<TPayload>, IStatProvider where TPayload : class, IPayload, new()
    {
        private readonly ITargetQueue<TPayload> _next;
        private readonly PayloadPool<TPayload> _payloadPool;
        private readonly int _maxDeviation;
        private readonly OverloadController _overloadController;
        private readonly int _version;
        protected StatisticKeeper<StatisticDataOfFpsQueue> _statisticKeeper;

        private long _currentTimeTick = -1;

        public int Fps { get; }

        public NodeName Name { get; }

        public FpsQueue(NodeName name, ITargetQueue<TPayload> next, IStreamerBase streamerBase, PayloadPool<TPayload> payloadPool, int fps, int maxDeviation, AVRational timeBase, 
            OverloadController overloadController, int version)
        {
            Name = name;
            _next = next;
            _payloadPool = payloadPool;
            Fps = fps;
            _maxDeviation = maxDeviation;
            _overloadController = overloadController;
            _version = version;
            _statisticKeeper = new StatisticKeeper<StatisticDataOfFpsQueue>(name);
            
            if (timeBase.den != 10_000_000)
                throw new InvalidOperationException($"FpsQueue {name} requires correct timeBase (not {timeBase.num}/{timeBase.den})");
        }

        public void Enqueue(Data<TPayload> data)
        {
            long payloadPts = data.Payload.GetPts();
            long payloadTime = payloadPts * Fps / 10_000_000;

            lock (this)
            {
                if (_version != -1 && _version != data.Version)
                {
                    _next.Enqueue(data);
                }
                else if (_currentTimeTick == -1) // init
                {
                    if (payloadTime != 0)
                        _currentTimeTick = payloadTime;
                    _next.Enqueue(data);
                }
                else
                {
                    long bestTime = _currentTimeTick + 1;
                    if (payloadTime < bestTime - _maxDeviation)
                    {
                        _payloadPool.Back(data.Payload);
                        _statisticKeeper.Data.Dropped++;
                    }
                    else
                    {
                        long sendCount = 1;

                        if (payloadTime > bestTime + _maxDeviation)
                        {
                            sendCount += payloadTime - bestTime - _maxDeviation + 1;

                            if (sendCount > Fps * 1.5) // > 1.5 sec gap
                            {
                                _statisticKeeper.Data.Errors++;
                                Core.LogWarning($"FPS filtering {Name} encountered gap of {sendCount} packets", "FPS filtering  encountered gap");
                            }
                        }

                        if (_overloadController != null)
                            _overloadController.Increment(ref _currentTimeTick, sendCount);
                        else
                            _currentTimeTick += sendCount;
                        _next.Enqueue(data);
                    }
                }
            }
        }

        public StatisticItem GetStat()
        {
            return _statisticKeeper.Get();
        }
    }
}
