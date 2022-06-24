
using System.Collections.Generic;

namespace Streamster.ClientData.Model
{
    public interface IDevice
    {
        string Name { get; set; }

        string Type { get; set; }

        DeviceState State { get; set; }

        RequireOutgestType RequireOutgestType { get; set; }

        bool RequireOutgest { get; set; }

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
        Lovense = 1,
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
