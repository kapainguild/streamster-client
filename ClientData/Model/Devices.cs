
using System.Collections.Generic;

namespace Streamster.ClientData.Model
{
    public interface IDevice
    {
        string Name { get; set; }

        DeviceState State { get; set; }

        bool PreviewVideo { get; set; } // remove

        bool PreviewAudio { get; set; }// remove

        RequireOutgestType RequireOutgestType { get; set; }

        bool RequireOutgest { get; set; }

        bool DisplayVideoHidden { get; set; }

        string AssignedOutgest { get; set; }

        IDeviceSettings DeviceSettings { get; set; }

        IDeviceIndicators KPIs { get; set; }

        VpnData VpnData { get; set; }

        bool VpnRequested { get; set; }

        VpnState VpnState { get; set; }

        string VpnServerIpAddress { get; set; }

        bool DisconnectRequested { get; set; }

        IDictionary<string, IInputDevice> VideoInputs { get; set; }

        IDictionary<string, IInputDevice> AudioInputs { get; set; }

        CaptureSource[] Displays { get; set; }

        CaptureSource[] Windows { get; set; }

        int ApiContract { get; set; }

        int PluginFlags { get; set; }

        bool PreviewSources { get; set; }

        bool PreviewAudioSources { get; set; }
    }

    public enum PluginFlags
    {
        Lovense = 1
    }

    public enum RequireOutgestType
    {
        Rtmp,
        Tcp
    }

    public enum DeviceState
    {
        Inactive,
        Offline,
        Online
    }
}
