using Autofac;
using Streamster.ClientApp.Win.Services;
using Streamster.ClientCore;
using Streamster.ClientCore.Cross;

namespace Streamster.ClientApp.Win
{
    class DependencyInjection : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Add<IAppEnvironment, WinEnvironment>();
            builder.Add<ILocalVideoSourceManager, LocalVideoSourceManager>();
            builder.Add<ILocalAudioSourceManager, LocalAudioSourceManager>();
            builder.Add<IWindowStateManager, WindowStateManager>();
            builder.Add<IUpdateManager, UpdateManager>();
            builder.Add<ICpuService, CpuService>();
            builder.Add<IVpnService, VpnService>();
            builder.Add<IImageHelper, ImageHelper>();
        }
    }
}
