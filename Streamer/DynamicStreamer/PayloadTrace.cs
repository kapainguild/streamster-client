using DynamicStreamer.Nodes;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer
{
    public class PayloadTrace
    {
        public static int Frequency = 1;

        public int SequenceNo { get; set; }

        public PayloadTrace Previous { get; set; }

        public DateTime ReceivedStamp { get; set; }

        public NodeName ReceivedBy { get; set; }

        public DateTime SentStamp { get; set; }

        public NodeName SentBy { get; set; }

        public List<PayloadTrace> PreviousOther { get; set; }

        public void Received(NodeName by)
        {
            ReceivedStamp = DateTime.UtcNow;
            ReceivedBy = by;
        }

        public void AddPrevious(PayloadTrace prev)
        {
            if (PreviousOther == null)
                PreviousOther = new List<PayloadTrace>();
            PreviousOther.Add(prev);
        }

        public static PayloadTrace Create(NodeName by, PayloadTrace previous = null, int sequenceNo = 0)
        {
            if (Frequency == 0)
                return null;
            return new PayloadTrace
            {
                SentBy = by,
                SentStamp = DateTime.UtcNow,
                Previous = previous,
                SequenceNo = sequenceNo
            };
        }

        internal PayloadTrace Clone()
        {
            return new PayloadTrace
            {
                SentBy = SentBy,
                SentStamp = SentStamp,
                ReceivedBy = ReceivedBy,
                ReceivedStamp = ReceivedStamp,
                Previous = Previous,
                SequenceNo = SequenceNo
            };
        }

        public void Dump()
        {
            

        }

        public string GetDump()
        {
            StringBuilder sb = new StringBuilder();
            RenderDump(DateTime.UtcNow, sb);
            return sb.ToString();
        }

        public void RenderDump(DateTime baseTime, StringBuilder sb)
        {
            if (ReceivedBy != null)
                sb.Append($"{ReceivedBy}/{(baseTime - ReceivedStamp).TotalMilliseconds:F0} ");

            if (SentBy != null)
                sb.Append($"{SentBy}/{(baseTime - SentStamp).TotalMilliseconds:F0} ");

            if (SequenceNo > 0)
                sb.Append($"Seq-{SequenceNo} ");


            if (PreviousOther != null)
            {
                sb.Append($"[ ");
                Previous.RenderDump(baseTime, sb);
                foreach (var other in PreviousOther)
                {
                    sb.Append($"|| ");
                    other.RenderDump(baseTime, sb);
                }

                sb.Append($"] ");
            }
            else if (Previous != null)
            {
                Previous.RenderDump(baseTime, sb);
            }
        }
    }
}
