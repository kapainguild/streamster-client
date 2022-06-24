﻿using Harmonic.Networking.Rtmp.Data;
using Harmonic.Networking.Rtmp.Serialization;
using Harmonic.Networking.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Harmonic.Networking.Rtmp.Messages
{
    internal class MessageData
    {
        public MessageHeader Header { get; set; }
        public int DataOffset { get; set; }
        public uint DataLength { get; set; }
    }

    internal class AggregateMessage : Message
    {
        public List<MessageData> Messages { get; set; } = new List<MessageData>();
        public byte[] MessageBuffer { get; set; } = null;

        public AggregateMessage() : base(MessageType.AggregateMessage)
        {
        }

        public AggregateMessage(MessageHeader messageHeader) : base(messageHeader)
        {
        }

        private MessageData DeserializeMessage(Span<byte> buffer, out int consumed)
        {
            consumed = 0;
            var header = new MessageHeader();
            header.MessageType = (MessageType)buffer[0];
            buffer = buffer.Slice(sizeof(byte));
            consumed += sizeof(byte);
            header.MessageLength = NetworkBitConverter.ToUInt24(buffer);
            buffer = buffer.Slice(3);
            consumed += 3;
            header.Timestamp = NetworkBitConverter.ToUInt32(buffer);
            buffer = buffer.Slice(sizeof(uint));
            consumed += sizeof(uint);
            header.MessageStreamId = header.MessageStreamId;
            // Override message stream id
            buffer = buffer.Slice(3);
            consumed += 3;
            var offset = consumed;
            consumed += (int)header.MessageLength;

            header.Timestamp += MessageHeader.Timestamp;

            return new MessageData()
            {
                Header = header,
                DataOffset = offset,
                DataLength = header.MessageLength
            };
        }

        public override void Deserialize(SerializationContext context)
        {
            var spanBuffer = context.ReadBuffer.Span;
            while (spanBuffer.Length != 0)
            {
                Messages.Add(DeserializeMessage(spanBuffer, out var consumed));
                spanBuffer = spanBuffer.Slice(consumed + /* back pointer */ 4);
            }
        }

        public override void Serialize(SerializationContext context)
        {
            int bytesNeed = (int)(Messages.Count * 11 + Messages.Sum(m => m.DataLength));
            var buffer = _arrayPool.Rent(bytesNeed);
            try
            {
                var span = buffer.AsSpan(0, bytesNeed);
                int consumed = 0;
                foreach (var message in Messages)
                {
                    span[0] = (byte)message.Header.MessageType;
                    span = span.Slice(sizeof(byte));
                    NetworkBitConverter.TryGetUInt24Bytes((uint)message.Header.MessageLength, span);
                    span = span.Slice(3);
                    NetworkBitConverter.TryGetBytes(message.Header.Timestamp, span);
                    span = span.Slice(4);
                    NetworkBitConverter.TryGetUInt24Bytes((uint)MessageHeader.MessageStreamId, span);
                    span = span.Slice(3);
                    MessageBuffer.AsSpan(consumed, (int)message.Header.MessageLength).CopyTo(span);
                    consumed += (int)message.Header.MessageLength;
                    span = span.Slice((int)message.Header.MessageLength);
                }
                context.WriteBuffer.WriteToBuffer(span);
            }
            finally
            {
                _arrayPool.Return(buffer);
            }
        }
    }
}
