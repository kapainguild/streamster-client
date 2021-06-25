using DynamicStreamer.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicStreamer.Queues
{
    public class OrderedStreamQueue<TPayload> : ITargetQueue<TPayload>, ISourceQueue<TPayload> where TPayload : class, IPayload, new()
    {
        private int currentSequenceNumber = 0;
        private readonly NodeName _name;
        private readonly PayloadPool<TPayload> _payloadPool;
        private bool _disposed;
        private readonly LinkedList<Data<TPayload>> _payloads = new LinkedList<Data<TPayload>>(); 

        public OrderedStreamQueue(NodeName name, PayloadPool<TPayload> payloadPool)
        {
            _name = name;
            _payloadPool = payloadPool;
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
            bool reactivate = false;
            result = null;
            lock (this)
            {
                if (TryFindSequenceNumber(currentSequenceNumber, out var node))
                {
                    currentSequenceNumber++;
                    _payloads.Remove(node);
                    reactivate = TryFindSequenceNumber(currentSequenceNumber, out var node2);
                    result = node.Value;
                }
                else
                {
                    if (_payloads.Count > 4)
                    {
                        Core.LogWarning($"{_name} queue lost sequence");
                        currentSequenceNumber = _payloads.Min(s => s.SequenceNumber);
                        reactivate = true;
                    }
                }
            }

            if (reactivate)
                OnChanged();

            return result != null;
        }

        private bool TryFindSequenceNumber(int currentSequenceNumber, out LinkedListNode<Data<TPayload>> result)
        {
            var next = _payloads.First;

            while (next != null)
            {
                if (next.Value.SequenceNumber == currentSequenceNumber)
                {
                    result = next;
                    return true;
                }
                next = next.Next;
            }
            result = null;
            return false;
        }

        public void Enqueue(Data<TPayload> payload)
        {
            bool raise = false;
            lock(this)
            {
                if (_disposed)
                {
                    Core.LogWarning($"Enqueue to disposed {_name} queue");
                    _payloadPool.Back(payload.Payload);
                }
                else
                {
                    if (payload.SequenceNumber < currentSequenceNumber)
                    {
                        Core.LogWarning($"Enqueue delayed packet ({payload.SequenceNumber} < {currentSequenceNumber}) to {_name} queue");
                        _payloadPool.Back(payload.Payload);
                    }
                    else
                    {
                        _payloads.AddLast(payload);
                        raise = payload.SequenceNumber == currentSequenceNumber;

                        if (_payloads.Count > 5)
                            raise = true; // push to call Dequeue 
                        
                        //Core.LogWarning($"{_name} Add: { payload.SequenceNumber} vs {currentSequenceNumber} = raise ({raise})");
                    }
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

                    foreach (var pay in _payloads)
                    {
                        _payloadPool.Back(pay.Payload);
                    }
                    _payloads.Clear();
                    _disposed = true;
                }
            }
        }
    }
}
