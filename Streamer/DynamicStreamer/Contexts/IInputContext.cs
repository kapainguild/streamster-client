using DynamicStreamer.Rtmp;
using System;

namespace DynamicStreamer.Contexts
{
    public record InputSetup(string Type, string Input, string Options = "", RtmpTransferData RtmpTransferData = null, object ObjectInput = null, DirectXContext Dx = null, 
        InputSetupNoneResetingOptions NoneResetingOptions = null, int ExpectedNumberOfStreams = 1,
        AdjustInputType AdjustInputType = AdjustInputType.None, bool FirstStreamOnly = true, bool UseFpsQueue = true, 
        int BitrateLimit = 0)
    {
        public override string ToString() => $"{Type} {Input}" + ObjectInput ?? "";
    }

    public enum AdjustInputType { None, Adaptive, CurrentTime, AdaptiveNetwork }

    public class LoopbackOptions
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class InputSetupNoneResetingOptions
    {
        public LoopbackOptions LoopbackOptions { get; set;}

        public int Fps { get; set; }

        public override bool Equals(object obj)
        {
            return true;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    public record InputConfig(InputStreamProperties[] InputStreamProps);

    public interface IInputContext : IDisposable
    {
        InputConfig Config { get; }

        void Open(InputSetup setup);

        void Read(Packet packet, InputSetup inputSetup);

        void Analyze(int duration, int streamsCount);

        public void Interrupt();
    }

    public class GracefulCloseException : Exception { }

}
