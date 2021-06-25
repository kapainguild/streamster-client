using System;
using System.Linq;

namespace DynamicStreamer.Helpers
{
    public class OverloadController : IDisposable
    {
        private IStreamerBase _streamer;
        private TimerSubscription _timer;

        private int _overloadLevel = 0;
        private int _takeEveryOffset = 0;
        private int _overloadPrintCounter = 0;

        public OverloadController(IStreamerBase streamer)
        {
            _streamer = streamer;

            _timer = streamer.Subscribe(90, OnTimer);
        }

        public void Dispose()
        {
            _timer.Unsubscribe();
        }

        private void OnTimer()
        {
            int overload = _streamer.ResourceManager.GetOverload();

            var (level, takeEvery) = GetOverloadLevel(overload);
            

            if (_overloadLevel != level)
            {
                if (_overloadLevel == 0)
                {
                    Core.LogWarning("Start overload" + GetOverloadString(level), "Overload");
                    _overloadPrintCounter = 0;
                }

                _overloadLevel = level;
                _takeEveryOffset = takeEvery;
            }

            if (_overloadPrintCounter++ == 11 &&_overloadLevel > 0)
            {
                _overloadPrintCounter = 0;
                Core.LogWarning("Overload" + GetOverloadString(level), "Overload");
            }
        }

        private string GetOverloadString(int level)
        {
            var details = _streamer.ResourceManager.GetOverloadDetails();
            var sum = details.Sum(s => s.size);
            return $" level {level} ({sum}) " + string.Join(" ", details.Select(s => $"{s.name}({s.size})"));
        }

        private (int level, int takeEvery) GetOverloadLevel(int overload)
        {
            if (overload < 8)
                return (0, 0);
            if (overload < 15)
                return (1, 1);
            if (overload < 20)
                return (2, 2);

            return (3, 4);
        }

        internal void Increment(ref long currentTimeTick, long increment)
        {
            if (_takeEveryOffset == 0)
            {
                currentTimeTick += increment;
            }
            else
            {
                long devided = (currentTimeTick + increment - 1) >> _takeEveryOffset;
                currentTimeTick = (devided + 1) << _takeEveryOffset;
            }
        }
    }
}
