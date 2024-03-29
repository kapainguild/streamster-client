﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DynamicStreamer.Queues
{
    public class OutputStreamQueue<TPayload> : ITargetQueue<TPayload> where TPayload : Packet, new()
    {
        private LinkedList<Data<TPayload>> _sortingQueue = new LinkedList<Data<TPayload>>();
        private readonly LinkedList<OutputStreamQueueDataReference<TPayload>> _readyQueue = new LinkedList<OutputStreamQueueDataReference<TPayload>>();
        private readonly List<int> _sortingQueueCountsPerId = new List<int>();
        private readonly List<OutputStreamQueueReader<TPayload>> _readers = new List<OutputStreamQueueReader<TPayload>>();
        private readonly PayloadPool<TPayload> _payloadPool;

        private long _lastReleasedPts = 0;

        private int _lastSequenceNumber = -1;

        private int _traceCounter = 0;

        public int MaxCount { get; set; } = 256;

        public OutputStreamQueue(PayloadPool<TPayload> payloadPool, params int[] sourceIds)
        {
            _payloadPool = payloadPool;
            RegisterSources(sourceIds);
        }

        public void SetSourceIds(int[] sourceIds)
        {
            lock (this)
            {
                if (sourceIds.Length != _sortingQueueCountsPerId.Count)
                {
                    Core.LogInfo($"Updating number of streams from {_sortingQueueCountsPerId.Count} to {sourceIds.Length}");
                    while (_sortingQueueCountsPerId.Count < sourceIds.Length)
                    {
                        _sortingQueueCountsPerId.Add(0);
                    }

                    while (_sortingQueueCountsPerId.Count > sourceIds.Length)
                    {
                        _sortingQueueCountsPerId.RemoveAt(_sortingQueueCountsPerId.Count - 1);
                        int id = _sortingQueueCountsPerId.Count;

                        _sortingQueue = new LinkedList<Data<TPayload>>(_sortingQueue.Where(s => s.SourceId != id));
                    }
                }
            }
        }

        public void Enqueue(Data<TPayload> payload)
        {
            //Core.LogInfo($"QIn {Core.FormatTicks(payload.Payload.GetPts())} ({_sortingQueueCountsPerId[0]}/{_sortingQueueCountsPerId[1]})  {GetHashCode()}");

            if (payload.SourceId == 0 && payload.Trace != null)
            {
                if ((_traceCounter++) % 300 == 0)
                    Core.LogInfo($"trace enc: {payload.Trace.GetDump()}");
            }

            lock(this)
            {
                var last = _sortingQueue.Last;
                var pts = payload.Payload.GetPts();

                if (payload.SourceId >= _sortingQueueCountsPerId.Count)
                {
                    Core.LogWarning($"Unexpected sourceId of packet {payload.SourceId} while max is {_sortingQueueCountsPerId.Count}");
                }
                else if (pts >= _lastReleasedPts)
                {
                    while (last != null && pts < last.Value.Payload.GetPts())
                        last = last.Previous;

                    if (last == null)
                        _sortingQueue.AddFirst(payload);
                    else
                        _sortingQueue.AddAfter(last, payload);

                    _sortingQueueCountsPerId[payload.SourceId]++;

                    // try to move to ready

                    while (_sortingQueueCountsPerId.All(s => s > 0) || IsOverloaded())
                    {
                        var first = _sortingQueue.First.Value;
                        _lastReleasedPts = first.Payload.GetPts();
                        _sortingQueueCountsPerId[first.SourceId]--;
                        AddToReady(first);
                        _sortingQueue.RemoveFirst();
                    }
                }
                else
                    Core.LogWarning($"OutputQueue dismissed too early packet SourceId = {payload.SourceId}/Flags={payload.Payload.Properties.Flags}, {pts} < {_lastReleasedPts}", "Dismissed too early packet");
            }
        }

        private bool IsOverloaded()
        {
            if (_sortingQueue.Count > 0)
            {
                var first = _sortingQueue.First.Value.Payload.GetPts();
                var last = _sortingQueue.Last.Value.Payload.GetPts();
                var delta = last - first;

                int audio = _sortingQueueCountsPerId.Count > 1 ? _sortingQueueCountsPerId[1] : -1;
                // 3,5 seconds
                if (delta > 35_000_000 || _sortingQueueCountsPerId[0] > 200 || audio > 200)
                {
                    
                    Core.LogWarning($"GAP! Pushing packet to Output. QDuration {delta / 10_000}ms, VideoQ = {_sortingQueueCountsPerId[0]}, AudioQ={audio}", "GAP! Pushing packet to Output.");
                    return true;
                }
            }
            return false;
        }

        private void AddToReady(Data<TPayload> first)
        {
            first.SequenceNumber = ++_lastSequenceNumber;
            _readyQueue.AddLast(new OutputStreamQueueDataReference<TPayload> { Pool = _payloadPool, Data = first, References = 1 });
            while (_readyQueue.Count > MaxCount)
                RemoveFirstFromReadyQueue();
            Monitor.PulseAll(this);
        }

        private void RemoveFirstFromReadyQueue()
        {
            _readyQueue.First.Value.RemoveReference();
            _readyQueue.RemoveFirst();
        }

        public OutputStreamQueueDataReference<TPayload> Dequeue(OutputStreamQueueReader<TPayload> reader, ref bool continueProcessing, out int dropped)
        {
            lock (this)
            {
                while (continueProcessing && reader.CurrentSequenceNumber > _lastSequenceNumber)
                    Monitor.Wait(this);

                int bufferSize = _lastSequenceNumber - reader.CurrentSequenceNumber;
                reader.BufferSize = bufferSize;
                if (continueProcessing)
                {
                    OutputStreamQueueDataReference<TPayload> result;
                    if (reader.CurrentSequenceNumber < _readyQueue.First.Value.Data.SequenceNumber)
                    {
                        dropped = _readyQueue.First.Value.Data.SequenceNumber - reader.CurrentSequenceNumber;
                        reader.CurrentSequenceNumber = _readyQueue.First.Value.Data.SequenceNumber + 1;

                        result = _readyQueue.First.Value.AddReference();
                    }
                    else
                    {
                        var last = _readyQueue.Last;

                        while (last.Value.Data.SequenceNumber != reader.CurrentSequenceNumber)
                            last = last.Previous;

                        dropped = 0;
                        reader.CurrentSequenceNumber++;

                        result = last.Value.AddReference();

                        if (last.Previous == null) // it is first
                            Clean();
                    }
                    reader.NotifyPacket?.Invoke(result.Data.Payload, result.Data.SourceId, bufferSize);
                    return result;
                }
                else
                {
                    dropped = 0;
                    return null;
                }

            }
        }

        private void Clean()
        {
            var min = _readers.Count == 0 ? int.MaxValue : _readers.Min(s => s.CurrentSequenceNumber);

            while (_readyQueue.Count > 0 && min > _readyQueue.First.Value.Data.SequenceNumber)
                RemoveFirstFromReadyQueue();
        }

        public void PulseAll()
        {
            lock(this)
                Monitor.PulseAll(this);
        }

        public OutputStreamQueueReader<TPayload> CreateReader()
        {
            lock (this)
            {
                var seq = _lastSequenceNumber + 1;
                var last = _readyQueue.Last;
                int count = 0;
                int size = 0;
                while (last != null)
                {
                    var lastPacket = last.Value.Data;
                    if ((lastPacket.Payload.Properties.Flags & 1) > 0 && lastPacket.SourceId == 0)
                    {
                        seq = lastPacket.SequenceNumber;
                        break;
                    }

                    size += lastPacket.Payload.Properties.Size;
                    last = last.Previous;
                    count++;
                }

                Core.LogInfo($"Adding reader, to be sent {count} packets and {size} bytes");
                
                var result = new OutputStreamQueueReader<TPayload>
                {
                    CurrentSequenceNumber = seq,
                    BufferSize = count
                };
                _readers.Add(result);
                return result;
            }
        }

        public void RemoveReader(OutputStreamQueueReader<TPayload> reader)
        {
            lock (this)
            {
                _readers.Remove(reader);
                Clean();
            }
        }

        public void RegisterSources(params int[] sourceIds)
        {
            foreach (var id in sourceIds)
            {
                if (id != _sortingQueueCountsPerId.Count)
                    throw new NotSupportedException("wrong sequence of source ids");
                _sortingQueueCountsPerId.Add(0);
            }
        }
    }


    public class OutputStreamQueueReader<TPayload>
    {
        public int CurrentSequenceNumber { get; set; }

        public Action<TPayload, int, int> NotifyPacket { get; set; }

        public int BufferSize { get; internal set; }
    }


    public class OutputStreamQueueDataReference<TPayload>  where TPayload : class, IPayload, new()
    {
        public Data<TPayload> Data { get; set; }

        public volatile int References;

        public PayloadPool<TPayload> Pool { get; internal set; }

        internal void RemoveReference()
        {
            if (Interlocked.Decrement(ref References) == 0)
                Pool.Back(Data.Payload);
        }

        internal OutputStreamQueueDataReference<TPayload> AddReference()
        {
            Interlocked.Increment(ref References); 
            return this;
        }
    }
}
