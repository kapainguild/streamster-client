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
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace ClientApp.Win
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public static class AppHelper
    {
        private static Mutex s_instanceMutex;

        public static void RunApp<T>(Application app)
        {
            RemoveCefSharpShortcuts();

            app.Resources.MergedDictionaries.Add(new BundledTheme
            {
                BaseTheme = BaseTheme.Dark,
                PrimaryColor = PrimaryColor.Indigo,
                SecondaryColor = SecondaryColor.Blue
            });

            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml")
            });

            SetTheme();

#if DEBUG
#else
            if (!SetSingleStart("Streamster.Client"))
            {
                MessageBox.Show("The application is already running", "Launch Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
                app.Shutdown();
                return;
            }
#endif

            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<DependencyInjection>();
            builder.RegisterModule<CoreDependencyInjection>();
            builder.Add<IAppResources, T>();

            var container = builder.Build();

            container.Resolve<LogService>();
            var root = container.Resolve<RootModel>();
            root.ByeByeModel = container.Resolve<ByeByeModel>();
            root.NavigateTo(container.Resolve<LoginModel>());

            var windowStateManager = container.Resolve<IWindowStateManager>();

            // consider move this to dispatcher call
            MainWindow mainWindow = new MainWindow();
            mainWindow.DataContext = root;
            mainWindow.Root = root;


            ((WindowStateManager)windowStateManager).SetWindow(mainWindow);
            mainWindow.Show();
        }

        private static void RemoveCefSharpShortcuts()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string desktopShortcut = Path.Combine(desktop, "CefSharp.lnk");
                if (File.Exists(desktopShortcut))
                    File.Delete(desktopShortcut);

                string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

                var cefDir = Path.Combine(programs, "The CefSharp Authors");
                if (Directory.Exists(cefDir))
                    Directory.Delete(cefDir, true);
            }
            catch
            {
            }
        }

        private static bool SetSingleStart(string v)
        {
            s_instanceMutex = new Mutex(true, v, out var createdNew);
            return createdNew;
        }

        private static void SetTheme()
        {
            PaletteHelper paletteHelper = new PaletteHelper();
            ITheme theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(Theme.Dark);
            theme.SetPrimaryColor(Color.FromRgb(0x10, 0x70, 0xD0));
            theme.SetSecondaryColor(Color.FromRgb(0x10, 0xB0, 0x70));
            theme.FlatButtonClick = Color.FromRgb(0x10, 0x70, 0xD0);
            paletteHelper.SetTheme(theme);
        }
    }
}
