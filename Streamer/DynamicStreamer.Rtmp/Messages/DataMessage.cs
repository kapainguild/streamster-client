﻿using Harmonic.Networking.Rtmp.Data;
using Harmonic.Networking.Rtmp.Serialization;
using Harmonic.Networking.Rtmp.Messages;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Linq;

namespace Harmonic.Networking.Rtmp.Messages
{
    public class DataMessage : Message
    {
        public List<object> Data { get; set; }

        public DataMessage(AmfEncodingVersion encoding) : base(encoding == AmfEncodingVersion.Amf0 ? MessageType.Amf0Data : MessageType.Amf3Data)
        {
        }

        public override void Deserialize(SerializationContext context)
        {
            Data = new List<object>();
            var span = context.ReadBuffer.Span;
            if (MessageHeader.MessageType == MessageType.Amf0Data)
            {
                while (span.Length != 0)
                {
                    if (!context.Amf0Reader.TryGetValue(span, out _, out var data, out var consumed))
                    {
                        throw new ProtocolViolationException();
                    }
                    Data.Add(data);
                    span = span.Slice(consumed);
                }

            }
            else
            {
                while (span.Length != 0)
                {
                    if (!context.Amf3Reader.TryGetValue(span, out var data, out var consumed))
                    {
                        throw new ProtocolViolationException();
                    }
                    Data.Add(data);
                    span = span.Slice(consumed);
                }
            }

        }

        public override void Serialize(SerializationContext context)
        {
            if (MessageHeader.MessageType == MessageType.Amf0Data)
            {
                var sc = new Amf.Serialization.Amf0.SerializationContext(context.WriteBuffer);
                foreach (var data in Data)
                {
                    context.Amf0Writer.WriteValueBytes(data, sc);
                }
            }
            else
            {
                var sc = new Amf.Serialization.Amf3.SerializationContext(context.WriteBuffer);
                foreach (var data in Data)
                {
                    context.Amf3Writer.WriteValueBytes(data, sc);
                }
            }
        }

        public override string ToString() => "Data(" + String.Join("; ", Data.Select(s => TranslateData(s))) + ")";

        private string TranslateData(object s)
        {
            if (s is Dictionary<string, object> dict)
                return String.Join(", ", dict.Select(s => $"{s.Key}={s.Value}"));
            return s.ToString();
        }
    }
}
