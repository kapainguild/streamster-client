using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DynamicStreamer
{
    public class Data<TPayload>
    {
        public Data(TPayload payload, int version, int sequenceNumber, PayloadTrace trace)
        {
            Payload = payload;
            Version = version;
            SequenceNumber = sequenceNumber;
            Trace = trace;
        }

        public TPayload Payload { get; set; }

        public int Version { get; set; }

        public int SequenceNumber { get; set; }

        public int SourceId { get; set; }

        public PayloadTrace Trace { get; set; }
    }

    public interface IPayload : IDisposable
    {
        void Unref();

        long GetPts();

        void SetPts(long pts);

        void CopyContentFromAndSetPts(IPayload from, long pts);

        void CopyContentFrom(IPayload from);

        void RescaleTimebase(ref AVRational from, ref AVRational to);
    }

    public class FromPool<T> : IDisposable where T : class, IPayload, new()
    {
        private PayloadPool<T> _pool;

        public T Item { get; }

        public FromPool(T item, PayloadPool<T> pool)
        {
            _pool = pool;
            Item = item;
        }

        public FromPool(PayloadPool<T> pool) : this(pool.Rent(), pool)
        {
        }

        public void Dispose()
        {
            if (_pool != null)
            {
                _pool.Back(Item);
                _pool = null;
            }
        }
    }

    public class PayloadPool<T> where T: class, IPayload, new()
    {
        private readonly Stack<T> _nodes = new Stack<T>();

        private int _rented;

        public PayloadPool()
        {
        }

        public T Rent()
        {
            lock (this)
            {
                _rented++;
                if (_nodes.TryPop(out var node))
                    return node;
             }
            return new T();
        }

        public void Back(T node)
        {
            if (node != null)
            {
                node.Unref();
                lock (this)
                {
                    _nodes.Push(node);
                    _rented--;
                }
            }
        }

        public (int pooled, int inField) CleanUp()
        {
            lock(this)
            {
                var pooled = _nodes.Count;

                while (_nodes.Count > 10)
                {
                    var item = _nodes.Pop();
                    item.Dispose();
                }
                return (pooled, _rented);
            }
        }
    }
}
