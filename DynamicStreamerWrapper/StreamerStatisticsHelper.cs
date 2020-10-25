using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Streamster.DynamicStreamerWrapper
{
    public static class StreamerStatisticsHelper
    {
        public static string[] GetCombinedError(StreamerStatistics[] list)
        {
            return list.Select(s => !string.IsNullOrWhiteSpace(s?.ErrorMsg) ? $"{GetInputOutputName(s.Id, false)}: {GetFriendlyError(s)}" : null).ToArray();
        }

        public static string GetInputOutputName(int id, bool withNumber)
        {
            return id < 0 ? "Input" :
                (withNumber ? String.Format("Output {0}", id) : "Output");
        }

        public static string GetFriendlyError(StreamerStatistics statistics)
        {
            if (statistics.Id == -1)
            {
                if (statistics.Error == -5)
                    return "Streamer unable to open Camera or Microphone. Maybe camera is in use by another application";
                if (statistics.Error == -10049)
                    return "Streamer unable to open input connnection";
            }
            else // outputs
            {
                if (statistics.Error == -138)
                    return "Streamer unable to connect stream to server";
                if (statistics.Error == -10054)
                    return "Streamer lost connection to server";
                if (statistics.Error == -1313558101)
                    return "Streamer is unable to connect to Media Server";
            }

            string originalMsg = statistics.ErrorMsg;

            if (string.IsNullOrEmpty(originalMsg))
                return null;
            string[] parts = originalMsg.Split('|');
            if (parts.Length != 5)
                return originalMsg;

            string line = new String(parts[3].Where(Char.IsDigit).ToArray());

            string[] output = { parts[0], parts[1], line, parts[4] };

            return String.Join(", ", output.Where(s => !String.IsNullOrEmpty(s)));
        }
    }
}
