using Harmonic.Networking.Rtmp.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.Rtmp
{
    internal class NetStream
    {
        public NetStreamState State { get; }

        public NetStream(uint chunkStreamId, uint messageStreamId) : 
            this(new NetStreamState { ChunkStreamId = chunkStreamId, MessageStreamId = messageStreamId })
        {
        }

        public NetStream(NetStreamState state)
        {
            State = state;
        }
    }

    

}
