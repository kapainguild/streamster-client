using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Streamster.ClientData
{
    public interface IConnectionHubClient
    {
        Task JsonPatch(ProtocolJsonPatchPayload payload);
    }

    public class ProtocolJsonPatchPayload
    {
        public string Changes { get; set; }

        public bool Reset { get; set; }
    }
}
