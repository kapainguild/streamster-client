using Harmonic.Networking.Amf.Serialization.Amf0;
using Harmonic.Networking.Amf.Serialization.Amf3;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.Rtmp
{
    public class AmfSerializationContext
    {
        public Amf0Reader Amf0Reader { get; } = new Amf0Reader();
        public Amf0Writer Amf0Writer { get; } = new Amf0Writer();
        public Amf3Reader Amf3Reader { get; } = new Amf3Reader();
        public Amf3Writer Amf3Writer { get; } = new Amf3Writer();
    }
}
