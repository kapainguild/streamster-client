using System;
using System.Runtime.InteropServices;

namespace DynamicStreamer.Helpers
{
    public class CpuLoadMeasurer
    {
        private int _processors;
        private DateTime _startMeasurement;
        private long _kernelStart;
        private long _userStart;
        private long _kernelLast;
        private long _userLast;
        private DateTime _last;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetProcessTimes(IntPtr hProcess, out long lpCreationTime, out long lpExitTime, out long lpKernelTime, out long lpUserTime);
        
        public CpuLoadMeasurer()
        {
            _processors = Environment.ProcessorCount;
        }

        public void Start()
        {
            _startMeasurement = DateTime.UtcNow;

            GetProcessTimes(System.Diagnostics.Process.GetCurrentProcess().Handle, out var ct, out var et, out _kernelStart, out _userStart);

            _last = _startMeasurement;
            _userLast = _userStart;
            _kernelLast = _kernelStart;
        }

        public (double load, double seconds, double currentLoad) GetLoad()
        {
            var now = DateTime.UtcNow;
            GetProcessTimes(System.Diagnostics.Process.GetCurrentProcess().Handle, out var ct, out var et, out var kernel, out var user);



            var delta = now - _startMeasurement;
            var timeDelta = _processors * delta.Ticks;
            var loadDelta = 100 * (kernel + user - _kernelStart - _userStart);


            var delta2 = now - _last;
            var timeDelta2 = _processors * delta2.Ticks;
            var loadDelta2 = 100 * (kernel + user - _kernelLast - _userLast);

            _last = now;
            _userLast = user;
            _kernelLast = kernel;

            return ((double)loadDelta / (double)timeDelta, delta.Seconds,
                    (double)loadDelta2 / (double)timeDelta2);
        }
    }
}
