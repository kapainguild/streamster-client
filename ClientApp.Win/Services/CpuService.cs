using Serilog;
using Streamster.ClientCore;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Support;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Streamster.ClientApp.Win.Services
{
    public class CpuService : ICpuService, IDisposable
    {
        private readonly PerformanceCounter _cpu = new PerformanceCounter()
        {
            CategoryName = "Processor",
            CounterName = "% Processor Time",
            InstanceName = "_Total"
        };
        private readonly CoreData _coreData;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Dictionary<int, ProcessInfo> _processes = new Dictionary<int, ProcessInfo>();
        private AverageIntValue _loadAverage = new AverageIntValue(3, false);

        public CpuService(CoreData coreData)
        {
            _coreData = coreData;
            TaskHelper.RunUnawaited(StartCpuProcessing(), "Cpu processing");
            //WatchUiHangs();
        }

        private void WatchUiHangs() // TODO
        {
            var sw = new Stopwatch();
            bool started = false;

            new DispatcherTimer(TimeSpan.FromMilliseconds(10), DispatcherPriority.Send, (sender, args) =>
            {
                lock (sw)
                {
                    started = true;
                    sw.Restart();
                }
            }, Application.Current.Dispatcher);

            Timer timer = null;
            timer = new Timer(state =>
            {
                lock (sw)
                {
                    var elapsed = sw.ElapsedMilliseconds;
                    if (started && elapsed > 200 && timer != null)
                    {
                        Debugger.Break();
                    }
                }

            }, null, TimeSpan.FromMilliseconds(2800), TimeSpan.FromMilliseconds(10));
        }

        private async Task StartCpuProcessing()
        {
            await TaskHelper.GoToPool().ConfigureAwait(false);

            while(!_cts.IsCancellationRequested)
            {
                try
                {
                    int load = (int)_cpu.NextValue();
                    load = _loadAverage.AddValue(load);
#if DEBUG
                    ProcessLoad[] processes = null;
#else
                    var processes = GetProcesses();
#endif
                    _coreData.RunOnMainThread(() => SetLoad(load, processes));
                }
                catch
                {
                    Log.Warning($"Get CPU load failed");
                }
                await Task.Delay(1000, _cts.Token);
            }
        }

        private int _tickCounter = -2;

        private ProcessLoad[] GetProcesses()
        {
            _tickCounter++;
            if (_tickCounter % 3 == 0 || _tickCounter == -1)
            {
                var measured = new List<ProcessInfo>();
                var processes = Process.GetProcesses();
                int procCount = Environment.ProcessorCount;
                foreach (var proc in processes)
                {
                    if (!_processes.TryGetValue(proc.Id, out var pi))
                    {
                        pi = new ProcessInfo
                        {
                            Name = proc.ProcessName,
                            LastMeasurement = DateTime.MinValue
                        };
                        _processes.Add(proc.Id, pi);
                    }

                    if (pi.IsFailed)
                        continue;

                    try
                    {
                        var now = DateTime.Now;
                        var currentValue = proc.TotalProcessorTime;
                        
                        if (pi.LastMeasurement != DateTime.MinValue)
                        {
                            pi.Load = (currentValue - pi.LastProcessorTime).TotalMilliseconds / ((now - pi.LastMeasurement).TotalMilliseconds * procCount);
                            measured.Add(pi);
                        }
                        pi.LastMeasurement = now;
                        pi.LastProcessorTime = currentValue;
                    }
                    catch
                    {
                        pi.IsFailed = true;
                    }
                }

                return measured
                    .GroupBy(s => s.Name)
                    .Select(s => new { n = s.Key, l = s.Sum(r => r.Load) })
                    .OrderByDescending(s => s.l).Take(3)
                    .Select(s => AdjustLoad(new ProcessLoad { Name = s.n, Load = (int)(s.l*100) }))
                    .ToArray();
            }
            return null;
        }

        private ProcessLoad AdjustLoad(ProcessLoad processLoad)
        {
            if (processLoad.Name == "Streamster.ClientApp.Win")
                processLoad.Name = "Streamster";

            if (processLoad.Load > 99)
                processLoad.Load = 99;
            return processLoad;
        }

        private void SetLoad(int load, ProcessLoad[] loads)
        {
            //TODO: lock
            if (_coreData.ThisDevice?.KPIs != null)
            {
                var kpis = _coreData.ThisDevice.KPIs;
                var cpu = _coreData.GetOrCreate(() => kpis.Cpu, v => kpis.Cpu = v);
                cpu.Load = load;

                if (load < 75)
                    cpu.State = IndicatorState.Ok;
                else if (load < 90)
                    cpu.State = IndicatorState.Warning;
                else
                    cpu.State = IndicatorState.Error;

                if (loads != null)
                    cpu.Top = loads;
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }

    internal class ProcessInfo
    {
        public string Name { get; set; }

        public DateTime LastMeasurement { get; set; }

        public bool IsFailed { get; set; }

        public TimeSpan LastProcessorTime { get; set; }

        public double Load { get; set; }
    }
}
