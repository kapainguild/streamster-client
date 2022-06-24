

using Harmonic.Networking.Rtmp.Messages.Commands;
using Harmonic.Networking.Rtmp.Messages;
using Harmonic.Networking.Amf.Common;

namespace Harmonic.Networking
{
    public class ConnectionInformation
    {
        public ConnectionInformation(AmfObject info)
        {
            App = GetVal(info, "app");
            SwfUrl = GetVal(info, "swfUrl");
            TcUrl = GetVal(info, "tcUrl");
        }

        public string App { get; set; }
        public string Flashver { get; set; }
        public string SwfUrl { get; set; }
        public string TcUrl { get; set; }
        public bool Fpad { get; set; }
        public int AudioCodecs { get; set; }
        public int VideoCodecs { get; set; }
        int VideoFunction { get; set; }
        public string PageUrl { get; set; }
        public AmfEncodingVersion AmfEncodingVersion { get; set; }

        private string GetVal(AmfObject info, string name)
        {
            if (info.Fields.TryGetValue(name, out var val) && val is string s)
                return s;
            return null;
        }
    }
}