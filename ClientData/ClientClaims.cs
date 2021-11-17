using Serilog;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Streamster.ClientData
{
    public class ClientClaims
    {
        public int MaxBitrate { get; }

        public int MaxChannels { get; }

        public bool IsDebug { get; }

        public int Transcoders { get; }

        public ClientClaimTranscoderLimit TranscoderInputLimit { get; set; }

        public ClientClaimTranscoderLimit TranscoderOutputLimit { get; set; }

        public string AppUpdatePath { get; set; }

        public bool HasVpn { get; set; }

        public ClientClaims(IEnumerable<Claim> claims)
        {
            MaxBitrate = GetIntClaim(claims, ClientConstants.MaxBitrateClaim, 4000);
            MaxChannels = GetIntClaim(claims, ClientConstants.MaxChannelsClaim, 2);
            Transcoders = GetIntClaim(claims, ClientConstants.TranscodersClaim, 0, true);

            TranscoderInputLimit = GetTranscoderLimit(claims, ClientConstants.TranscodersInputLimitClaim);
            TranscoderOutputLimit = GetTranscoderLimit(claims, ClientConstants.TranscodersOutputLimitClaim);


            IsDebug = claims.Any(s => s.Type == ClientConstants.DebugClaim);
            AppUpdatePath = claims.FirstOrDefault(s => s.Type == ClientConstants.AppUpdatePathClaim)?.Value;

            var found = claims.FirstOrDefault(s => s.Type == ClientConstants.VpnClaim);
            if (found != null && int.TryParse(found.Value, out var vpnVersion))
            {
                if (vpnVersion == ClientConstants.VpnVersion)
                    HasVpn = true;
                else
                    Log.Warning($"VpnVersion {vpnVersion}!={ClientConstants.VpnVersion} mismatch");
            }
        }

        private ClientClaimTranscoderLimit GetTranscoderLimit(IEnumerable<Claim> claims, string name)
        {
            var found = claims.FirstOrDefault(s => s.Type == name);
            if (found != null && found.Value != null)
            {
                var parts = found.Value.Split('x');

                if (parts.Length >= 2 && int.TryParse(parts[0], out var height) && int.TryParse(parts[1], out var fps))
                {
                    return new ClientClaimTranscoderLimit { Fps = fps, Height = height };
                }
            }
            return new ClientClaimTranscoderLimit();
        }

        private int GetIntClaim(IEnumerable<Claim> claims, string name, int def, bool optional = false)
        {
            var found = claims.FirstOrDefault(s => s.Type == name);
            if (found == null)
            {
                if (!optional)
                    Log.Error($"Unable to find '{name}' claim");
            }
            else if (!int.TryParse(found.Value, out var res))
                Log.Error($"Unable to parse '{found.Value}' for '{name}' claim");
            else
                return res;

            return def;
        }
    }

    public class ClientClaimTranscoderLimit
    {
        public int Fps { get; set; } = 30;

        public int Height { get; set; } = 1080;
    }
}
