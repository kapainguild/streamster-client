
namespace Streamster.ClientData.Model
{
    public interface IInput
    {
        string Name { get; set; }

        string Owner { get; set; }

        InputType Type { get; set; }

        InputState State { get; set; }
    }

    public interface IAudioInput : IInput
    {
    }

    public interface IVideoInput : IInput
    {
        VideoInputPreview Preview { get; set; }

        VideoFilters Filters { get; set; }

        VideoInputCapabilities Capabilities { get; set; }
    }

    public class VideoInputPreview
    {
        public int W { get; set; }

        public int H { get; set; }

        public byte[] Data { get; set; }
    }

    public enum InputState
    {
        Unknown = 0,

        Ready = 1,
        InUseByOtherApp = 2,
        Removed = 3,
        Failed = 4,
        ObsIsNotStarted = 5,
        Running = 6,

        RemoteDeviceOffline = 10,
        RemoteDeviceInactive = 11,
    }

    public enum InputType
    {
        USB = 0,
        Virtual = 1,
        Remote = 2
    }

    public class VideoInputCapabilities
    {
       public  VideoInputCapability[] Caps { get; set; }
    }

    public class VideoInputCapability
    {
        public int W { get; set; }
        public int H { get; set; }
        public int MinF { get; set; }
        public int MaxF { get; set; }
        public VideoInputCapabilityFormat Fmt {get;set;}

        public override bool Equals(object obj)
        {
            return obj is VideoInputCapability capability &&
                   W == capability.W &&
                   H == capability.H &&
                   MinF == capability.MinF &&
                   MaxF == capability.MaxF &&
                   Fmt == capability.Fmt;
        }

        public VideoInputCapability GetClone() => new VideoInputCapability
        {
            H = H,
            W = W,
            MinF = MinF,
            MaxF = MaxF,
            Fmt = Fmt
        };

        public override int GetHashCode() => W + H + MinF + MaxF + (int)Fmt;

        public override string ToString() => $"{Fmt}.{W}x{H} {MinF}-{MaxF}";
        
    }

    public enum VideoInputCapabilityFormat
    {
        Raw = 0,
        Empty = 1,
        MJpeg = 2,
        Unknown = 3,
        H264 = 4,
    }

}
