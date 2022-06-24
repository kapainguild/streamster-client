using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData.Model;
using System;
using System.Linq;

namespace Streamster.ClientCore.Models
{
    public class ExternalEncoderModel
    {
        private readonly CoreData _coreData;
        private readonly ConnectionService _connectionService;

        public Property<string> Url { get; } = new Property<string>();

        public Property<string> Key { get; } = new Property<string>();

        public Action Reset { get; }

        public AppData ApplicationData { get; }

        public Action CopyUrl { get; }

        public Action CopyKey { get; }

        public RootModel Root { get; }

        public Property<bool> IsRegistered { get; } = new Property<bool>();

        public ExternalEncoderModel(CoreData coreData, IAppEnvironment appEnvironment, IAppResources appResources, ConnectionService connectionService)
        {
            _coreData = coreData;
            _connectionService = connectionService;
            CopyKey = () => appEnvironment.CopyToClipboard(Key.Value);
            CopyUrl = () => appEnvironment.CopyToClipboard(Url.Value);
            Reset = () => DoReset();

            ApplicationData = appResources.AppData;
        }

        public void Start()
        {
            RefreshValue();

            IsRegistered.Value = !String.IsNullOrEmpty(_connectionService.UserName);
            _coreData.Subscriptions.SubscribeForProperties<IIngest>(s => s.Data, (a, b, c) => RefreshValue());
        }

        private void DoReset()
        {
            var item = _coreData.Root.Ingests.Values.FirstOrDefault(s => s.Type == IngestType.External);
            if (item != null)
                item.ResetCounter++;
        }

        public void RefreshValue()
        {
            var item = _coreData.Root.Ingests.Values.FirstOrDefault(s => s.Type == IngestType.External);
            Url.SilentValue = item?.Data?.Output ?? "Failed";
            Key.SilentValue = item?.Data?.Options ?? "Failed";
        }
    }
}
