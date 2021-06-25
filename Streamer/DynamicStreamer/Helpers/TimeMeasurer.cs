using Serilog;
using System;
using System.Diagnostics;

namespace DynamicStreamer
{
    public class TimeMeasurer : IDisposable
    {
        private readonly string _title;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public TimeMeasurer(string title)
        {
            _stopwatch.Start();
            _title = title;
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            Log.Information($"Time for '{_title}': {_stopwatch.ElapsedMilliseconds}ms");
        }
    }
}
