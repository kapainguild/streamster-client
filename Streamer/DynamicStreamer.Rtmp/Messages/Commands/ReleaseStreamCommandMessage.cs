using Harmonic.Networking.Rtmp.Messages;
using Harmonic.Networking.Rtmp.Messages.Commands;
using Harmonic.Networking.Rtmp.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace RtmpProtocol.Messages.Commands
{
    public class ReleaseStreamCommandMessage : CommandMessage
    {
        [OptionalArgument]
        public string PublishingName { get; set; }

        public ReleaseStreamCommandMessage(AmfEncodingVersion encoding) : base("releaseStream", encoding)
        {
        }

        public override string ToString() => $"ReleaseStream({PublishingName})";
    }
}
