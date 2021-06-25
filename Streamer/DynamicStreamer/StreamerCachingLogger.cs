using Serilog;
using System;
using System.Collections.Generic;

namespace DynamicStreamer
{
    public class StreamerCachingLogger
    {
        private const int QueueLimit = 5;

        private readonly Queue<StreamerCachingLoggerEntry> _items = new Queue<StreamerCachingLoggerEntry>();
        private readonly string _prefix;
        private int _lastFlushedSecond = -1;

        public StreamerCachingLogger(string prefix)
        {
            _prefix = prefix;
        }

        public void Write(LogType severity, string pattern, string message, Exception ex)
        {
            bool equal = false;
            DateTime now = DateTime.UtcNow;

            lock (this)
            {
                int q = 0;
                foreach (var item in _items)
                {
                    if (message == item.Message)
                    {
                        equal = true;
                    }
                    else if (pattern != null &&
                        item.Pattern == pattern)
                    {
                        equal = true;
                        item.Equal = false;
                    }

                    if (equal)
                    {
                        item.Dublicates++;
                        if (q != _items.Count - 1)
                            item.Continues = false;
                        break;
                    }
                    q++;
                }

                if (!equal)
                {
                    _items.Enqueue(new StreamerCachingLoggerEntry { Message = message, Pattern = pattern });
                    if (_items.Count > QueueLimit)
                    {
                        var dequeued = _items.Dequeue();
                        WriteRepeated(dequeued);
                    }
                }

                if (_lastFlushedSecond != now.Second)
                {
                    _lastFlushedSecond = now.Second;
                    foreach (var e in _items)
                        WriteRepeated(e);
                    _items.Clear();
                }
            }

            if (!equal)
            {
                if (severity == LogType.Info)
                    Log.Information(_prefix + message);
                else if (severity == LogType.Warning)
                    Log.Warning(ex, _prefix + message);
                else
                    Log.Error(ex, _prefix + message);
            }
        }

        private void WriteRepeated(StreamerCachingLoggerEntry e)
        {
            if (e.Dublicates > 0)
            {
                char continues = e.Continues ? 'C' : '-';
                char equal = e.Equal ? 'E' : '-';
                Log.Information($"{_prefix}Repeated {e.Dublicates + 1} times ({continues}{equal}) => {e.Message}");
            }
        }
    }

    public class StreamerCachingLoggerEntry
    {
        public string Message { get; set; }

        public string Pattern { get; set; }

        public int Dublicates { get; set; }

        public bool Equal { get; set; } = true;

        public bool Continues { get; set; } = true;
    }
}
