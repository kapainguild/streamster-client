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

        public string AppUpdatePath { get; set; }

        public ClientClaims(IEnumerable<Claim> claims)
        {
            MaxBitrate = GetIntClaim(claims, ClientConstants.MaxBitrateClaim, 4000);
            MaxChannels = GetIntClaim(claims, ClientConstants.MaxChannelsClaim, 2);
            IsDebug = claims.Any(s => s.Type == ClientConstants.DebugClaim);
            AppUpdatePath = claims.FirstOrDefault(s => s.Type == ClientConstants.AppUpdatePathClaim)?.Value;
        }

        private int GetIntClaim(IEnumerable<Claim> claims, string name, int def)
        {
            var found = claims.FirstOrDefault(s => s.Type == name);
            if (found == null)
                Log.Error($"Unable to find '{name}' claim");
            else if (!int.TryParse(found.Value, out var res))
                Log.Error($"Unable to parse '{found.Value}' for '{name}' claim");
            else
                return res;

            return def;
        }
    }
}
