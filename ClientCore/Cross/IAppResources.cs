
using Streamster.ClientData.Model;

namespace Streamster.ClientCore.Cross
{
    public interface IAppResources
    {
        AppData AppData { get; }

        bool TargetFilter(ITarget target);
    }

    public class AppData
    {
        public string UserNamePrefix { get; set; }

        public string ClientTitle { get; set; }

        public string Background { get; set; }

        public string BackgroundBye { get; set; }

        public string Logo { get; set; }

        public string Logo1 { get; set; }

        public string Logo2 { get; set; }

        public string Logo3 { get; set; }

        public string Logo4 { get; set; }

        public bool SimpleLogo { get; set; }

        public string Description { get; set; }

        public string WebSiteName { get; set; }

        public string WebSiteUrl { get; set; }

        public string MyAccountUrl { get; set; }

        public string KnowledgeBaseUrl { get; set; }

        public string RegisterUrl { get; set; }

        public string TermsOfServiceUrl { get; set; }

        public string PricingUrl { get; set; }

        public string DownloadAppUrl { get; set; }

        public string TargetHintTemplate { get; set; }

        public string Domain { get; set; }

        public string DataFolder { get; set; }

        public bool HideTargetFilter { get; set; }
    }
}
