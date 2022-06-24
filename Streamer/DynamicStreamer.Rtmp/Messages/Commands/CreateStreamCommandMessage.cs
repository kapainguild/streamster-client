using System;
using System.Collections.Generic;
using System.Text;
using Harmonic.Networking.Rtmp.Serialization;
using Harmonic.Networking.Rtmp.Messages;

namespace Harmonic.Networking.Rtmp.Messages.Commands
{
    public class CreateStreamCommandMessage : CommandMessage
    {
        public CreateStreamCommandMessage(AmfEncodingVersion encoding) : base("createStream", encoding)
        {
        }

        public override string ToString() => $"CreateStream()";
    }


    
}
