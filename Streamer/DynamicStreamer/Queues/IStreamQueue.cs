using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.Queues
{
    


    /*public interface IStreamQueue<TPayload> : IStreamQueue
    {
        public Action OnChanged { get; set; }

        bool TryDequeue(out Data<TPayload> result);

        void Enqueue(Data<TPayload> data);
    }*/

    public interface ISourceQueue
    {
        int Count { get; }
    }

    public interface ISourceQueue<TPayload> : ISourceQueue
    {
        bool TryDequeue(out Data<TPayload> result);

        public Action OnChanged { get; set; }
    }

    public interface ITargetQueue<TPayload>
    {
        void Enqueue(Data<TPayload> data);
    }
}
