using System.Linq;
using System.Net.Sockets;

namespace DynamicStreamer.Rtmp
{
    public class RtmpTransferData
    {
        public RtmpConnectionState State { get; set; }
        public SocketInformation SocketInformation { get; set; }

        public override bool Equals(object obj)
        {
            return obj is RtmpTransferData other &&
                SocketInformation.ProtocolInformation.SequenceEqual(other.SocketInformation.ProtocolInformation);
        }

        public override int GetHashCode() => 0;
    }

    public class RtmpConnectionState
    {
        public NetStreamState[] NetStreams { get; set; }
        public uint ConnectionChunkId { get; set; }
        public BufferState BufferState { get; set; }
        public PrevReadMessageState[] PrevReadMessageStates { get; set; }
    }

    public class PrevReadMessageState
    {
        public uint ChunkId { get; set; }
        public RtmpMessageHeader MessageHeader { get; set; }
    }

    public class NetStreamState
    {
        public uint MessageStreamId { get; set; }
        public uint ChunkStreamId { get; set; }
        public string PublishingName { get; set; }
        public int PublishingEncoding { get; set; } // 0 or 3
    }

    public class BufferState
    {
        public uint? ReadWindowAcknowledgementSize { get; set; } = null;
        public uint? WriteWindowAcknowledgementSize { get; set; } = null;
        public int ReadChunkSize { get; set; } = 128;
        public long ReadUnacknowledgedSize { get; set; } = 0;
        public long WriteUnacknowledgedSize { get; set; } = 0;
        public uint WriteChunkSize { get; set; } = 128;
    }

    public class RtmpMessageHeader
    {
        public uint Timestamp { get; set; }
        public uint MessageLength { get; set; }
        public int MessageType { get; set; } = 0;
        public uint? MessageStreamId { get; set; } = null;
    }
}
