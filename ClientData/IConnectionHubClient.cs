using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Streamster.ClientData
{
    public interface IConnectionHubClient
    {
        Task JsonPatch(ProtocolJsonPatchPayload payload);

        Task ReceiveChatMessages(ReceiveChatMessagesData data);
    }

    public class ProtocolJsonPatchPayload
    {
        public string Changes { get; set; }

        public bool Reset { get; set; }
    }

    public class ReceiveChatMessagesData
    {
        public ReceiveChatMessagesPerTarget[] Targets { get; set; }
    }

    public class ReceiveChatMessagesPerTarget
    {
        public string TargetId { get; set; }

        public ChatMessageToReceive[] Messages { get; set; }

        public ChatMessageAuthor[] Authors { get; set; }
    }

    public class ChatMessageToReceive
    {
        public string Msg { get; set; }

        public DateTime Time { get; set; }

        public string AuthorId { get; set; }
    }

    public class ChatMessageAuthor
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public bool Self { get; set; }
    }
}
