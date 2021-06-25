using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.Contexts
{
    class FilterContextNull : IFilterContext
    {
        public static string Type = nameof(FilterContextNull);

        public void Dispose()
        {
        }

        public FilterContextNull()
        {
        }

        public int Open(FilterSetup setup)
        {
            return 0;
        }

        public ErrorCodes Read(Frame frame)
        {
            return ErrorCodes.TryAgainLater;
        }

        public int Write(Frame frame, int inputNo)
        {
            return (int)ErrorCodes.NullFilter;
        }
    }
}
