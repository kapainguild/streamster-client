using Serilog;
using System;
using System.Diagnostics;

namespace Streamster.ClientData
{
    public class TimeMeasurer : IDisposable
    {
        private readonly string _title;
        private Stopwatch _stopwatch = new Stopwatch();

        public TimeMeasurer(string title)
        {
            _stopwatch.Start();
            _title = title;
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            Log.Information($"Time for '{_title}': '{_stopwatch.ElapsedMilliseconds}'");
        }
    }
}
