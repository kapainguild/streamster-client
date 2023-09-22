using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Linq;
using System.Web;

namespace Streamster.ClientCore.Models
{
    public class ExternalPreviewModel
    {
        private CoreData _coreData;
        private ConnectionService _connectionService;

        public AppData ApplicationData { get; }

        public Property<string> Url { get; } = new Property<string>();

        public Action CopyUrl { get; }

        public Property<bool> IsRegistered { get; } = new Property<bool>();

        public ExternalPreviewModel(CoreData coreData, ConnectionService connectionService, IAppEnvironment appEnvironment, IAppResources appResources)
        {
            _coreData = coreData;
            _connectionService = connectionService;

            ApplicationData = appResources.AppData;

            CopyUrl = () =>
            {
                Log.Information("External preview Url copied");
                appEnvironment.CopyToClipboard(Url.Value);
            };
        }

        public void Start()
        {
            RefreshValue();

            IsRegistered.Value = !String.IsNullOrEmpty(_connectionService.UserName);
            _coreData.Subscriptions.SubscribeForProperties<IOutgest>(s => s.Data, (a, b, c) => RefreshValue());
        }

        private void RefreshValue()
        {
            var id = _coreData.ThisDeviceId;
            var myOutgets = _coreData.Root.Outgests.Values.
                FirstOrDefault(s => s.Data != null && s.Data.RequireType == RequireOutgestType.WebRtc && s.Data.DeviceId == id);
            if (myOutgets != null)
                Url.Value = ClientConstants.GetWebRtcWebPage(_connectionService.ConnectionServer, myOutgets.Data.Output);
            else
                Url.Value = null;
        }
    }
}
