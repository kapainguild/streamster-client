//#define STAGING

using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientData
{

    public class ClientConstants
    {
        public static int AuthorizationServerPort = 6000;
        public static int AuthorizationServerPortMobile = 6002;
        public static int LoadBalancerServerPort = 6001;
        public static string LoadBalancerFilesFolder = "/Files";
        public static string LoadBalancerFiles_Versions = "versions";
        public static string LoadBalancerFiles_Targets = "targets";

        public const bool ChatsEnabled = false;

#if STAGING
        public static string[] LoadBalancerServers = new[] { "fi3.streamster.io" };

        public static string IngestSuffix = ".staging-in.streamster.io";

        public static string GetRtmpUrl(string urlId, string localServer) => "rtmp://" + urlId + IngestSuffix + "/in";
        
#elif DEBUG
        public static string[] LoadBalancerServers = new[] { "localhost" };

        public static string IngestSuffix = "???";

        public static string GetRtmpUrl(string urlId, string localServer) => "rtmp://" + localServer + "/in";

#else
        public static string[] LoadBalancerServers = new[] { "fi1.streamster.io", "de2.streamster.io", "mo1.streamster.io" };

        public static string IngestSuffix = ".in.streamster.io";

        public static string GetRtmpUrl(string urlId, string localServer) => "rtmp://" + urlId + IngestSuffix + "/in";

#endif


        public static string AnonymousGrandType = "ano";

        public static string WinClientId = "win";
        public static string UwpClientId = "uwp";
        public static string WebClientId = "web";
        public static string IosClientId = "ios";
        public static string AndroidClientId = "and";
        public static string ExternalClientId = "ext"; // obs

        public static string ConnectionServerApi = "csrv";


        public static string ClientIdClaim = "client_id";
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


        public static bool SupportsStreaming(string type) => type == ClientConstants.WinClientId || type == ClientConstants.AndroidClientId ||
                                                       type == ClientConstants.IosClientId || type == ClientConstants.ExternalClientId;

        public static bool SupportsSceneEditing(string type) => type == ClientConstants.WinClientId;

    }
}
