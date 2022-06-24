using Harmonic.Networking.Rtmp.Messages;
using Harmonic.Networking.Rtmp.Messages.Commands;
using Harmonic.Networking.Rtmp.Serialization;

namespace RtmpProtocol.Messages.Commands
{
    internal class FCUnpublishCommandMessage : CommandMessage
    {
        [OptionalArgument]
        public string PublishingName { get; set; }

        public FCUnpublishCommandMessage(AmfEncodingVersion encoding) : base("FCUnpublish", encoding)
        {
        }

        public override string ToString() => $"FCUnpublish({PublishingName})";
    }
}
