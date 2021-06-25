using DynamicStreamer.Extension;
using System;
using System.Threading;

namespace DynamicStreamer
{
    public class MainThreadExecutor : IMainThreadExecutor
    {
        private SynchronizationContext _syncContext;

        public MainThreadExecutor()
        {
            _syncContext = SynchronizationContext.Current;
        }

        public void Execute(Action action, bool sync)
        {
            if (!sync)
                _syncContext.Post(s => action(), null);
            else
                _syncContext.Send(s => action(), null);
        }
    }
}
