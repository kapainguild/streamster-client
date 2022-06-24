using Harmonic.Networking.Rtmp.Serialization;
using Harmonic.Networking.Rtmp.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Harmonic.Networking.Rtmp.Messages.Commands
{
    public class DeleteStreamCommandMessage : CommandMessage
    {
        [OptionalArgument]
        public double StreamID { get; set; }

        public DeleteStreamCommandMessage(AmfEncodingVersion encoding) : base("deleteStream", encoding)
        {
        }

        public override string ToString() => $"DeleteStream({StreamID})";
    }
}
