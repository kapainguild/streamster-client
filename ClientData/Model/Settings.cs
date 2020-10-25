
namespace Streamster.ClientData.Model
{
    public interface ISettings
    {
        int TargetFilter { get; set; }

        string RequestedVideo { get; set; }

        string SelectedVideo { get; set; }

        string RequestedAudio { get; set; }

        string SelectedAudio { get; set; }

        int Bitrate { get; set; }

        int Fps { get; set; }

        Resolution Resolution { get; set; }

        StreamingToCloudBehavior StreamingToCloud { get; set; }

        bool StreamingToCloudStarted { get; set; }

        EncoderType EncoderType { get; set; }

        EncoderQuality EncoderQuality { get; set; }

        bool IsRecordingRequested { get; set; }
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

        public override string ToString() => $"{Width} x {Height}";
    }
}
