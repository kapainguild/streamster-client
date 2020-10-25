using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Streamster.DynamicStreamerWrapper
{
    public class Streamer : IDynamicStreamerCallback
    {
        private DynamicStreamer _streamer;
        private readonly decimal _frequency;
        private readonly StreamerLogger _streamerLogger;
        private CallbackBridge _directFrameCallbackBridge;

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);

        public Streamer(StreamerLogger streamerLogger)
        {
            _streamer = new DynamicStreamer();
            _streamer.SetCallback(this);

            QueryPerformanceFrequency(out var frequency);
            _frequency = frequency;
            _streamerLogger = streamerLogger;
        }

        public void SetDirectFrameCallback(Action<int, int, int, long> notifyFrame)
        {
            if (notifyFrame != null)
            {
                _directFrameCallbackBridge = new CallbackBridge(notifyFrame);
                _streamer.SetDirectFrameCallback(_directFrameCallbackBridge);
            }
            else
            {
                _streamer.SetDirectFrameCallback(null);
                _directFrameCallbackBridge = null;
            }
        }

        class CallbackBridge : IDynamicStreamerDecoderCallback
        {
            private Action<int, int, int, long> _notifyFrame;

            public CallbackBridge(Action<int, int, int, long> notifyFrame)
            {
                _notifyFrame = notifyFrame;
            }

            public void NotifyFrame(int width, int height, int length, long data)
            {
                _notifyFrame(width, height, length, data);
            }
        }


        public void Shutdown()
        {
            // it takes a while to shutdown (especially camera outputs)
            if (_streamer != null)
            {
                _streamer.SetCallback(null);
                //Thread.Sleep(150);
                var streamer = _streamer;
                _streamer = null;
                Marshal.FinalReleaseComObject(streamer);
            }
        }

        public int SetOutputCallback(Action<int, int, int, long> onFrame, int id)
        {
            if (onFrame == null)
            {
                _streamer.RemoveOutput(id);
                return -1;
            }
            else
            {
                _directFrameCallbackBridge = new CallbackBridge(onFrame);
                return _streamer.AddOutput(null, null, null, _directFrameCallbackBridge);
            }
        }

        public void SetInput(string type, string input, string options, int fps, int width, int height)
        {
            _streamer.SetInput(type, input, options, fps, width, height);
        }

        public int AddOutput(string type, string output, string options)
        {
            return _streamer.AddOutput(type, output, options, null);
        }

        public void RemoveOutput(int id)
        {
            _streamer.RemoveOutput(id);
        }

        public StreamerStatistics[] GetStreamerStatistics()
        {
            return ProcessStatistics(_streamer.GetStatistics());
        }

        public static int GetBitrateFromStatistics(long value)
        {
            return (int)(value * 8 / 1000);
        }

        public static int GetMegabytesFromStatistics(long value)
        {
            return (int)(value / 1024 / 1024);
        }

        private StreamerStatistics[] ProcessStatistics(Array array)
        {
            var list = array.OfType<IDynamicStreamerStatistics>().ToList();

            List<StreamerStatistics> result = new List<StreamerStatistics>();

            foreach (IDynamicStreamerStatistics input in list)
            {
                var vals = input.GetValues();
                long[] v = new long[vals.GetLength(0)];
                for (int q = 0; q < v.Length; q++)
                {
                    v[q] = (long)vals.GetValue(q);
                }

                StreamerStatistics stat = result.FirstOrDefault(c => c.Id == input.id);
                if (stat == null)
                {
                    stat = new StreamerStatistics { Id = input.id };
                    result.Add(stat);
                }

                decimal seconds = input.Interval / _frequency;
                if (input.Overall == 0)
                {
                    stat.CurrentValues = GetStatisticsValues(v, seconds);// v.Select((i, idx) => idx == 4 ? i : i / correction).ToArray();
                }
                else
                {
                    stat.Error = input.GetError(out var msg);
                    stat.ErrorMsg = stat.Error == 0 ? null : msg;
                    stat.Interval = (double)seconds;

                    stat.OverallValues = GetStatisticsValues(v, 1m);//,.Select(i => (decimal)i).ToArray();
                }
            }
            return result.ToArray();
        }

        private StreamerStatisticsValues GetStatisticsValues(long[] v, decimal correction)
        {
            return new StreamerStatisticsValues
            {
                Transferred = (long)((v[(int)StatisticType.statisticTypeAudioBytes] + v[(int)StatisticType.statisticTypeVideoBytes]) / correction),
                Drops = (int)v[(int)StatisticType.statisticTypeDropped],
                Errors = (int)v[(int)StatisticType.statisticTypeErrors],
                QueueSize = (int)v[(int)StatisticType.statisticTypeProcessingTime],
                Frames = (int)Math.Round((v[(int)StatisticType.statisticTypeVideoFrames]) / correction),
            };
        }

        public void NotifyError(int errorCode, string errorMessage, string pattern)
        {
            _streamerLogger.Write(errorCode, errorMessage, pattern);
        }

        public void SetFilter(string filter)
        {
            _streamer.SetFilter(filter);
        }

        public void SetEncoder(string videoCodec, string videoOptions, string videoCodecFallback, string videoOptionsFallback, int videoMaxBitrate, string audioCodec, string audioOptions, int audioMaxBitrate)
        {
            _streamer.SetEncoder(videoCodec, videoOptions, videoCodecFallback, videoOptionsFallback, videoMaxBitrate, audioCodec, audioOptions, audioMaxBitrate);
        }
    }
}
