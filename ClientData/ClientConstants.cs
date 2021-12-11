using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientData
{
    public class ClientConstants
    {
        public static int AuthorizationServerPort = 6000;
        public static int LoadBalancerServerPort = 6001;
        public static string LoadBalancerFilesFolder = "/Files";
        public static string LoadBalancerFiles_Versions = "versions";
        public static string LoadBalancerFiles_Targets = "targets";

#if DEBUG
        public static string[] LoadBalancerServers = new[] { "localhost" };
        //public static string[] LoadBalancerServers = new[] { "fi3.streamster.io" };
#else
        //public static string[] LoadBalancerServers = new[] { "fi1.streamster.io", "de2.streamster.io", "mo1.streamster.io" };
        public static string[] LoadBalancerServers = new[] { "fi3.streamster.io" };
#endif

        public static string AnonymousGrandType = "ano";
        public static string WinClientId = "win";
        public static string UwpClientId = "uwp";
        public static string ConnectionServerApi = "csrv";


        public static string DeviceIdClaim = "iid";
        public static string MaxBitrateClaim = "mbr";
        public static string MaxChannelsClaim = "mch";
        public static string VersionClaim = "ver";
        public static string AppUpdatePathClaim = "aup";
        public static string DebugClaim = "deb";
        public static string VpnClaim = "vpn";
        public static string DomainClaim = "dmn";
        public static string TranscodersClaim = "tra";
        public static string TranscodersInputLimitClaim = "tri";
        public static string TranscodersOutputLimitClaim = "tro";

        public static int VpnVersion = 1;
    }
}
