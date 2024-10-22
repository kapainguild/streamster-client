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

        TargetMode TargetMode { get; set; }

        AutoLoginState AutoLoginState { get; set; }

        bool Temporary { get; set; }

        string BitrateWarning { get; set; }
    }

    public enum ChannelState
    {
        Idle,
        RunningOk,
        RunningInitError,
        RunningConnectError,
        RunningNotAuthenticated
    }

    public enum TargetMode
    {
        ManualKey = 0,
        AutoLogin = 1
    }

    public enum AutoLoginState
    {
        Unknown = 0,
        InProgress = 1,
        Authenticated = 2,
        NotAuthenticated = 3,
        KeyObtained = 4,
        KeyNotFound = 5
    }
}
