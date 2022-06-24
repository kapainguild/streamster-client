﻿using Harmonic.Networking.Rtmp.Serialization;
using Harmonic.Networking.Rtmp.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Harmonic.Networking.Rtmp.Messages.Commands
{
    public class Play2CommandMessage : CommandMessage
    {
        [OptionalArgument]
        public object Parameters { get; set; }

        public Play2CommandMessage(AmfEncodingVersion encoding) : base("play2", encoding)
        {
        }
    }
}
