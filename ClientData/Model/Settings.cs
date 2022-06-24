
namespace Streamster.ClientData.Model
{
    public interface ISettings
    {
        int TargetFilter { get; set; }

        int Bitrate { get; set; }

        int Fps { get; set; }

        Resolution Resolution { get; set; }

        StreamingToCloudBehavior StreamingToCloud { get; set; }

        bool StreamingToCloudStarted { get; set; }

        EncoderType EncoderType { get; set; }

        EncoderQuality EncoderQuality { get; set; }

        bool PreferNalHdr { get; set; }

        bool DisableQsvNv12Optimization { get; set; }

        bool IsRecordingRequested { get; set; }

        bool NoStreamWithoutVpn { get; set; }

        string SelectedScene { get; set; }

        RecordingFormat RecordingFormat { get; set; }

        string UserFeedback { get; set; }

        bool ResetKeys { get; set; }
    }

    public enum RecordingFormat
    {
        Flv,
        Mp4
    }

    public enum StreamingToCloudBehavior
    {
        AppStart,
        FirstChannel,
        Manually
    }

    public enum EncoderType
    {
        Auto,
        Hardware,
        Software,
    }

    public enum EncoderQuality
    {
        Speed = -1,
        Balanced,
        BalancedQuality,
        Quality
    }

    public class VpnData
    {
        public string User { get; set; }

        public string Pwd { get; set; }

        public string Url { get; set; }
    }



    public class Resolution
    {
        public Resolution(int width, int height)
        {
            Height = height;
            Width = width;
        }

        public int Height { get; set; }

        public int Width { get; set; }

        public override bool Equals(object obj)
        {
            return obj is Resolution resolution &&
                   Height == resolution.Height &&
                   Width == resolution.Width;
        }

        public override int GetHashCode() => Height * Width;

        public override string ToString() => Width == 0 ? "Custom" : $"{Width} x {Height}";
    }
}
