using Harmonic.Networking.Rtmp.Serialization;
using Harmonic.Networking.Rtmp.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Harmonic.Networking.Rtmp.Messages.Commands
{
    public class SeekCommandMessage : CommandMessage
    {
        [OptionalArgument]
        public double MilliSeconds { get; set; }

        public SeekCommandMessage(AmfEncodingVersion encoding) : base("seek", encoding)
        {
        }
    }
}
