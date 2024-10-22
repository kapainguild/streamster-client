using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Streamster.ClientData
{
    public interface IConnectionHubServer
    {
        Task JsonPatch(ProtocolJsonPatchPayload payload);

        Task Logs(ProtocolLogPayload payload);

        Task SendChatMessage(ChatMessageToSend msg);
    }

    public class ProtocolLogPayload
    {
        public string[] Logs { get; set; }
    }


    public class ChatMessageToSend
    {
        public string ChatId { get; set; }

        public string Msg { get; set; }

        public string AuthorId { get; set; } // if author id is specified it is private message
    }


    public class ProtocolAuthenticateData
    {
        public string User { get; set; }

        public string Password { get; set; }
    }

}
