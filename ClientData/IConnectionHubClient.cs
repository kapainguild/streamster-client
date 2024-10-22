using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Streamster.ClientData
{
    public interface IConnectionHubClient
    {
        Task JsonPatch(ProtocolJsonPatchPayload payload);

        Task ReceiveChatMessages(ReceiveChatMessagesData data, CancellationToken cancellationToken);
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

        public int Tip { get; set; }

        public int Type { get; set; } //ChatMessageToReceiveType

        public bool Private { get; set; }

        public string ToAuthorId { get; set; } // for private messages from model to user

        public bool Internal { get; set; } // internal messages from streamster to show user something, it should not possible to send private messages to them
    }

    public enum ChatMessageToReceiveType
    {
        Message = 0,
        Tip = 1,
    }

    public class ChatMessageAuthor
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public bool Self { get; set; }
    }
}
