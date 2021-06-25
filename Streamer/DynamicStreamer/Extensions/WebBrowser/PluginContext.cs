using DynamicStreamer.Contexts;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicStreamer.Extensions.WebBrowser
{
    public class PluginContext : WebBrowserContext
    {
        public static string PluginName = "webbasedplugin";

        [DllImport("webbasedplugin.dll", CharSet = CharSet.Unicode)] 
        private static extern int Plugin_GetUrl(IntPtr handle, [Out] StringBuilder url, [In] int nSize);

        public PluginContext(IStreamerBase streamer, MainThreadExecutor mainThreadExecutor) : base(streamer, mainThreadExecutor)
        {
        }

        public override void Open(InputSetup setup)
        {
            var handle = PluginContextSetup.GetHandle();
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException($"PluginContextSetup returned Zero");

            StringBuilder url = new StringBuilder(2001);

            int code = Plugin_GetUrl(handle, url, 2000);
            if (code != 0)
                throw new InvalidOperationException($"Plugin_GetUrl returned {code}");
            
            base.Open(setup with { Input = url.ToString() });
        }
    }


}
