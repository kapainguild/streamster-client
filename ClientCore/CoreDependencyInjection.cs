using Autofac;
using DeltaModel;
using Streamster.ClientCore.Logging;
using Streamster.ClientCore.Models;
using Streamster.ClientCore.Models.Chats;
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
                .Add<ResourceService>()
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
                .Add<MainIndicatorsModel>()
                .Add<LocalSourcesModel>()
                .Add<ScreenRendererModel>()
                .Add<MainAboutModel>()
                .Add<MainVpnModel>()
                .Add<TransientMessageModel>()
                .Add<SceneEditingModel>()
                .Add<AudioModel>()
                .Add<StreamSettingsModel>()
                .Add<TranscodingModel>()
                .Add<PlatformsModel>()
                .Add<ExternalEncoderModel>()
                .Add<ExternalPreviewModel>()
                .Add<StreamingSourcesModel>();
        }
    }
}
