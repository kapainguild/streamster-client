using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
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

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern IntPtr MonitorFromWindow(HandleRef handle, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(HandleRef hmonitor, [In, Out]MONITORINFOEX info);

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
