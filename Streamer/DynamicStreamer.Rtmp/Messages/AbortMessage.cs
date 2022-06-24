﻿using Harmonic.Networking.Rtmp.Data;
using Harmonic.Networking.Rtmp.Serialization;
using Harmonic.Networking.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Harmonic.Networking.Rtmp.Messages
{
    public class AbortMessage : ControlMessage
    {
        public uint AbortedChunkStreamId { get; set; }

        public AbortMessage() : base(MessageType.AbortMessage)
        {
        }

        public override void Deserialize(SerializationContext context)
        {
            AbortedChunkStreamId = NetworkBitConverter.ToUInt32(context.ReadBuffer.Span);
        }

        public override void Serialize(SerializationContext context)
        {
            var buffer = _arrayPool.Rent(sizeof(uint));
            try
            {
                NetworkBitConverter.TryGetBytes(AbortedChunkStreamId, buffer);
                context.WriteBuffer.WriteToBuffer(buffer);
            }
            finally
            {
                _arrayPool.Return(buffer);
            }
        }
    }
}
