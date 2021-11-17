using DynamicStreamer.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicStreamer.Queues
{
    public record AudioMixingQueueSetup(
        bool CheckAgainstLastPacketEndPts, 
        bool UseCurrentTimeForDelta, 
        int GenerateSilenceToRuntime,
        int PushSilenceDelay);

    public class AudioMixingQueue : ITargetQueue<Frame>, IStatProvider, ISourceQueue<Frame>
    {
        private readonly Queue<Data<Frame>> _payloads = new Queue<Data<Frame>>();
        private readonly NodeName _name;
        private readonly PayloadPool<Frame> _payloadPool;
        private readonly AudioMixingQueueSetup _setup;
        private bool _disposed;
        private List<AudioMixingQueueInput> _inputs = new List<AudioMixingQueueInput>();
        private long _lastPacketPts = 0;
        private int _version;
        private TimerSubscription _timer;

        public AudioMixingQueue(NodeName name, PayloadPool<Frame> payloadPool, IStreamerBase streamer,
            AudioMixingQueueSetup setup)
        {
            _name = name;
            _payloadPool = payloadPool;
            _setup = setup;
            OnChanged = () => { Core.LogWarning($"Fake activation of {_name}"); };
            _timer = streamer.Subscribe(100, OnTimer);
        }

        public Action OnChanged { get; set; }

        public int Count
        {
            get
            {
                lock (this)
                    return _payloads.Count;
            }
        }

        public NodeName Name => _name;

        public void Enqueue(Data<Frame> payload)
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
                else
                    EnqueueNext(payload);
            }
            if (raise)
                OnChanged();
        }


        public void Reset(int version, int count, bool sameConfig)
        {
            lock(this)
            {
                if (!sameConfig)
                {
                    _inputs = Enumerable.Range(0, count).Select((s, i) => new AudioMixingQueueInput { Id = i}).ToList();
                    _lastPacketPts = 0;
                }
                _version = version;
            }
        }

        private void OnTimer()
        {
            bool changed;
            lock (this)
                changed = GenerateSilence();
            if (changed)
                OnChanged();
        }

        private long ToTime(long v) => v / 441 * 100000;

        private void EnqueueNext(Data<Frame> payload)
        {
            int source = payload.SourceId;
            if (source >= _inputs.Count)
            {
                Core.LogWarning($"{source} exceeding number of inputs, ignore.");
                _payloadPool.Back(payload.Payload);
            }
            else
            {
                var packetPts = payload.Payload.GetPts();

                var samples = payload.Payload.Properties.Samples;
                var input = _inputs[source];
                var currentTimePts = Core.GetCurrentTime() * 441 / 100000;

                if (_lastPacketPts == 0)
                {
                    _lastPacketPts = packetPts;
                    _inputs.ForEach(s =>
                    {
                        s.LastPacketPts = packetPts;
                        s.LastPacketEndPts = packetPts + ((s == input) ? samples : 0);
                        s.LastCurrentTimePts = currentTimePts;
                    });
                    _payloads.Enqueue(payload);
                }
                else
                {
                    bool gap = currentTimePts - input.LastCurrentTimePts > 44100 * 2;
                    if (gap)
                    {
                        Core.LogWarning($"Audio smooth flow interrupted {Core.FormatTicks(ToTime(input.LastPacketEndPts))} - {Core.FormatTicks(ToTime(currentTimePts))}");
                        input.DeltasCount = -200;
                    }
                    input.LastCurrentTimePts = currentTimePts;

                    if (input.LastPacketEndPts > currentTimePts)
                    {
                        _payloadPool.Back(payload.Payload);
                        input.Dropped++;
                        Core.LogWarning($"Dropping packet on audio{source} ({Core.FormatTicks(ToTime(input.LastPacketEndPts))}; {Core.FormatTicks(ToTime(currentTimePts))};  {Core.FormatTicks(ToTime(packetPts))})");
                    }
                    else if (input.LastPacketEndPts > packetPts && _setup.CheckAgainstLastPacketEndPts)
                    {
                        _payloadPool.Back(payload.Payload);
                        input.Dropped++;
                        Core.LogWarning($"Dropping obsolete packet on audio{source} ({Core.FormatTicks(ToTime(input.LastPacketEndPts))}; {Core.FormatTicks(ToTime(currentTimePts))};  {Core.FormatTicks(ToTime(packetPts))})");
                    }
                    else
                    {
                        payload.Payload.SetPts(input.LastPacketEndPts);

                        input.LastPacketPts = input.LastPacketEndPts;
                        input.LastPacketEndPts = input.LastPacketEndPts + samples;
                        input.LastInputPts = packetPts;

                        _payloads.Enqueue(payload);

                        //Core.LogWarning($"Audio {Core.FormatTicks(ToTime(input.LastPacketPts))} ({_payloads.Count})");

                        var deltaBase = _setup.UseCurrentTimeForDelta ? currentTimePts : packetPts;
                        var delta = deltaBase - input.LastPacketEndPts;
                        if (input.DeltasCount >= 0)
                            input.Deltas[input.DeltasCount] = delta;
                        input.DeltasCount++;
                        if (input.DeltasCount == input.Deltas.Length)
                        {
                            input.DeltasCount = 0;
                            var ave = input.Deltas.Average();
                            if (delta > 6000 && ave > 6000) // more then 150ms
                            {
                                var over5000 = Math.Min(delta - 5000, ave - 5000);
                                var toGenerate = Math.Min(_setup.GenerateSilenceToRuntime, Math.Max(1, (int)over5000 / 441)); // from 10 to 100 ms
                                Core.LogWarning($"Generate {toGenerate} silences to catch runtime on audio{input.Id}: d={delta},ave={ave}", "Generate silence");
                                for (int q = 0; q < toGenerate; q++)
                                    PushFrame(source, input, _version, input.LastPacketEndPts);
                            }
                        }
                    }
                }

                GenerateSilence();
            }
        }

        private bool GenerateSilence()
        {
            int initialCount = _payloads.Count;
            var currentTimePts = Core.GetCurrentTime() * 441 / 100000;

            // check other sources to inser silence
            for (int q = 0; q < _inputs.Count; q++)
            {
                var other = _inputs[q];

                if (other.LastPacketEndPts != 0)
                {
                    int generate = (int)((currentTimePts - _setup.PushSilenceDelay - other.LastPacketEndPts) / 441);

                    if (generate > 50) // kind of gap, push one packet
                    {
                        Core.LogWarning($"FATAL. Generate silence after gap {generate} on audio {other.Id}", "Generate gap silence");
                        PushFrame(q, other, _version, currentTimePts - 44100 / 4);
                    }
                    else
                    {
                        for (int w = 0; w < generate; w++)
                        {
                            Core.LogWarning($"Generate silence on audio{other.Id} {other.LastPacketEndPts * 100000 / 441}", "Generate silence");
                            PushFrame(q, other, _version, other.LastPacketEndPts);
                        }
                    }
                }
            }
            return initialCount != _payloads.Count;
        }

        private void PushFrame(int sourceId, AudioMixingQueueInput input, int version, long pts)
        {
            var frame = _payloadPool.Rent();
            frame.GenerateSilence(pts);

            var data = new Data<Frame>(frame, version, 0, null);
            data.SourceId = sourceId;
            _payloads.Enqueue(data);

            input.LastPacketPts = pts;
            input.LastPacketEndPts = pts + 441;
            input.Silence++;

            //Core.LogWarning($"Silence {Core.FormatTicks(ToTime(input.LastPacketPts))} ({_payloads.Count})");
        }

        public bool TryDequeue(out Data<Frame> result)
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

        public void Dispose()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    _timer.Unsubscribe();

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

        public StatisticItem GetStat()
        {

            lock(this)
            {
                var time = Core.GetCurrentTime();
                return new StatisticItem
                {
                    Name = _name,
                    DurationMs = 1000,
                    Data = new StatisticDataOfAudioMixerQueue
                    {
                        Inputs = _inputs.Select(s =>
                        {
                            var res =  new StatisticDataOfAudioMixerQueue_Input
                            {
                                Dropped = s.Dropped,
                                Produced = s.Silence,
                                DelayMs = (int)(time / 10000 - s.LastPacketEndPts * 1000 / 44100),
                                DelayInputMs = (int)(time / 10000 - s.LastInputPts * 1000 / 44100)
                            };
                            s.Dropped = 0;
                            s.Silence = 0;

                            return res;
                        }).ToArray()
                    }
                };
            }
        }
    }

    public class AudioMixingQueueInput
    {
        public int Id { get; set; }

        public long LastPacketPts { get; set; }

        public long LastPacketEndPts { get; set; }

        public int Dropped { get; set; }

        public long[] Deltas { get; } = new long[70];

        public int DeltasCount { get; set; }

        public int Silence { get; set; }

        public long LastInputPts { get; internal set; }

        public long LastCurrentTimePts { get; internal set; }
    }
}
