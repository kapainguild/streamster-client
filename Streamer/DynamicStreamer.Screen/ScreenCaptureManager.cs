using DynamicStreamer.Extension;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace DynamicStreamer.Screen
{
    public class ScreenCaptureManager
    {
        public const int CreateFromHandleSupported = 8;
        public const int CursorSupported = 10;

        public static ScreenCaptureManager Instance { get; } = new ScreenCaptureManager();

        public IExtensionLogger Logger { get; private set; }

        public IMainThreadExecutor MainThreadExecutor { get; private set; }

        public void Init(IExtensionLogger extensionLogger, IMainThreadExecutor mainThreadExecutor)
        {
            Logger = extensionLogger;
            MainThreadExecutor = mainThreadExecutor;
        }

        public static int GetApiContract()
        {
            var osVersion = Environment.OSVersion;
            if (osVersion.Version > new Version(6, 2))
                return GetApiContractInternal();
            return -2;
        }

        private static int GetApiContractInternal()
        {
            try
            {
                //see https://stackoverflow.com/questions/61128450/how-to-get-windows-10-version-e-g-1809-1903-1909-in-a-uwp-app-c-or-w
                for (ushort q = 11; q > 0; q--)
                    if (ApiInformation.IsApiContractPresent(typeof(Windows.Foundation.UniversalApiContract).FullName, q))
                        return q;
            }
            catch 
            {
            }
            return -1;
        }

        public IEnumerable<ScreenCaptureItem> GetPrograms()
        {
            return Process.GetProcesses()
                .Where(p => !string.IsNullOrWhiteSpace(p.MainWindowTitle) && WindowEnumerationHelper.IsWindowValidForCapture(p.MainWindowHandle))
                .Select(p => new { p, size = WindowEnumerationHelper.GetWindowSize(p.MainWindowHandle) })
                .Select(p =>  new ScreenCaptureItem(p.p.MainWindowTitle, p.p.MainWindowHandle, true, p.size.w, p.size.h));
        }

        public IEnumerable<ScreenCaptureItem> GetDisplays()
        {
            return MonitorEnumerationHelper.GetMonitors().Select(m => new ScreenCaptureItem(m.DeviceName, m.Hmon, false, 0, 0));
        }

        public async Task<GraphicsCaptureItemWrapper> UserSelectAsync(IntPtr hwnd)
        {
            var picker = new GraphicsCapturePicker();
            picker.SetWindow(hwnd);
            var item = await picker.PickSingleItemAsync();
            return item != null ? new GraphicsCaptureItemWrapper(item, "UserSelect") : null;
        }

        public GraphicsCaptureItemWrapper CreateGraphicsCaptureItem(IntPtr handle, bool isProgram)
        {
            if (isProgram)
            {
                var item = CaptureHelper.CreateItemForWindow(handle);
                if (item != null)
                    return new GraphicsCaptureItemWrapper(item, "Window");
            }
            else
            {
                var item = CaptureHelper.CreateItemForMonitor(handle);
                if (item != null)
                    return new GraphicsCaptureItemWrapper(item, "Display");
            }
            return null;
        }
    }
}
