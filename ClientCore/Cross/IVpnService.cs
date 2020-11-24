using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Cross
{
    public interface IVpnService
    {
        Task ConnectAsync(VpnData vpnData, Action<VpnRuntimeState> onStateChanged);

        Task DisconnectAsync();
    }


    public class VpnException : Exception
    {
        public VpnException(string msg) : base(msg)
        {
        }
    }

    public class VpnRuntimeState
    {
        public bool Connected { get; set; }

        public int SentKbs { get; set; }

        public int ReceivedKbs { get; set; }

        public string ErrorMessage { get; set; }

        public string ServerIpAddress { get; set; }
    }
}
