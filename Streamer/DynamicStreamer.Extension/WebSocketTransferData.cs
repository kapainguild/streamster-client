
using System.Net.Sockets;

namespace DynamicStreamer.Extension
{
    public class WebSocketTransferData
    {
        public WebSocketBootstrapState State { get; set; }

        public Socket Socket { get; set; }

        public string Service { get; set; }

        public int Index { get; set; }

        public override bool Equals(object obj)
        {
            return obj is WebSocketTransferData data &&
                   Index == data.Index;
        }

        public override int GetHashCode() => base.GetHashCode();
    }

    public class WebSocketBootstrapState
    {
        public string PublishingName { get; set; }

        public string[] Headers { get; set; }

        public string Body { get; set; }

        public byte[] Bytes { get; set; }
    }
}
