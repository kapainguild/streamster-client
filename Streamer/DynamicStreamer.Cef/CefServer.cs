using CefSharp;
using System;
using System.Threading;

namespace DynamicStreamerCef
{
    public static class CefServer
    {
        private static RequestContext s_requestContext;
        private static bool initialized;
        private static object initlock = new object();
        private static Thread s_initShutDownThread;
        private static AutoResetEvent s_Initialized = new AutoResetEvent(false);
        private static ManualResetEvent s_StartShutDown = new ManualResetEvent(false);
        private static ManualResetEvent s_IsShutDown = new ManualResetEvent(false);
        private static Exception s_initShutDownException;



        public static void EnsureInit(CefConfiguration cefConfiguration)
        {
            lock (initlock)
            {
                if (!initialized)
                {
                    initialized = true;
                    s_initShutDownThread = new Thread(() => InitShutDownThread(cefConfiguration));
                    s_initShutDownThread.Start();
                    s_Initialized.WaitOne();

                    if (s_initShutDownException != null)
                        throw s_initShutDownException;
                }
            }
        }

        private static void InitShutDownThread(CefConfiguration cefConfiguration)
        {
            try
            {
                Init(cefConfiguration);
            }
            catch (Exception e)
            {
                s_initShutDownException = e;
            }

            s_Initialized.Set();

            if (s_initShutDownException != null)
                return;

            s_StartShutDown.WaitOne();

            try
            {
                //Wait until the browser has finished closing (which by default happens on a different thread).
                //Cef.EnableWaitForBrowsersToClose(); must be called before Cef.Initialize to enable this feature
                //See https://github.com/cefsharp/CefSharp/issues/3047 for details
                Cef.WaitForBrowsersToClose();

                // Clean up Chromium objects.  You need to call this in your application otherwise
                // you will get a crash when closing.
                Cef.Shutdown();
            }
            catch { }
            s_IsShutDown.Set();
        }

        public static void Init(CefConfiguration cefConfiguration)
        {
            Cef.EnableWaitForBrowsersToClose();

            InitInternal(cefConfiguration);

            var requestContextSettings = new RequestContextSettings { CachePath = cefConfiguration.CachePathRequest };
            s_requestContext = new RequestContext(requestContextSettings);
        }

        public static void Shutdown()
        {
            if (initialized)
            {
                s_StartShutDown.Set();
                s_IsShutDown.WaitOne(1000);
            }
        }

        public static ChromiumWebBrowser CreateBrowser(string url, int fps, IRenderTarget renderTarget)
        {
            var browserSettings = new BrowserSettings();
            browserSettings.BackgroundColor = Cef.ColorSetARGB(0, 0, 0, 0);
            browserSettings.WindowlessFrameRate = fps;
            return new ChromiumWebBrowser(url, browserSettings, s_requestContext, true, renderTarget);
        }

