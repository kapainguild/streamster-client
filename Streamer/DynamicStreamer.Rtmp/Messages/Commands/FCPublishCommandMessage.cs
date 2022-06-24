using Harmonic.Networking.Rtmp.Messages;
using Harmonic.Networking.Rtmp.Messages.Commands;
using Harmonic.Networking.Rtmp.Serialization;

namespace RtmpProtocol.Messages.Commands
{
    internal class FCPublishCommandMessage : CommandMessage
    {
        [OptionalArgument]
        public string PublishingName { get; set; }

        public FCPublishCommandMessage(AmfEncodingVersion encoding) : base("FCPublish", encoding)
        {
        }

        public override string ToString() => $"FCPublish({PublishingName})";
    }
}
