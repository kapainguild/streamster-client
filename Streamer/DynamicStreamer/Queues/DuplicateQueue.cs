using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.Queues
{
    public class DuplicateQueue<T> : ITargetQueue<T> where T : class, IPayload, new()
    {
        private readonly PayloadPool<T> _pool;
        private ITargetQueue<T>[] _queues = Array.Empty<ITargetQueue<T>>();

        public DuplicateQueue(PayloadPool<T> pool)
        {
            _pool = pool;
        }

        public void SetQueues(params ITargetQueue<T>[] queues)
        {
            lock (this)
            {
                _queues = queues;
            }
        }

        public void Enqueue(Data<T> data)
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
                        _queues[q].Enqueue(new Data<T>(frame, data.Version, data.SequenceNumber, data.Trace?.Clone()) { SourceId = data.SourceId });
                    }
                    else
                        _queues[q].Enqueue(new Data<T>(null, data.Version, data.SequenceNumber, data.Trace?.Clone()));


                }
                _queues[_queues.Length - 1].Enqueue(data);
            }
        }
    }
}
