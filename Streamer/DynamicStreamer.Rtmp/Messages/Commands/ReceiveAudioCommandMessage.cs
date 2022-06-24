using Harmonic.Networking.Rtmp.Serialization;

namespace Harmonic.Networking.Rtmp.Messages.Commands
{
    public class ReceiveAudioCommandMessage : CommandMessage
    {
        [OptionalArgument]
        public bool IsReceive { get; set; }

        public ReceiveAudioCommandMessage(AmfEncodingVersion encoding) : base("receiveAudio", encoding)
        {
        }
    }
}
