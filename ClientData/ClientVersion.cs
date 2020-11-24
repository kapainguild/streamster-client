using System;
using System.Linq;
using System.Reflection;

namespace Streamster.ClientData
{
    public class ClientVersion
    {
        public string Version { get; set; }

        public bool BreakingChange { get; set; }

        public string WhatsNew { get; set; }

        public ClientVersion Clone()
        {
            return new ClientVersion
            {
                Version = Version,
                BreakingChange = BreakingChange,
                WhatsNew = WhatsNew,
            };
        }
    }

    public static class ClientVersionHelper
    {
        public static bool IsBreakingChange(ClientVersion[] versions)
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            return versions?.Any(s => s.BreakingChange && new Version(s.Version) > ver) ?? false;
        }

        public static string GetVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }

        public static (ClientVersion, string) GetCurrent(ClientVersion[] versions)
        {
            var ver = GetVersion();
            return (versions?.FirstOrDefault(s => s.Version == ver), ver);
        }
    }
}
