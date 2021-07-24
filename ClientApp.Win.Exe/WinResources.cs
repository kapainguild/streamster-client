using Streamster.ClientCore.Cross;
using Streamster.ClientData.Model;

namespace Streamster.ClientApp.Win
{
    class WinResources : IAppResources
    {
        public AppData AppData => new AppData
        {
            UserNamePrefix = "",
            ClientTitle = "Streamster",
            DataFolder = "Streamster.Data",
            Background = "pack://application:,,,/Streamster.ClientApp.Win;component/Assets/Background.png",
            BackgroundBye = "pack://application:,,,/Streamster.ClientApp.Win;component/Assets/Background.png",
            Logo = "pack://application:,,,/Streamster.ClientApp.Win;component/Assets/Logo.png",
            Logo1 = "pack://application:,,,/Streamster.ClientApp.Win;component/Assets/Logo1.png",
            Logo2 = "pack://application:,,,/Streamster.ClientApp.Win;component/Assets/Logo2.png",
            Logo3 = "pack://application:,,,/Streamster.ClientApp.Win;component/Assets/Logo3.png",
            Logo4 = "pack://application:,,,/Streamster.ClientApp.Win;component/Assets/Logo4.png",
            SimpleLogo = false,

            Description = "FREE LIVE BROADCASTING SOFTWARE WITH A CLOUD-BASED MULTISTREAMING FEATURE",
            WebSiteName = "streamster.io website",
            WebSiteUrl = "https://streamster.io",
            MyAccountUrl = "https://account.streamster.io",
            KnowledgeBaseUrl = "https://help.streamster.io/support/home",
            RegisterUrl = "https://account.streamster.io/register",
            TermsOfServiceUrl = "https://streamster.io/terms-conditions",
            PricingUrl = "https://account.streamster.io/user/tariff",
            DownloadAppUrl = "https://streamster.io",
            TargetHintTemplate = "https://streamster.io/{0}",
            Domain = null,
        };

        public bool TargetFilter(ITarget target) => true;
    }
}
