using System;
using System.Collections.Generic;

namespace DynamicStreamer.Rtmp
{

    internal class IdProvider
    {
        private readonly uint _minValue;
        private readonly uint _maxValue;
        private uint _current;
        private HashSet<uint> _allocated = new HashSet<uint> ();

        public IdProvider(uint minValue, uint maxValue)
        {
            _minValue = minValue;
            _maxValue = maxValue;
            _current = minValue;
        }

        public uint Get()
        {
            if (_allocated.Count >= _maxValue - _minValue - 1)
                throw new InvalidOperationException("Too many allocated streams");
            while (true)
            {
                if (!_allocated.Contains (_current))
                {
                    _allocated.Add (_current);
                    return _current;
                }

                _current++;

                if (_current >= _maxValue)
                    _current = _minValue;

            }
        }

        public void Release(uint val)
        {
            lock (this)
                _allocated.Remove(val);
        }

        internal void InitRange(IEnumerable<uint> enumerable)
        {
            lock (this)
                _allocated = new HashSet<uint>(enumerable);
        }
    }
}
