using System;

namespace Streamster.DynamicStreamerWrapper
{
    public class StreamerStatistics
    {
        public int Id { get; set; }

        public double Interval { get; set; }

        public string ErrorMsg { get; set; }

        public int Error { get; set; }

        public StreamerStatisticsValues CurrentValues { get; set; }

        public StreamerStatisticsValues OverallValues { get; set; }
    }

    public class StreamerStatisticsValues
    {
        public long Transferred { get; set; }

        public int QueueSize { get; set; }

        public int Errors { get; set; }

        public int Drops { get; set; }

        public int Frames { get; set; }
    }
}
