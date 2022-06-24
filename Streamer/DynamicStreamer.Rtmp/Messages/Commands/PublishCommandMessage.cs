﻿using Harmonic.Networking.Rtmp.Serialization;
using Harmonic.Networking.Rtmp.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Harmonic.Networking.Rtmp.Messages.Commands
{
    public class PublishCommandMessage : CommandMessage
    {
        [OptionalArgument]
        public string PublishingName { get; set; }
        [OptionalArgument]
        public string PublishingType { get; set; }

        public PublishCommandMessage(AmfEncodingVersion encoding) : base("publish", encoding)
        {
        }

        public override string ToString() => $"Publish({PublishingName})";
    }
}
