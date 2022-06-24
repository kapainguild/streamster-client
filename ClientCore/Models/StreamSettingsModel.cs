using Streamster.ClientCore.Services;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class StreamSettingsModel
    {
        private ConnectionService _connectionService;
        private readonly RootModel _rootModel;
        private bool _promoIsShown;

        private readonly TransientMessageModel _transientMessage;
        private readonly StreamingSourcesModel _streamingSourcesModel;
        private int _vpnMessage = -1;

        private LayoutType _lastLayout = LayoutType.Standart;

        public Resolution[] Resolutions { get; } = new Resolution[]
        {
            new Resolution(3840, 2160),
            new Resolution(2560, 1440),
            new Resolution(1920, 1080),
            new Resolution(1280, 720),
            new Resolution(960, 720),
            new Resolution(960, 540),
            new Resolution(640, 360),
        };

        public int[] FpsList { get; } = new[] { 60, 50, 30, 25, 20, 15, 10 };

        public int MinBitrate { get; } = 800;

        public int MaxBitrate { get; set; }

        public Property<int> ActualBitrate { get; } = new Property<int>();

        public Property<bool> IsChangeBitrateEnabled { get; } = new Property<bool>(true);

        public Property<IndicatorState> ActualBitrateState { get; } = new Property<IndicatorState>();

        public Action<object> SelectResolution { get; }

        public Action<object> SelectFps { get; }

        public Property<string> Promo { get; } = new Property<string>();

        public Property<string> PromoUrl { get; } = new Property<string>();

        public Property<bool> ChangeStreamParamsDisabled { get; } = new Property<bool>();

        public CoreData CoreData { get; }

        public MainVpnModel Vpn { get; }

        public Property<LayoutType> SelectedLayout { get; } = new Property<LayoutType>(LayoutType.Standart);

        private Dictionary<string, BitrateInfo> _bitratesByDevices = new Dictionary<string, BitrateInfo>();

        public List<SettingsSelectorData<LayoutType>> LayoutTypes { get; } = new List<SettingsSelectorData<LayoutType>>
        {
            new SettingsSelectorData<LayoutType> { Value = LayoutType.NoScreen, DisplayName = "No preview" },
            new SettingsSelectorData<LayoutType> { Value = LayoutType.Standart, DisplayName = "Standard" },
            new SettingsSelectorData<LayoutType> { Value = LayoutType.ScreenAndIndicators, DisplayName = "Preview & states" },
            new SettingsSelectorData<LayoutType> { Value = LayoutType.ScreenOnly, DisplayName = "Preview only" },
        };

        public Action<object> SelectLayout { get; }

        public Action ShowPreview { get; }

        public StreamSettingsModel(CoreData coreData,
            ConnectionService connectionService,
            RootModel rootModel,
            TransientMessageModel transientMessage,
            MainVpnModel vpn,
            StreamingSourcesModel streamingSourcesModel)
        {
            CoreData = coreData;
            _connectionService = connectionService;
            _rootModel = rootModel;
            _transientMessage = transientMessage;
            Vpn = vpn;
            _streamingSourcesModel = streamingSourcesModel;
            SelectResolution = o => CoreData.Settings.Resolution = (Resolution)o;
            SelectLayout = o =>
            {
                _lastLayout = SelectedLayout.Value;
                SelectedLayout.Value = ((SettingsSelectorData<LayoutType>)o).Value;
            };
            SelectFps = o => CoreData.Settings.Fps = (int)o;
            ShowPreview = () => SelectedLayout.Value = _lastLayout;
        }

        internal void SetActualBitrate(int ave, IndicatorState state, DeviceIndicatorsModel localDevice)
        {
            _bitratesByDevices[localDevice.DeviceId] = new BitrateInfo { Bitrate = ave, IndicatorState = state, Updated = DateTime.UtcNow };

            RefreshBitrate();
        }

        private void RefreshBitrate()
        {
            if (_streamingSourcesModel.TryGetCurrentScene(out var scene) && 
                _bitratesByDevices.TryGetValue(scene.Owner, out var info) &&
                DateTime.UtcNow - info.Updated < TimeSpan.FromSeconds(3))
            {
                var bitrate = info.Bitrate;
                if (bitrate < MinBitrate + 30)
                    bitrate = MinBitrate + 30;

                ActualBitrate.Value = bitrate;
                ActualBitrateState.Value = info.IndicatorState;

                bool externalEncoder = scene.Owner == ClientConstants.ExternalClientId;
                IsChangeBitrateEnabled.Value = !externalEncoder;
            }
            else
            {
                ActualBitrate.Value = MinBitrate;
                ActualBitrateState.Value = IndicatorState.Disabled;
                IsChangeBitrateEnabled.Value = true;
            }
        }

        internal void Start()
        {
            MaxBitrate = _connectionService.Claims.MaxBitrate;

            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.Bitrate, (i, c, p) => RefreshPromo());
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.IsRecordingRequested, (i, c, p) => RefreshControls());
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.StreamingToCloudStarted, (i, c, p) => RefreshControls());

            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.SelectedScene, (i, c, p) => RefreshControls());
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.NoStreamWithoutVpn, (i, c, p) => RefreshControls());
            CoreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.VpnState, (i, c, p) => RefreshControls());

            RefreshPromo();
            RefreshControls();
        }

        private void RefreshControls()
        {
            ChangeStreamParamsDisabled.Value = CoreData.Settings.IsRecordingRequested || CoreData.Settings.StreamingToCloudStarted;

            RefreshBitrate();

            if (CoreData.Settings.NoStreamWithoutVpn)
            {
                if (_streamingSourcesModel.IsMySceneSelected())
                {
                    if (CoreData.ThisDevice.VpnState == VpnState.Idle)
                        _vpnMessage = _transientMessage.Show("VPN is OFF. Streaming is not possible.", TransientMessageType.Error, false);
                    else
                        _vpnMessage = _transientMessage.Show("VPN is ON but not yet connected. Streaming not possible.", TransientMessageType.Error, false);
                }
            }
            if (_vpnMessage != -1)
                _transientMessage.Clear(_vpnMessage);
        }

        private void RefreshPromo()
        {
            if (!_promoIsShown && CoreData.Settings.Bitrate == MaxBitrate && MaxBitrate < 16000)
            {
                _promoIsShown = true;
                Promo.Value = MaxBitrate == 4000 ? "Get more bitrate after registration" : "Upgrade you plan to get more bitrate";
                PromoUrl.Value = MaxBitrate == 4000 ? _rootModel.AppData.RegisterUrl : _rootModel.AppData.PricingUrl;
                TaskHelper.RunUnawaited(async () =>
                {
                    await Task.Delay(8000);
                    Promo.Value = null;
                }, "Show promo");
            }
        }
    }

    class BitrateInfo
    {
        public DateTime Updated { get; set; }

        public int Bitrate { get; set; }

        public IndicatorState IndicatorState { get; set; }
    }
}
