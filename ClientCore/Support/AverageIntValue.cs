using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Streamster.ClientCore.Support
{
    public class AverageIntValue
    {
        private readonly int _count;
        private readonly bool _optimistic;
        private LinkedList<int> _values = new LinkedList<int>();

        public AverageIntValue(int count, bool optimistic)
        {
            _count = count;
            _optimistic = optimistic;
        }

        public int AddValue(int value)
        {
            _values.AddLast(value);
            if (_values.Count > _count)
            {
                _values.RemoveFirst();
            }
            return GetAverage();
        }

        public bool Any() => _values.Count > 0;

        public int GetAverage()
        {
            if (_optimistic)
            {
                var threshold = (int)(_values.Last.Value * 0.75);
                int sum = 0;
                int count = 0;
                var next = _values.Last;
                while (next != null && next.Value >= threshold)
                {
                    sum += next.Value;
                    count++;
                    next = next.Previous;
                }
                return sum / count;

            }
            else return (int)_values.Average();
        }

        public bool TryGetAverage(out int ave)
        {
            if (Any())
            {
                ave = GetAverage();
                return true;
            }
            ave = 0;
            return false;
        }

        public bool TryGetLast(out int last)
        {
            if (Any())
            {
                last = _values.Last();
                return true;
            }
            last = 0;
            return false;
        }

        public void Clear() => _values.Clear();
    }
}
