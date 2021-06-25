using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DynamicStreamer
{
    public class ProcessingPool
    {
        private readonly object _monitor = new object();
        private readonly Queue<ProcessingItem> _queue = new Queue<ProcessingItem>();
        private List<Thread> _processors = new List<Thread>();
        private volatile bool _continueProcessing = true;


        public void StartProcessing(int threads)
        {
            _processors = Enumerable.Range(0, threads).Select(s =>
            {
                var thread = new Thread(() => OnProcessingThread())
                {
                    Name = $"Streamer:ProcessingPool {s}"
                };
                thread.Start();
                return thread;
            }).ToList();
        }

        public void StopProcessing()
        {
            lock (_monitor)
            {
                _continueProcessing = false;
                Monitor.PulseAll(_monitor);
            }

            _processors.ForEach(s => s.Join());
        }

        public void Enqueue(ProcessingItem item)
        {
            lock(_monitor)
            {
                _queue.Enqueue(item);
                Monitor.Pulse(_monitor);
            }
        }

        private void OnProcessingThread()
        {
            while (_continueProcessing)
            {
                ProcessingItem item = null;

                lock (_monitor)
                {
                    while (!_queue.TryDequeue(out item) && _continueProcessing)
                        Monitor.Wait(_monitor);
                }

                if (item != null)
                {
                    item.Process();
                }
            }
        }
    }

    public class ProcessingItem
    {
        public ProcessingItem(Action process)
        {
            Process = process;
        }

        public Action Process { get; set; }
    }
}
