using Harmonic.Networking.Rtmp.Serialization;
using Harmonic.Networking.Rtmp.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Harmonic.Networking.Rtmp.Messages.Commands
{
    public class PauseCommandMessage : CommandMessage
    {
        [OptionalArgument]
        public bool IsPause { get; set; }
        [OptionalArgument]
        public double MilliSeconds { get; set; }

        public PauseCommandMessage(AmfEncodingVersion encoding) : base("pause", encoding)
        {
        }
    }
}
