using DynamicStreamer.Rtmp;
using System;
using System.Collections.Generic;
using System.Text;

namespace Harmonic.Networking.Rtmp.Data
{
    public class MessageHeader: ICloneable
    {
        public MessageHeader() { }

        public MessageHeader(RtmpMessageHeader messageHeader)
        {
            Timestamp = messageHeader.Timestamp;
            MessageLength = messageHeader.MessageLength;
            MessageType = (MessageType)messageHeader.MessageType;
            MessageStreamId = messageHeader.MessageStreamId;
        }

        public RtmpMessageHeader ToMessageHeader() => new RtmpMessageHeader
        {
            Timestamp = this.Timestamp,
            MessageLength = this.MessageLength,
            MessageType = (int)this.MessageType,
            MessageStreamId = this.MessageStreamId
        };

        public uint Timestamp { get; set; }
        public uint MessageLength { get; set; }
        public MessageType MessageType { get; set; } = 0;
        public uint? MessageStreamId { get; set; } = null;

        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
