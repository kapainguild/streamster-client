using DynamicStreamer.Nodes;
using SharpDX;
using SharpDX.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynamicStreamer.Helpers
{
    public class ResourceManager : IDisposable
    {
        private IStreamerBase _streamer;
        private readonly TimerSubscription _timer5s;
        private readonly TimerSubscription _timer1s;
        private StreamerRuntimeConfig _runtimeConfig = new StreamerRuntimeConfig();
        private int _lastVersionConfigDump = -1;
        private int _configDumpCounter = -1;
        private List<StatisticItem> _current1SecondStatistics;
        private DateTime _lastMeasured1SecondStatistics = DateTime.MinValue;
        private TimeSpan _lastMeasured1SecondStatisticsSpan = TimeSpan.FromSeconds(1);

        public ResourceManager(IStreamerBase streamer)
        {
            _streamer = streamer;
            _timer5s = streamer.Subscribe(5000, On5Seconds);
            _timer1s = streamer.Subscribe(990, On1Seconds);
        }

        private void On1Seconds()
        {
            
            lock(this)
            {
                var ordered = _runtimeConfig.Nodes.OrderBy(s => s.item.Name.SortId()).ToList();
                _current1SecondStatistics = ordered.Select(s => s.item).OfType<IStatProvider>().Select(s => s.GetStat()).ToList();

                var now = DateTime.UtcNow;
                if (_lastMeasured1SecondStatistics != DateTime.MinValue)
                    _lastMeasured1SecondStatisticsSpan = now - _lastMeasured1SecondStatistics;
                _lastMeasured1SecondStatistics = now;
            }
        }

        private void On5Seconds()
        {
            var c = _runtimeConfig;

            var now = DateTime.UtcNow;

            var dxStat = c.Dx?.Pool.CleanUp(now);
            var packetStat = _streamer.PacketPool.CleanUp();
            var frameStat = _streamer.FramePool.CleanUp();

            var fails = _streamer.GetDxFailureCounter();
            var failsString = fails > 0 ? $"; {fails} FAILURES" : "";

            // statistics
            lock (this)
            {
                if (_current1SecondStatistics == null)
                    return;
                var msg = string.Join("  ", _current1SecondStatistics.Select(s => $"{s.Name}({s.Data})"));
                Core.LogInfo("stat: " + msg + "; " + GetPoolData(dxStat, packetStat, frameStat) + failsString);
            }


            // dump config
            var ordered = c.Nodes.OrderBy(s => s.item.Name.SortId()).ToList();
            if (_lastVersionConfigDump != c.Version || 
                _configDumpCounter++ > 10)
            {
                _lastVersionConfigDump = c.Version;
                _configDumpCounter = 0;

                var configs = ordered.Where(s => s.setup != null).Select(s => $"{s.item.Name}[{s.setup.ToString().TrimEnd()}]");
                Core.LogInfo("config: " + string.Join("  ", configs));

                if (Configuration.EnableObjectTracking)
                {
                    var objs = ObjectTracker.FindActiveObjects();
                    var grouped = objs.GroupBy(s => s.Object.Target?.GetType().Name).Select(s => $"{s.Key}:{s.Count()}");
                    Core.LogInfo("dx: " + string.Join("; ", grouped));
                }
            }
        }

        private string GetPoolData((int pooled, int inField)? dxStat, (int pooled, int inField) packetStat, (int pooled, int inField) frameStat)
        {
            var dx = dxStat == null ? "" : $"Dx[{dxStat.Value.pooled}:{dxStat.Value.inField}] ";
            return dx + $"P[{packetStat.pooled}:{packetStat.inField}] F[{frameStat.pooled}:{frameStat.inField}]";
        }

        public void SetConfig(StreamerRuntimeConfig runtimeConfig)
        {
            _runtimeConfig = runtimeConfig;
            Configuration.EnableObjectTracking = runtimeConfig.EnableObjectTracking; 
        }

        public List<StatisticItem> GetStatistics(out TimeSpan period)
        {
            lock (this)
            {
                period = _lastMeasured1SecondStatisticsSpan;
                return _current1SecondStatistics?.ToList() ?? new List<StatisticItem>();
            }
        }

        public int GetOverload()
        {
            return _runtimeConfig.Nodes.Select(s => s.item).OfType<ISourceQueueHolder>().Where(s => s.InputQueueForOverload != null).Sum(s => s.InputQueueForOverload.Count);
        }

        public List<(string name, int size)> GetOverloadDetails()
        {
            return _runtimeConfig.Nodes.Select(s => s.item).OfType<ISourceQueueHolder>().Where(s => s.InputQueueForOverload != null).Select(s => (s.Name.ToString(), s.InputQueueForOverload.Count)).ToList();
        }

        public void Dispose()
        {
            _timer5s.Unsubscribe();
        }
    }
}
