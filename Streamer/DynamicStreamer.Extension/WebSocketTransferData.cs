
using System.Net.Sockets;

namespace DynamicStreamer.Extension
{
    public class WebSocketState
    {
        public string PublishingName { get; set; }

        public string[] Headers { get; set; }

        public string Body { get; set; }
    }

    public class WebSocketTransferData
    {
        public WebSocketState State { get; set; }

        public SocketInformation SocketInformation { get; set; }

        public int Index { get; set; }
    }
}
