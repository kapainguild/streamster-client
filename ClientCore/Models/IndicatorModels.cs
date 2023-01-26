using Streamster.ClientCore.Support;
using Streamster.ClientData.Model;
using System.Collections.Generic;
using System.Linq;

namespace Streamster.ClientCore.Models
{

    public class DeviceIndicatorsModel
    {
        public string DeviceId { get; set; }

        public Property<DeviceIndicatorsState> Offline { get; } = new Property<DeviceIndicatorsState>();

        public Property<string> Name { get; } = new Property<string>();

        public IndicatorModelCpu Cpu { get; } = new IndicatorModelCpu() { Name = "CPU load" };

        public IndicatorModelEncoder Encoder { get; } = new IndicatorModelEncoder() { Name = "Encoder load" };

        public IndicatorModelCloudOut CloudOut { get; } = new IndicatorModelCloudOut() { Name = "Stream to cloud state" };

        public IndicatorModelCloudOut CloudIn { get; } = new IndicatorModelCloudIn() { Name = "Stream from cloud state" };

        public IndicatorModelRestream Restream { get; } = new IndicatorModelRestream() { Name = "Restreaming state" };

        public IndicatorModelVpn Vpn { get; } = new IndicatorModelVpn();
    }

    public enum DeviceIndicatorsState
    {
        Online,
        Offline,
        Indicators
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
            State.Value = IndicatorState.Disabled;
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

        public override void Reset()
        {
            base.Reset();
            Processes.Value = null;
        }
    }

    public class IndicatorModelEncoder : IndicatorModelBase
    {
        public ChartModel OutputFps { get; } = new ChartModel();
    }

    public class IndicatorModelInput : IndicatorModelBase
    {
    }

    public class IndicatorModelVpn : IndicatorModelBase
    {
        public ChartModel Received { get; } = new ChartModel();

        public override void Reset()
        {
            base.Reset();
            Received.Clear();
        }
    }

    public class IndicatorModelCloudOut : IndicatorModelBase
    {
        public Property<string> SmallValue { get; } = new Property<string>();

        public override void Reset()
        {
            base.Reset();
            SmallValue.Value = null;
        }
    }

    public class IndicatorModelCloudIn : IndicatorModelCloudOut
    {
    }
}
