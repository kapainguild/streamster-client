using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.Queues
{
    public class SetSourceIdQueue<T> : ITargetQueue<T> where T : IPayload
    {
        private readonly ITargetQueue<T> _output;
        private readonly int _sourceId;

        public SetSourceIdQueue(ITargetQueue<T> output, int sourceId)
        {
            _output = output;
            _sourceId = sourceId;
        }

        public void Enqueue(Data<T> data)
        {
            data.SourceId = _sourceId;
            _output.Enqueue(data);
        }
    }
}
