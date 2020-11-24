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

    public interface IIndicatorBase
    {
        bool Enabled { get; set; }
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

        int Errors { get; set; } 

        int Drops { get; set; }
    }

    public interface IIndicatorCloudIn : IIndicatorBase
    {
        int Bitrate { get; set; }

        int Errors { get; set; }
    }

    public interface IIndicatorEncoder : IIndicatorBase
    {
        int QueueSize { get; set; }

        int InputErrors { get; set; }

        int InputFps { get; set; }

        int InputTargetFps { get; set; }
    }

    public interface IIndicatorVpn : IIndicatorBase
    {
        int Sent { get; set; }

        int Received { get; set; }

        VpnState State { get; set; }
    }
}
