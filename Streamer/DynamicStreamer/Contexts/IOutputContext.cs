using System;

namespace DynamicStreamer.Contexts
{
    public interface IOutputContext : IDisposable
    {
        bool IsOpened { get; }
           
        void Open(OutputSetup setup);

        void UpdateSetup(OutputSetup setup);

        ErrorCodes Write(Packet packet, int stream);

        void CloseOutput();

        void Interrupt();
    }
}
