using Clutch.DeltaModel;
using DeltaModel;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Models;
using Streamster.ClientCore.Services;
using Streamster.ClientData.Model;
using System;
using System.ComponentModel;
using System.Threading;

namespace Streamster.ClientCore
{
    public class CoreData : INotifyPropertyChanged
    {
        private readonly IdService _idService;
        private readonly HubConnectionService _hubConnectionService; // required for correct disposal sequence
        private readonly DeltaModelManager<IRoot> _manager;

        private readonly SynchronizationContext _syncContext;


        public event PropertyChangedEventHandler PropertyChanged;


        public IRoot Root { get; private set; }

        public IDevice ThisDevice { get; private set; }

        public ISettings Settings { get; private set; }

        public string ThisDeviceId => _idService.GetDeviceId();

        public SubscriptionManager Subscriptions => _manager.Subscriptions;

        public DeltaModelManager<IRoot> GetManager() => _manager;


        public CoreData(IdService environment, IDeltaServiceProvider deltaServiceProvider, HubConnectionService hubConnectionService)
        {
            _idService = environment;
            _hubConnectionService = hubConnectionService;
            _manager = Build(deltaServiceProvider);
            _manager.RootChanged += OnRootChanged;

            _syncContext = SynchronizationContext.Current;
        }

        public T Create<T>()
        {
            return _manager.Create<T>();
        }

        public T Create<T>(Action<T> init)
        {
            var result = _manager.Create<T>();
            init(result);
            return result;
        }

        public void RunOnMainThread(Action action)
        {
            if (SynchronizationContext.Current == null)
                _syncContext.Post(s => action(), null);
            else
                action();
        }


        //TODO: bad pattern as is: introduces a lot of patches
        public T GetOrCreate<T>(Func<T> getter, Action<T> init) => _manager.GetOrCreate(getter, init);

        private void OnRootChanged(object sender, EventArgs e)
        {
            Root = _manager.Root;
            ThisDevice = Root.Devices[ThisDeviceId];
            Settings = Root.Settings;

            // precreated data
            _manager.GetOrCreate(() => ThisDevice.DeviceSettings, v => ThisDevice.DeviceSettings = v);
            _manager.GetOrCreate(() => ThisDevice.KPIs, v => ThisDevice.KPIs = v);
            _manager.GetOrCreate(() => ThisDevice.KPIs.Cpu, v => ThisDevice.KPIs.Cpu = v);
            _manager.GetOrCreate(() => ThisDevice.KPIs.CloudIn, v => ThisDevice.KPIs.CloudIn = v);
            _manager.GetOrCreate(() => ThisDevice.KPIs.CloudOut, v => ThisDevice.KPIs.CloudOut = v);
            _manager.GetOrCreate(() => ThisDevice.KPIs.Encoder, v => ThisDevice.KPIs.Encoder = v);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Root)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ThisDevice)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Settings)));
        }


        private DeltaModelManager<IRoot> Build(IDeltaServiceProvider deltaServiceProvider)
        {
            var builder = new DeltaModelBuilder();
            builder.Config<IChannel>(c =>
            {
                c.HasLocal((s, m, p) => new ChannelModel(s, p.Get<IAppEnvironment>(), p.Get<CoreData>(), p.Get<MainTargetsModel>()));
            });

            builder.Config<IVideoInput>(c =>
            {
                c.HasLocal((s, m, p) => new LocalVideoInputModel(s, this, p.Get<MainSourcesModel>(), p.Get<LocalSettingsService>()));
            });

            builder.Config<IAudioInput>(c =>
            {
                c.HasLocal((s, m, p) => new LocalAudioInputModel(s, this, p.Get<MainSourcesModel>()));
            });

            builder.Config<ISettings>(c =>
            {
                c.Property(s => s.SelectedVideo).DontCompareBeforeSet();
                c.Property(s => s.SelectedAudio).DontCompareBeforeSet();

                c.Property(s => s.StreamingToCloud).HasDefault(StreamingToCloudBehavior.FirstChannel);
            });

            builder.Config<IIndicatorCpu>(c => { c.Property(s => s.Load).DontCompareBeforeSet(); });

            builder.Config<IIndicatorCloudIn>(c => { c.Property(s => s.Bitrate).DontCompareBeforeSet(); });
            builder.Config<IIndicatorCloudOut>(c => { c.Property(s => s.Bitrate).DontCompareBeforeSet(); });
            builder.Config<IIndicatorEncoder>(c => 
            { 
                c.Property(s => s.InputFps).DontCompareBeforeSet();
                c.Property(s => s.QueueSize).DontCompareBeforeSet();
            });

            return builder.Build<IRoot>(deltaServiceProvider, new SingleThreadLockerProvider());
        }

        public T GetLocal<T>(object model) => _manager.GetLocal<T>(model);

        public T GetParent<T>(object model) => _manager.GetParent<T>(model);

        internal string GetId(object obj) => _manager.GetId(obj);
    }
}
