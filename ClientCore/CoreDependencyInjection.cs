using Autofac;
using Clutch.DeltaModel;
using Streamster.ClientCore.Logging;
using Streamster.ClientCore.Models;
using Streamster.ClientCore.Services;

namespace Streamster.ClientCore
{
    public class CoreDependencyInjection : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder
                .Add<LogService>()
                .Add<LocalSettingsService>()
                .Add<ConnectionService>()
                .Add<NotificationService>()
                .Add<HubConnectionService>()
                .Add<StaticFilesCacheService>()
                .Add<IDeltaServiceProvider, DeltaServiceProvider>()
                .Add<CoreData>()
                .Add<IdService>()
                .Add<StateLoggerService>();

            builder
                .Add<RootModel>()
                .Add<LoginModel>()
                .Add<UpdateModel>()
                .Add<MainModel>()
                .Add<ByeByeModel>()
                .Add<MainTargetsModel>()
                .Add<MainSettingsModel>()
                .Add<MainStreamerModel>()
                .Add<MainFiltersModel>()
                .Add<MainIndicatorsModel>()
                .Add<MainSourcesModel>()
                .Add<ScreenRendererModel>()
                .Add<MainAboutModel>();
        }
    }
}
