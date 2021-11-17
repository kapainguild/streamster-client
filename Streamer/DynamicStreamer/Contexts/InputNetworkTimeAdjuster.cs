using System;

namespace DynamicStreamer.Contexts
{
    class InputNetworkTimeAdjuster : IInputTimeAdjuster
    {
        private long _minValue = long.MaxValue;
        private int _timeCompensatorCounter = 0;

        public long Add(long packetTime, long currentTime)
        {
            _timeCompensatorCounter++;
            if (_timeCompensatorCounter > 350) // every apx 5 sec add 1 ms
            {
                _timeCompensatorCounter = 0;
                _minValue += 10000;
            }

            var currentDelta = currentTime - packetTime;
            _minValue = Math.Min(currentDelta, _minValue);

            return _minValue + packetTime;
        }
    }
}
