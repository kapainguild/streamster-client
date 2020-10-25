
namespace Streamster.ClientData.Model
{
    public interface IDevice
    {
        string Name { get; set; }

        DeviceState State { get; set; }

        bool PreviewVideo { get; set; }

        bool PreviewAudio { get; set; }

        bool RequireOutgest { get; set; }

        bool DisplayVideoHidden { get; set; }

        string AssignedOutgest { get; set; }

        IDeviceSettings DeviceSettings { get; set; }

        IDeviceIndicators KPIs { get; set; }
    }

    public enum DeviceState
    {
        Inactive,
        Offline,
        Online
    }
}
