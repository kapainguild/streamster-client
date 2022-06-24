using DynamicStreamer.Rtmp;
using Harmonic.Buffers;
using Harmonic.Networking.Amf.Serialization.Amf0;
using Harmonic.Networking.Amf.Serialization.Amf3;
using RtmpProtocol;
using System;
using System.Collections.Generic;
using System.Text;

namespace Harmonic.Networking.Rtmp.Serialization
{
    public class SerializationContext
    {
        public AmfSerializationContext AmfSerializationContext { get; internal set; } = null;
        public ByteBuffer WriteBuffer { get; internal set; } = null;
        public Memory<byte> ReadBuffer { get; internal set; } = null;


        public Amf0Reader Amf0Reader => AmfSerializationContext.Amf0Reader;
        public Amf0Writer Amf0Writer => AmfSerializationContext.Amf0Writer; 
        public Amf3Reader Amf3Reader => AmfSerializationContext.Amf3Reader; 
        public Amf3Writer Amf3Writer => AmfSerializationContext.Amf3Writer;

    }
}
