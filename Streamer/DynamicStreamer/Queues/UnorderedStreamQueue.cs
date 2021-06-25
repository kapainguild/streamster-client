using DynamicStreamer.Nodes;
using System;
using System.Collections.Generic;

namespace DynamicStreamer.Queues
{
    public class UnorderedStreamQueue<TPayload> : ITargetQueue<TPayload>, ISourceQueue<TPayload> where TPayload : class, IPayload, new()
    {
        private readonly Queue<Data<TPayload>> _payloads = new Queue<Data<TPayload>>();
        private readonly NodeName _name;
        private readonly PayloadPool<TPayload> _payloadPool;
        private readonly int _limit;
        private bool _disposed;
        private int _sequenceNumber = 0;


        public UnorderedStreamQueue(NodeName name, PayloadPool<TPayload> payloadPool, int limit = int.MaxValue)
        {
            _name = name;
            _payloadPool = payloadPool;
            _limit = limit;
            OnChanged = () => { Core.LogWarning($"Fake activation of {_name}"); };
        }

        public int Count
        {
            get
            {
                lock (this)
                    return _payloads.Count;
            }
        }

        public Action OnChanged { get; set; } 

        public bool TryDequeue(out Data<TPayload> result)
        {
            lock (this)
            {
                if (_payloads.Count > 0)
                {
                    result = _payloads.Dequeue();
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            }
        }

        public void Enqueue(Data<TPayload> payload)
        {
            bool raise = true;
            lock (this)
            {
                if (_disposed)
                {
                    raise = false;
                    Core.LogWarning($"Enqueue to disposed {_name} queue");
                    _payloadPool.Back(payload.Payload);
                }
                else if (_payloads.Count >= _limit)
                {
                    Core.LogWarning($"Payloads exceeding limit in {_name} queue");
                    _payloadPool.Back(payload.Payload);
                }
                else
                {
                    payload.SequenceNumber = _sequenceNumber++;
                    _payloads.Enqueue(payload);
                }
            }
            if (raise)
                OnChanged();
        }

        public void Dispose()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    if (_payloads.Count > 0)
                        Core.LogWarning($"Disposing {_payloads.Count} payloads in {_name} queue");

                    while (_payloads.TryDequeue(out var payload))
                    {
                        _payloadPool.Back(payload.Payload);
                    }
                    _disposed = true;
                }
            }
        }
    }
}
