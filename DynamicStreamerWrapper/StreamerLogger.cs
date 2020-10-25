using System;
using System.Collections.Generic;
using System.Linq;


namespace Streamster.DynamicStreamerWrapper
{
    public class StreamerLogger
    {
        private const int QueueLimit = 50;
        private const int DequeCount = 10;

        private readonly List<StreamerLoggerItem> _items = new List<StreamerLoggerItem>();
        private readonly string _prefix;

        public StreamerLogger(string prefix)
        {
            _prefix = prefix;
        }

        public void Write(int errorCode, string errorMessage, string pattern)
        {
            var item = new StreamerLoggerItem
            {
                Time = DateTime.UtcNow,
                Message = errorMessage?.TrimEnd('\n'),
                Pattern = pattern
            };

            lock (this)
            {
                int start = Math.Max(0, _items.Count - 2);
                for (int q = _items.Count - 1; q >= start; q--)
                {
                    var compare = _items[q];
                    bool equal = false;
                    if (compare.Message == item.Message)
                    {
                        equal = true;
                    }
                    else if (pattern != null)
                    {
                        if (item.Pattern == compare.Pattern)
                        {
                            equal = true;
                            compare.Equal = false;
                        }
                    }


                    if (equal)
                    {
                        compare.Dublicates++;
                        if (q != _items.Count - 1)
                            compare.Continues = false;
                        return;
                    }
                }


                _items.Add(item);
                if (_items.Count > QueueLimit)
                {
                    _items.RemoveRange(0, DequeCount);
                    _items.Add(new StreamerLoggerItem
                    {
                        Message = "LogDeque",
                        Time = DateTime.UtcNow,
                    });
                }
            }
        }

        public string GetLogMessage(StreamerLoggerItem item)
        {
            if (item.Dublicates == 0)
            {
                return $"{_prefix}({item.Time:ss.fff}) {item.Message}";
            }
            else
            {
                char continues = item.Continues ? 'C' : '-';
                char equal = item.Equal ? 'E' : '-';

                return $"{_prefix}({item.Time:ss.fff}) ({item.Dublicates + 1} {continues}{equal}) {item.Message}";
            }
        }

        public List<StreamerLoggerItem> Flush()
        {
            lock (this)
            {
                var res =_items.ToList();
                _items.Clear();
                return res;
            }
        }
    }

    public class StreamerLoggerItem
    {
        public DateTime Time { get; set; }

        public string Message { get; set; }

        public string Pattern { get; set; }

        public int Dublicates { get; set; }

        public bool Equal { get; set; } = true;

        public bool Continues { get; set; } = true;
    }
}
