using Streamster.ClientData.Model;
using System.Collections.Generic;
using System.Linq;

namespace Streamster.ClientCore.Models
{

    public class DeviceIndicatorsModel
    {
        public string DeviceId { get; set; }

        public Property<bool> Offline { get; } = new Property<bool>();

        public Property<string> Name { get; } = new Property<string>();

        public IndicatorModelCpu Cpu { get; } = new IndicatorModelCpu() { Name = "CPU load" };

        public IndicatorModelInput Input { get; } = new IndicatorModelInput() { Name = "Audio-video input state" };

        public IndicatorModelEncoder Encoder { get; } = new IndicatorModelEncoder() { Name = "Encoder load" };

        public IndicatorModelCloudOut CloudOut { get; } = new IndicatorModelCloudOut() { Name = "Stream to cloud state" };

        public IndicatorModelCloudOut CloudIn { get; } = new IndicatorModelCloudIn() { Name = "Stream from cloud state" };

        public IndicatorModelRestream Restream { get; } = new IndicatorModelRestream() { Name = "Stream from cloud state" };
    }

    public class IndicatorModelBase
    {
        public string Name { get; set; }

        public Property<string> Value { get; } = new Property<string>();

        public Property<IndicatorState> State { get; } = new Property<IndicatorState>();

        public Property<string> DetailedDescription { get; } = new Property<string>();

        public ChartModel ChartModel { get; } = new ChartModel();

        public virtual void Reset()
        {
            State.Value = IndicatorState.Unknown;
            Value.Value = "";
            DetailedDescription.Value = "Status unknown";
            ChartModel.Clear();
        }
    }


    public class IndicatorModelRestream : IndicatorModelBase
    {
        public Property<IndicatorModelRestreamChannel[]> Channels { get; } = new Property<IndicatorModelRestreamChannel[]>();
    }

    public class IndicatorModelRestreamChannel
    {
        public string Name { get; set; }

        public int Bitrate { get; set; }

        public ChannelState State { get; set; }
    }

    public class IndicatorModelCpu : IndicatorModelBase
    {
        public Property<ProcessLoad[]> Processes { get; } = new Property<ProcessLoad[]>();

        public AverageIntValue AverageCpu { get; } = new AverageIntValue(3, false);

        public override void Reset()
        {
            base.Reset();
            Processes.Value = null;
            AverageCpu.Clear();
        }
    }

    public class IndicatorModelEncoder : IndicatorModelBase
    {
    }

    public class IndicatorModelInput : IndicatorModelBase
    {
    }

    public class IndicatorModelCloudIn : IndicatorModelCloudOut
    {
    }

    public class IndicatorModelCloudOut : IndicatorModelBase
    {
        public Property<string> SmallValue { get; } = new Property<string>();

        public AverageIntValue AverageBitrate { get; } = new AverageIntValue(3, true);

        public override void Reset()
        {
            base.Reset();
            SmallValue.Value = null;
            AverageBitrate.Clear();
        }
    }

    public class AverageIntValue
    {
        private readonly int _count;
        private readonly bool _optimistic;
        private LinkedList<int> _values = new LinkedList<int>();

        public AverageIntValue(int count, bool optimistic)
        {
            _count = count;
            _optimistic = optimistic;
        }

        public int AddValue(int value)
        {
            _values.AddLast(value);
            if (_values.Count > _count)
            {
                _values.RemoveFirst();
            }
            return GetAverage();
        }

        public bool Any() => _values.Count > 0;

        public int GetAverage()
        {
            if (_optimistic)
            {
                var threshold = (int)(_values.Last.Value * 0.75);
                int sum = 0;
                int count = 0;
                var next = _values.Last;
                while (next != null && next.Value >= threshold)
                {
                    sum += next.Value;
                    count++;
                    next = next.Previous;
                }
                return sum / count;

            }
            else return (int)_values.Average();
        }

        public bool TryGetAverage(out int ave)
        {
            if (Any())
            {
                ave = GetAverage();
                return true;
            }
            ave = 0;
            return false;
        }

        public bool TryGetLast(out int last)
        {
            if (Any())
            {
                last = _values.Last();
                return true;
            }
            last = 0;
            return false;
        }

        public void Clear() => _values.Clear();
    }



    public enum IndicatorState
    {
        Unknown,
        Ok,
        Warning,
        Error
    }

}
