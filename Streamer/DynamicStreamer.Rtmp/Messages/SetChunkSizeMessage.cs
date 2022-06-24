﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Harmonic.Networking.Rtmp.Data;
using Harmonic.Networking.Rtmp.Serialization;
using Harmonic.Networking.Utils;

namespace Harmonic.Networking.Rtmp.Messages
{
    public class SetChunkSizeMessage : ControlMessage
    {
        public uint ChunkSize { get; set; }

        public SetChunkSizeMessage() : base(MessageType.SetChunkSize)
        {
            
        }

        public override void Deserialize(SerializationContext context)
        {
            var chunkSize = NetworkBitConverter.ToInt32(context.ReadBuffer.Span);
            ChunkSize = (uint)chunkSize;
        }

        public override void Serialize(SerializationContext context)
        {
            var buffer = _arrayPool.Rent(sizeof(uint));
            try
            {
                NetworkBitConverter.TryGetBytes(ChunkSize, buffer);
                context.WriteBuffer.WriteToBuffer(buffer.AsSpan(0, sizeof(uint)));
            }
            finally
            {
                _arrayPool.Return(buffer);
            }
        }

        public override string ToString() => $"SetChunkSize({ChunkSize})";
    }
}
