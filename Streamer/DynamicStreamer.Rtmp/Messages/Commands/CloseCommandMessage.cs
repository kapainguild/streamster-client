using Harmonic.Networking.Rtmp.Messages;
using Harmonic.Networking.Rtmp.Messages.Commands;

namespace Harmonic.Networking.Rtmp.Messages.Commands
{
    internal class CloseCommandMessage : CommandMessage
    {
        public CloseCommandMessage(AmfEncodingVersion encoding) : base("close", encoding)
        {
        }
    }
}
