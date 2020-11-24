using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Streamster.ClientApp.Win.Services
{
    class MonitorHelper
    {
        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
        public class MONITORINFOEX
        {
            internal int cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
            internal RECT rcMonitor = new RECT();
            internal RECT rcWork = new RECT();
            internal int dwFlags = 0;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            internal char[] szDevice = new char[32];
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }


        [DllImport("user32.dll")]
        static extern uint EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

        [DllImport("shcore.dll")]
        internal static extern uint GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        delegate bool EnumMonitorsDelegate(IntPtr monitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr lparam);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern IntPtr MonitorFromWindow(HandleRef handle, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern uint GetMonitorInfo(HandleRef hmonitor, [In, Out]MONITORINFOEX info);

        public static Rect GetMonitorWorkingArea(Window window)
        {
            WindowInteropHelper windowInteropHelper = new WindowInteropHelper(window);
            var hMonitor = MonitorFromWindow(new HandleRef(null, windowInteropHelper.Handle), MONITOR_DEFAULTTONEAREST);
            MONITORINFOEX info = new MONITORINFOEX();
            GetMonitorInfo(new HandleRef(null, hMonitor), info);

            GetDpi(window, out var dpiX, out var dpiY);

            var rc = info.rcWork;
            return new Rect(rc.left / dpiX, rc.top / dpiX, (rc.right - rc.left) / dpiX, (rc.bottom - rc.top) / dpiY);
        }

        public static Rect[] GetMonitorsWorkingAreas()
        {
            try
            {
                List<IntPtr> monitors = new List<IntPtr>();
                Checked(1, "EnumDisplayMonitors", EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (a, b, c, d) => { monitors.Add(a); return true; }, IntPtr.Zero));

                return monitors.Select(handle =>
                {
                    Checked(0, "GetDpiForMonitor", GetDpiForMonitor(handle, 0, out var x, out var y));
                    MONITORINFOEX info = new MONITORINFOEX();
                    Checked(1, "GetMonitorInfo", GetMonitorInfo(new HandleRef(null, handle), info));

                    var rc = info.rcWork;
                    var dpiX = x / 96.0;
                    var dpiY = y / 96.0;
                    return new Rect(rc.left / dpiX, rc.top / dpiX, (rc.right - rc.left) / dpiX, (rc.bottom - rc.top) / dpiY);
                }).ToArray();
            }
            catch(Exception e)
            {
                Log.Warning(e, "GetMonitorsWorkingAreas failed");
                return null;
            }
        }

        private static void Checked(uint ideal, string name, uint errorcode)
        {
            if (ideal == 0)
            {
                if (errorcode != 0)
                    throw new InvalidOperationException($"{name} failed with {errorcode}");
            }
            else
            {
                if (errorcode == 0)
                    throw new InvalidOperationException($"{name} failed with {errorcode}");
            }
        }

        private static void GetDpi(Window wnd, out double dpiX, out double dpiY)
        {
            PresentationSource source = PresentationSource.FromVisual(wnd);
            if (source?.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }
            else
            {
                dpiX = 1;
                dpiY = 1;
            }
        }
    }
}