        public static void InitInternal(CefConfiguration cefConfiguration)
        {
            var settings = new CefSettingsNoAbstract();
            var browserProcessHandler = new BrowserProcessHandler();

            settings.LogSeverity = cefConfiguration.LogVerbose ? LogSeverity.Verbose : LogSeverity.Info;
            settings.LogFile = cefConfiguration.LogFile;
            settings.WindowlessRenderingEnabled = true;

            //For OffScreen it doesn't make much sense to enable audio by default, so we disable it.
            //this can be removed in user code if required
            settings.CefCommandLineArgs.Add("mute-audio");

            settings.BackgroundColor = Cef.ColorSetARGB(0, 0, 0, 0);

            //The location where cache data will be stored on disk. If empty an in-memory cache will be used for some features and a temporary disk cache for others.
            //HTML5 databases such as localStorage will only persist across sessions if a cache path is specified. 
            settings.RootCachePath = cefConfiguration.CachePathRoot;
            //If non-null then CachePath must be equal to or a child of RootCachePath
            //We're using a sub folder.
            //
            settings.CachePath = cefConfiguration.CachePathGlobal;

            //NOTE: The following function will set all three params
            settings.SetOffScreenRenderingBestPerformanceArgs();
            //settings.CefCommandLineArgs.Add("disable-gpu");
            //settings.CefCommandLineArgs.Add("disable-gpu-compositing");
            //settings.CefCommandLineArgs.Add("enable-begin-frame-scheduling");

            //settings.CefCommandLineArgs.Add("disable-gpu-vsync"); //Disable Vsync

            //Enables Uncaught exception handler
            settings.UncaughtExceptionStackSize = 10;


            // Off Screen rendering (WPF/Offscreen)
            if (settings.WindowlessRenderingEnabled)
            {
                //Disable Direct Composition to test https://github.com/cefsharp/CefSharp/issues/1634
                //settings.CefCommandLineArgs.Add("disable-direct-composition");

                // DevTools doesn't seem to be working when this is enabled
                // http://magpcss.org/ceforum/viewtopic.php?f=6&t=14095
                //settings.CefCommandLineArgs.Add("enable-begin-frame-scheduling");
            }

            var proxy = ProxyConfig.GetProxyInformation();
            switch (proxy.AccessType)
            {
                case InternetOpenType.Direct:
                    //Don't use a proxy server, always make direct connections.
                    settings.CefCommandLineArgs.Add("no-proxy-server");
                    break;
                case InternetOpenType.Proxy:
                    settings.CefCommandLineArgs.Add("proxy-server", proxy.ProxyAddress);
                    break;
                case InternetOpenType.PreConfig:
                    settings.CefCommandLineArgs.Add("proxy-auto-detect");
                    break;
            }

            //This must be set before Cef.Initialized is called
            CefSharpSettings.FocusedNodeChangedEnabled = true;

            //Exit the subprocess if the parent process happens to close
            //This is optional at the moment
            //https://github.com/cefsharp/CefSharp/pull/2375/
            CefSharpSettings.SubprocessExitIfParentProcessClosed = true;


            //if (DebuggingSubProcess)
            //{
            //    var architecture = Environment.Is64BitProcess ? "x64" : "x86";
            //    settings.BrowserSubprocessPath = Path.GetFullPath("..\\..\\..\\..\\..\\CefSharp.BrowserSubprocess\\bin.netcore\\" + architecture + "\\Debug\\netcoreapp3.1\\CefSharp.BrowserSubprocess.exe");
            //}


            //Disable WebAssembly
            //settings.JavascriptFlags = "--noexpose_wasm";


            // The following options control accessibility state for all frames.
            // These options only take effect if accessibility state is not set by IBrowserHost.SetAccessibilityState call.
            // --force-renderer-accessibility enables browser accessibility.
            // --disable-renderer-accessibility completely disables browser accessibility.
            //settings.CefCommandLineArgs.Add("force-renderer-accessibility");
            //settings.CefCommandLineArgs.Add("disable-renderer-accessibility");



            // Set Google API keys, used for Geolocation requests sans GPS.  See http://www.chromium.org/developers/how-tos/api-keys
            // Environment.SetEnvironmentVariable("GOOGLE_API_KEY", "");
            // Environment.SetEnvironmentVariable("GOOGLE_DEFAULT_CLIENT_ID", "");
            // Environment.SetEnvironmentVariable("GOOGLE_DEFAULT_CLIENT_SECRET", "");

            // Widevine CDM registration - pass in directory where Widevine CDM binaries and manifest.json are located.
            // For more information on support for DRM content with Widevine see: https://github.com/cefsharp/CefSharp/issues/1934
            //Cef.RegisterWidevineCdm(@".\WidevineCdm");

            //Chromium Command Line args
            //http://peter.sh/experiments/chromium-command-line-switches/
            //NOTE: Not all relevant in relation to `CefSharp`, use for reference purposes only.
            //CEF specific command line args
            //https://bitbucket.org/chromiumembedded/cef/src/master/libcef/common/cef_switches.cc?fileviewer=file-view-default
            //IMPORTANT: For enabled/disabled command line arguments like disable-gpu specifying a value of "0" like
            //settings.CefCommandLineArgs.Add("disable-gpu", "0"); will have no effect as the second argument is ignored.

            //settings.RemoteDebuggingPort = 8088;


            //Async Javascript Binding - methods are queued on TaskScheduler.Default.
            //Set this to true to when you have methods that return Task<T>
            //CefSharpSettings.ConcurrentTaskExecution = true;

            //Legacy Binding Behaviour - Same as Javascript Binding in version 57 and below
            //See issue https://github.com/cefsharp/CefSharp/issues/1203 for details
            //CefSharpSettings.LegacyJavascriptBindingEnabled = true;

            //NOTE: Set this before any calls to Cef.Initialize to specify a proxy with username and password
            //One set this cannot be changed at runtime. If you need to change the proxy at runtime (dynamically) then
            //see https://github.com/cefsharp/CefSharp/wiki/General-Usage#proxy-resolution
            //CefSharpSettings.Proxy = new ProxyOptions(ip: "127.0.0.1", port: "8080", username: "cefsharp", password: "123");

            //settings.UserAgent = "CefSharp Browser" + Cef.CefSharpVersion; // Example User Agent
            //settings.CefCommandLineArgs.Add("renderer-process-limit", "1");
            //settings.CefCommandLineArgs.Add("renderer-startup-dialog");
            //settings.CefCommandLineArgs.Add("enable-media-stream"); //Enable WebRTC
            //settings.CefCommandLineArgs.Add("no-proxy-server"); //Don't use a proxy server, always make direct connections. Overrides any other proxy server flags that are passed.
            //settings.CefCommandLineArgs.Add("debug-plugin-loading"); //Dumps extra logging about plugin loading to the log file.
            //settings.CefCommandLineArgs.Add("disable-plugins-discovery"); //Disable discovering third-party plugins. Effectively loading only ones shipped with the browser plus third-party ones as specified by --extra-plugin-dir and --load-plugin switches
            //settings.CefCommandLineArgs.Add("enable-system-flash"); //Automatically discovered and load a system-wide installation of Pepper Flash.
            //settings.CefCommandLineArgs.Add("allow-running-insecure-content"); //By default, an https page cannot run JavaScript, CSS or plugins from http URLs. This provides an override to get the old insecure behavior. Only available in 47 and above.
            //https://peter.sh/experiments/chromium-command-line-switches/#disable-site-isolation-trials
            //settings.CefCommandLineArgs.Add("disable-site-isolation-trials");
            //NOTE: Running the Network Service in Process is not something CEF officially supports
            //It may or may not work for newer versions.
            //settings.CefCommandLineArgs.Add("enable-features", "CastMediaRouteProvider,NetworkServiceInProcess");

            //settings.CefCommandLineArgs.Add("enable-logging"); //Enable Logging for the Renderer process (will open with a cmd prompt and output debug messages - use in conjunction with setting LogSeverity = LogSeverity.Verbose;)
            //settings.LogSeverity = LogSeverity.Verbose; // Needed for enable-logging to output messages

            //settings.CefCommandLineArgs.Add("disable-extensions"); //Extension support can be disabled
            //settings.CefCommandLineArgs.Add("disable-pdf-extension"); //The PDF extension specifically can be disabled

            //Load the pepper flash player that comes with Google Chrome - may be possible to load these values from the registry and query the dll for it's version info (Step 2 not strictly required it seems)
            //settings.CefCommandLineArgs.Add("ppapi-flash-path", @"C:\Program Files (x86)\Google\Chrome\Application\47.0.2526.106\PepperFlash\pepflashplayer.dll"); //Load a specific pepper flash version (Step 1 of 2)
            //settings.CefCommandLineArgs.Add("ppapi-flash-version", "20.0.0.228"); //Load a specific pepper flash version (Step 2 of 2)

            //Audo play example
            //settings.CefCommandLineArgs["autoplay-policy"] = "no-user-gesture-required";

            //NOTE: For OSR best performance you should run with GPU disabled:
            // `--disable-gpu --disable-gpu-compositing --enable-begin-frame-scheduling`
            // (you'll loose WebGL support but gain increased FPS and reduced CPU usage).
            // http://magpcss.org/ceforum/viewtopic.php?f=6&t=13271#p27075
            //https://bitbucket.org/chromiumembedded/cef/commits/e3c1d8632eb43c1c2793d71639f3f5695696a5e8

            bool performDependencyCheck = false;// !DebuggingSubProcess;

            if (!Cef.Initialize(settings, performDependencyCheck: performDependencyCheck, browserProcessHandler: browserProcessHandler))
            {
                throw new Exception("Unable to Initialize Cef");
            }
        }


        public class CefSettingsNoAbstract : CefSettingsBase
        {
        }
    }
}
