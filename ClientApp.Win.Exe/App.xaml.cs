using Autofac;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Streamster.ClientApp.Win;
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
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppHelper.RunApp<WinResources>(this);
        }
    }
}
