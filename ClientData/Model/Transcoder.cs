namespace Streamster.ClientData.Model
{
    public interface ITranscoder
    {
        int Bitrate { get; set; }

        int Fps { get; set; }

        Resolution Resolution { get; set; }
    }
}
