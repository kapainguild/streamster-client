using DynamicStreamer.Extensions.WebBrowser;
using DynamicStreamer.Screen;
using DynamicStreamerCef;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer
{
    public static class ExtensionsManager
    {
        public static MainThreadExecutor MainThreadExecutor { get; set; }


        public static void InitOnMain()
        {
            MainThreadExecutor = new MainThreadExecutor();
        }

        public static void Init()
        {
            PluginContextSetup.TryToLoad();

            ScreenCaptureManager.Instance.Init(new ExtensionLogger("sc: "), MainThreadExecutor);
        }

        public static void Shutdown()
        {
            PluginContextSetup.Unload();
            CefServer.Shutdown();
        }
    }
}
