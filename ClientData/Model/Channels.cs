using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientData.Model
{
    public interface IChannel
    {
        string TargetId { get; set; }

        string Name { get; set; }

        string Key { get; set; }

        string WebUrl { get; set; }

        string RtmpUrl { get; set; }

        bool IsOn { get; set; }

        ChannelState State { get; set; }

        int Bitrate { get; set; }

        string Timer { get; set; }

        string TranscoderId { get; set; }
    }

    public enum ChannelState
    {
        Idle,
        RunningOk,
        RunningInitError,
        RunningConnectError,
    }
}
