using System;

namespace DynamicStreamer.Contexts
{
    public interface IOutputContext : IDisposable
    {
        bool IsOpened { get; }
           
        void Open(OutputSetup setup);

        void UpdateSetup(OutputSetup setup);

        ErrorCodes Write(Packet packet, int stream, OutputSetup setup);

        void CloseOutput();

        void Interrupt();

        bool SetupEquals(OutputSetup oldSetup, OutputSetup newSetup);
    }
}
