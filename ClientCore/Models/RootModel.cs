using Autofac;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using System;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class RootModel
    {
        private readonly ILifetimeScope _container;
        private readonly HubConnectionService _hubConnectionService;
        private readonly MainVpnModel _mainVpnModel;

        public RootModel(ILifetimeScope container, IWindowStateManager windowStateManager, HubConnectionService hubConnectionService, MainVpnModel mainVpnModel, IAppResources appResources)
        {
            AppData = appResources.AppData;
            _container = container;
            WindowStateManager = windowStateManager;
            _hubConnectionService = hubConnectionService;
            _mainVpnModel = mainVpnModel;
        }

        public ByeByeModel ByeByeModel { get; set; }

        public AppData AppData { get; }

        public Property<object> CurrentPage { get; } = new Property<object>();

        public Property<bool> Resizable { get; } = new Property<bool>();

        public IWindowStateManager WindowStateManager { get; }

        public void NavigateTo(object model)
        {
            CurrentPage.Value = model;
        }

        public async Task Set(object model)
        {
            await Task.Yield();
            CurrentPage.Value = model;
        }

        public void Exit()
        {
            if (CurrentPage.Value != ByeByeModel)
            {
                CurrentPage.Value = ByeByeModel;
                _ = ExitAsync();
            }
        }

        private async Task ExitAsync()
        {
            try
            {
                Log.Information("Exit app requested");
                var maxDelay = Task.Delay(4000);
                if (await Task.WhenAny(maxDelay,
                                 Task.WhenAll(new Task[] { Task.Delay(2000), 
                                                            StopAll() })) == maxDelay)
                {
                    Log.Warning("No exit withing 4 seconds");
                }
            }
            catch(Exception e)
            {
                Log.Error(e, "Failure during exit");
            }
            Environment.Exit(0);
        }

        private async Task StopAll()
        {
            Log.Information("Disposing all");
            await _container.DisposeAsync().AsTask();
            Log.Information("Disposed all");

            await Task.Delay(400); // wait for everything is done.

            Log.Information("Closing connection");
            await Task.Run(() => Log.CloseAndFlush());
            // no loging after this point
            
            if (_hubConnectionService.Connection != null)
                await _hubConnectionService.Connection.DisposeAsync();

            await _mainVpnModel.StopAsync();
        }
    }
}
