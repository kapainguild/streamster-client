﻿using Harmonic.Networking.Rtmp.Data;
using Harmonic.Networking.Rtmp.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace Harmonic.Networking.Rtmp.Messages
{
    public sealed class VideoMessage : Message, ICloneable
    {
        public VideoMessage() : base(MessageType.VideoMessage)
        {

        }

        public VideoMessage(MessageHeader messageHeader) : base(messageHeader)
        {
        }

        public ReadOnlyMemory<byte> Data { get; private set; }

        public object Clone()
        {
            var ret = new VideoMessage((MessageHeader)MessageHeader.Clone());
            ret.MessageHeader.MessageStreamId = null;
            ret.Data = Data;
            return ret;
        }

        public override void Deserialize(SerializationContext context)
        {
            // TODO: optimize performance
            var data = new byte[context.ReadBuffer.Length];
            context.ReadBuffer.Span.Slice(0, (int)MessageHeader.MessageLength).CopyTo(data);
            Data = data;
        }

        public override void Serialize(SerializationContext context)
        {
            context.WriteBuffer.WriteToBuffer(Data.Span.Slice(0, Data.Length));
        }

    }
}
