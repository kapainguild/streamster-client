using Streamster.ClientCore.Cross;
using Streamster.ClientData.Model;
using System.IO;
using System.Reflection;

namespace Streamster.ClientApp.Win
{
    class WinResources : IAppResources
    {
        static byte[] _canvasBackground = ReadResource("Canvas.png");

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
            WebSiteName = "Streamster.io home page",
            WebSiteUrl = "https://streamster.io",
            MyAccountUrl = "https://app.streamster.io/login",
            KnowledgeBaseUrl = "https://docs.streamster.io",
            RegisterUrl = "https://app.streamster.io/register",
            TermsOfServiceUrl = "https://streamster.io/terms-conditions",
            CreateTicketUrl = "https://help.streamster.io/support/tickets/new",
            PricingUrl = "https://app.streamster.io/tariff",
            PricingUrlForNotRegistered = "https://app.streamster.io/register",
            DownloadAppUrl = "https://streamster.io",
            TargetHintTemplate = "https://streamster.io/manuals/{0}",
            Domain = null,
            CanvasBackground = _canvasBackground
        };

        public static byte[] ReadResource(string name)
        {
            var assembly = Assembly.GetEntryAssembly();
            using (Stream stream = assembly.GetManifestResourceStream($"Streamster.ClientApp.Win.Assets.{name}"))
            {
                var buf = new byte[stream.Length];
                stream.Read(buf);
                return buf;
            }
        }

        public bool TargetFilter(ITarget target) => true;
    }
}
