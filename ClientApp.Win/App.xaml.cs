using Autofac;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Streamster.ClientApp.Win;
using Streamster.ClientApp.Win.Services;
using Streamster.ClientCore;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Logging;
using Streamster.ClientCore.Models;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace ClientApp.Win
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static Mutex s_instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            SetTheme();

#if DEBUG
#else
            if (!SetSingleStart("Xtreamer.Client"))
            {
                MessageBox.Show("Streamster is already running", "Launch Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
#endif

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<DependencyInjection>();
            builder.RegisterModule<CoreDependencyInjection>();

            var container = builder.Build(); 

            container.Resolve<LogService>();
            Root = container.Resolve<RootModel>();
            Root.ByeByeModel = container.Resolve<ByeByeModel>();
            Root.NavigateTo(container.Resolve<LoginModel>());

            var windowStateManager = container.Resolve<IWindowStateManager>();

            // consider move this to dispatcher call
            MainWindow mainWindow = new MainWindow();
            ((WindowStateManager)windowStateManager).SetWindow(mainWindow);
            mainWindow.Show();
        }

        private bool SetSingleStart(string v)
        {
            s_instanceMutex = new Mutex(true, v, out var createdNew);
            return createdNew;
        }

        private void SetTheme()
        {
            PaletteHelper paletteHelper = new PaletteHelper();
            ITheme theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(Theme.Dark);
            theme.SetPrimaryColor(Color.FromRgb(0x10, 0x70, 0xD0));
            theme.SetSecondaryColor(Color.FromRgb(0x10, 0xB0, 0x70));
            theme.FlatButtonClick = Color.FromRgb(0x10, 0x70, 0xD0);
            paletteHelper.SetTheme(theme);
        }

        public static RootModel Root { get; private set; }
    }
}
