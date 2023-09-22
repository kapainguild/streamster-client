using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicStreamer.Nodes
{
    public class StatisticData
    {
        public int Errors { get; set; }

        public override string ToString() => (Errors > 0 ? $"E{Errors} " : "");

    }

    public enum InputErrorType
    {
        None = 0,
        Error = 1,
        InUse = 2,
        GracefulClose = 3, // these values are used in different places as int, so search for name like GracefulClose
        ExceedingLimit = 4,
    }

    public class StatisticDataOfInputOutput : StatisticData
    {
        public int Frames { get; set; }

        public int Fps { get; set; }

        public int Bytes { get; set; }

        public string Other { get; set; }

        public InputErrorType ErrorType { get; set; }

        public override string ToString() => (base.ToString() + (Frames > 0 ? $"F{Frames} B{Bytes * 8 / 1024} " : "")).TrimEnd();

        public void AddPacket(int packetSize, bool video = true)
        {
            if (video)
                Fps++;
            Frames++;
            Bytes += packetSize;
        }

        public void AddError(InputErrorType type)
        {
            ErrorType = type;
            Errors++;
        }
    }

    public class StatisticDataOfProcessingNode : StatisticData
    {
        public int InFrames { get; set; }

        public int OutFrames { get; set; }

        public override string ToString() => (base.ToString() + (InFrames > 0 ? $"I{InFrames} " : "") + (OutFrames > 0 ? $"O{OutFrames} " : "")).TrimEnd();
    }

    public class StatisticDataOfFpsQueue : StatisticData
    {
        public int Dropped { get; set; }

        public override string ToString() => base.ToString() + (Dropped > 0 ? $"R{Dropped}" : "");
    }

    public class StatisticDataOfAudioMixerQueue_Input
    {
        public int Dropped { get; set; }
        public int Produced { get; set; }
        public int DelayMs { get; set; }
        public int DelayInputMs { get; set; }

        public override string ToString()
        {
            if (Dropped != 0 || Produced != 0)
                return $"R{Dropped}:S{Produced}:{DelayMs}ms:{DelayInputMs}ms";
            return $"{DelayMs}ms:{DelayInputMs}ms";
        }
    }

    public class StatisticDataOfAudioMixerQueue : StatisticData
    {
        public StatisticDataOfAudioMixerQueue_Input[] Inputs { get; set; }

        public override string ToString() => string.Join("; ", Inputs.Select(s => s.ToString()));
    }

    public class StatisticDataOfBlenderNode_Input
    {
        public int QueueSize { get; set; }

        public long Delay { get; set; }

        public int InFrames { get; set; }

        public override string ToString()
        {
            if (QueueSize != 0 || Delay != 0 || InFrames != 0)
                return $"I{InFrames}:Q{QueueSize}:D{Delay/10_000}ms";
            return "-";
        }
    }

    public class StatisticDataOfBlenderNode : StatisticData
    {
        StatisticDataOfBlenderNode_Input[] _items = new StatisticDataOfBlenderNode_Input[5];

        public StatisticDataOfBlenderNode_Input GetInput(int q)
        {
            if (q < 5)
            {
                if (_items[q] == null)
                    _items[q] = new StatisticDataOfBlenderNode_Input();
                return _items[q];
            }
            else
                return new StatisticDataOfBlenderNode_Input();
        }

        public int OutFrames { get; set; }

        public override string ToString() 
        {
            var inputs = string.Join("; ", _items.Where(s => s != null)) + " => ";
            return (base.ToString() + inputs + (OutFrames > 0 ? $"O{OutFrames} " : "")).TrimEnd();
        }
    }


    public class StatisticItem
    {
        public NodeName Name { get; set; }

        public double DurationMs { get; set; }

        public StatisticData Data { get; set; }
    }

    public class StatisticKeeper<TData> where TData : StatisticData, new()
    {
        private readonly NodeName _name;
        private DateTime _last;
        private TData _value;

        public StatisticKeeper(NodeName name)
        {
            _name = name;
            _last = DateTime.UtcNow;
            _value = new TData();
        }

        public TData Data => _value;

        public StatisticItem Get()
        {
            lock (this)
            {
                DateTime now = DateTime.UtcNow;

                var res = new StatisticItem
                {
                    Name = _name,
                    DurationMs = (now - _last).TotalMilliseconds,
                    Data = _value
                };

                _value = new TData();
                _last = now;

                return res;
            }
        }

        public void UpdateData(Action<TData> dataUpdater)
        {
            lock (this)
            {
                dataUpdater(_value);
            }
        }
    }
}
