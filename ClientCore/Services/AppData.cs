using System.Linq;

namespace Streamster.ClientCore.Services
{
    public class AppData
    {
        public string Marker { get; set; }

        public string ClientTitle { get; set; }

        public string ClientIcon { get; set; }

        public string Background { get; set; }

        public string Logo { get; internal set; }

        public string Description { get; set; }

        public string WebSiteName { get; set; }

        public string WebSiteUrl { get; set; }

        public string MyAccountUrl { get; set; }

        public string KnowledgeBaseUrl { get; set; }

        public string RegisterUrl { get; set; }

        public string TermsOfServiceUrl { get; set; }

        public string PricingUrl { get; set; }

        public string DownloadAppUrl { get; set; }
    }

    public class AppDataFactory
    {
        private static AppData[] _brands =
        {
            new AppData
            {
                Marker = null,
                ClientTitle = "Streamster",
                ClientIcon = "not used",
                Background = "../../Assets/Background.png",
                Logo = "../../Assets/Logo.png",
                Description = "FREE LIVE BROADCASTING SOFTWARE WITH A CLOUD-BASED MULTISTREAMING FEATURE",
                WebSiteName = "Streamster.io website",
                WebSiteUrl = "https://streamster.io",
                MyAccountUrl = "https://account.streamster.io",
                KnowledgeBaseUrl = "https://help.streamster.io/support/home",
                RegisterUrl = "https://account.streamster.io/register",
                TermsOfServiceUrl = "https://streamster.io/terms-conditions",
                PricingUrl = "https://account.streamster.io/user/tariff",
                DownloadAppUrl = "https://streamster.io",
            },
        };


        public static AppData Create()
        {
            return _brands.FirstOrDefault();
        }
    }
}
