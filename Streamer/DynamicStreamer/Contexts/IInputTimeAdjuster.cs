namespace DynamicStreamer.Contexts
{
    public interface IInputTimeAdjuster
    {
        long Add(long packetTime, long currentTime);
    }
}
