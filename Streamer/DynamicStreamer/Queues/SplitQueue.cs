using System;

namespace DynamicStreamer.Queues
{
    public class SplitQueue<T> : ITargetQueue<T>
    {
        private readonly Action<Data<T>> _recycle;
        private readonly ITargetQueue<T>[] _queues;

        public SplitQueue(Action<Data<T>> recycle, params ITargetQueue<T>[] queues)
        {
            _recycle = recycle;
            _queues = queues;
        }

        public void Enqueue(Data<T> data)
        {
            if (data.SourceId >= 0 && data.SourceId < _queues.Length)
            {
                _queues[data.SourceId].Enqueue(data);
            }
            else
                _recycle(data);
        }
    }
}
