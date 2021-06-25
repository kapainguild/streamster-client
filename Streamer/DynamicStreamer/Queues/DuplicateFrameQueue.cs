using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.Queues
{
    public class DuplicateFrameQueue : ITargetQueue<Frame>
    {
        private readonly PayloadPool<Frame> _pool;
        private ITargetQueue<Frame>[] _queues = Array.Empty<ITargetQueue<Frame>>();

        public DuplicateFrameQueue(PayloadPool<Frame> pool)
        {
            _pool = pool;
        }

        public void SetQueues(params ITargetQueue<Frame>[] queues)
        {
            lock (this)
            {
                _queues = queues;
            }
        }

        public void Enqueue(Data<Frame> data)
        {
            lock (this)
            {
                if (_queues.Length == 0)
                {
                    _pool.Back(data.Payload);
                    return;
                }

                for (int q = 0; q < _queues.Length - 1; q++)
                {
                    if (data.Payload != null)
                    {
                        var frame = _pool.Rent();
                        frame.CopyContentFrom(data.Payload);
                        _queues[q].Enqueue(new Data<Frame>(frame, data.Version, data.SequenceNumber, data.Trace?.Clone()));
                    }
                    else
                        _queues[q].Enqueue(new Data<Frame>(null, data.Version, data.SequenceNumber, data.Trace?.Clone()));


                }
                _queues[_queues.Length - 1].Enqueue(data);
            }
        }
    }
}
