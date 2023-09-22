using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientData.Model
{
    public interface IDeviceIndicators
    {
        IIndicatorCpu Cpu { get; set; }

        IIndicatorCloudIn CloudIn { get; set; }

        IIndicatorCloudOut CloudOut { get; set; }

        IIndicatorEncoder Encoder { get; set; }

        IIndicatorVpn Vpn { get; set; }
    }

    public enum IndicatorState
    {
        Disabled,
        Ok,
        Warning,
        Warning2,
        Error,
        Error2,
        Warning3
    }

    public interface IIndicatorBase
    {
        IndicatorState State { get; set; }
    }


    public interface IIndicatorCpu : IIndicatorBase
    {
        int Load { get; set; }

        ProcessLoad[] Top { get; set; }
    }

    public class ProcessLoad
    {
        public string Name { get; set; }

        public int Load { get; set; }
    }

    public interface IIndicatorCloudOut : IIndicatorBase
    {
        int Bitrate { get; set; }

        int Fps { get; set; }

        int ResY { get; set; }
    }

    public interface IIndicatorCloudIn : IIndicatorBase
    {
        int Bitrate { get; set; }
    }

    public class EncoderData
    {
        public int Q { get; set; } // queue size

        public int O { get; set; } // output fps

        public int F { get; set; } // failed inputs
    }

    public interface IIndicatorEncoder : IIndicatorBase
    {
        EncoderData Data { get; set; }
    }

    public interface IIndicatorVpn : IIndicatorBase
    {
        int Sent { get; set; }

        int Received { get; set; }
    }
}
