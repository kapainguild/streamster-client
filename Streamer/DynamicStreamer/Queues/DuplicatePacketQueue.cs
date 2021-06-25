using System;

namespace DynamicStreamer.Queues
{
   public  class DuplicatePacketQueue : ITargetQueue<Packet>
    {
        private readonly PayloadPool<Packet> _pool;
        private ITargetQueue<Packet>[] _queues = Array.Empty<ITargetQueue<Packet>>();

        public DuplicatePacketQueue(PayloadPool<Packet> pool)
        {
            _pool = pool;
        }

        public void SetQueues(params ITargetQueue<Packet>[] queues)
        {
            lock (this)
            {
                _queues = queues;
            }
        }

        public void Enqueue(Data<Packet> data)
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
                    var frame = _pool.Rent();
                    frame.CopyContentFrom(data.Payload);

                    _queues[q].Enqueue(new Data<Packet>(frame, data.Version, data.SequenceNumber, data.Trace?.Clone()));
                }
                _queues[_queues.Length - 1].Enqueue(data);
            }
        }
    }
}
