
namespace Streamster.ClientData.Model
{
    public interface IIngest
    {
        IngestType Type { get; set; }

        IngestData Data { get; set; }

        string Owner { get; set; }

        int ResetCounter { get; set; }

        IIndicatorIngest In { get; set; }
    }

    public enum IngestType
    {
        External,
        TcpForDevice,
        RtmpForDevice,
        WebRtc,
    }

    public class IngestData
    {
        public string Type { get; set; }

        public string Output { get; set; }

        public string Options { get; set; }

        public int Port { get; set; }
    }

    public interface IOutgest
    {
        OutgestData Data { get; set; }
    }

    public class OutgestData
    {
        public RequireOutgestType RequireType { get; set; }

        public string Type { get; set; }

        public string Output { get; set; } 

        public string Options { get; set; }

        public string DeviceId { get; set; }

        public int Port { get; set; }
    }

    public interface IIndicatorIngest : IIndicatorBase
    {
        int Bitrate { get; set; }
    }

    public class WebRtcConstants
    {
        public const string WebRtcType = "webrtc";
        public const string WebRtcOptionTranscoder = "transcoder";
    }
}
