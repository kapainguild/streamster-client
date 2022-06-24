using Harmonic.Networking.Rtmp.Data;
using Harmonic.Networking.Rtmp.Serialization;
using Harmonic.Networking.Rtmp.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Harmonic.Networking.Rtmp.Messages.Commands
{
    public class OnStatusCommandMessage : CommandMessage
    {
        [OptionalArgument]
        public object InfoObject { get; set; }

        public OnStatusCommandMessage(AmfEncodingVersion encoding) : base("onStatus", encoding)
        {
        }

        public override string ToString() => $"OnStatus({InfoObject})";
    }
}
