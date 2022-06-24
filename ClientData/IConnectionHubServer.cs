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
    }

}
