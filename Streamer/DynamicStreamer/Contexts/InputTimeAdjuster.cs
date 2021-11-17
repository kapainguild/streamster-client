using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.Contexts
{
    class InputTimeAdjusterCurrentTime : IInputTimeAdjuster
    {
        public long Add(long packetTime, long currentTime) => currentTime;
    }

    class InputTimeAdjusterNone : IInputTimeAdjuster
    {
        public long Add(long packetTime, long currentTime) => packetTime;
    }

    class InputTimeAdjuster : IInputTimeAdjuster
    {
        const int Size = 60;

        bool _fullBuffer;
        int _position = 0;
        long[] _times = new long[Size];
        long _last = 0;


        public long Add(long packetTime, long currentTime)
        {
            long delta = currentTime - packetTime;
            _times[_position] = delta;

            _position++;
            if (_position == Size)
            {
                _position = 0;
                _fullBuffer = true;
            }    

            int max = _fullBuffer ? Size : _position;

            long sum = 0;
            for (int q = 0; q < max; q++)
                sum += _times[q];

            long ave = sum / max;

            long next = packetTime + ave;

            if (_last != 0)
            {
                if (next < _last)
                    next = _last;
            }
            _last = next;

            return next;
        }
    }
}
