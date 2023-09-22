#define DEBUG_LOCK_no

using Serilog;
using System;
using System.Threading;

namespace DeltaModel
{
    public interface ILockerProvider
    {
        IDisposable GetLocker();

        void SetManager(DeltaModelManager manager);
    }

    public class SingleThreadLockerProvider : ILockerProvider
    {
        public IDisposable GetLocker()
        {
            return new SingleThreadLocker();
        }

        public void SetManager(DeltaModelManager m)
        {
        }

        public class SingleThreadLocker : IDisposable
        {
            public SingleThreadLocker()
            {
#if DEBUG
                if (SynchronizationContext.Current == null)
                    throw new InvalidOperationException("Change to data is called on none UI thread");
#endif
                if (SynchronizationContext.Current == null)
                    Log.Warning("Access from none UI thread");
            }

            public void Dispose()
            {
            }
        }
    }

    public class MultiThreadLockerProvider : ILockerProvider
    {
        private DeltaModelManager _manager;
        private MultiThreadLockerRelease _releaseLocker;

        public IDisposable GetLocker()
        {
#if DEBUG_
            return new MultiThreadLockerDebug(_manager);
#else
            Monitor.Enter(_manager);
            return _releaseLocker;
#endif
        }

        public void SetManager(DeltaModelManager manager)
        {
            _manager = manager;
            _releaseLocker = new MultiThreadLockerRelease(manager);
        }

        public class MultiThreadLockerRelease : IDisposable
        {
            private readonly DeltaModelManager _manager;

            public MultiThreadLockerRelease(DeltaModelManager manager) => _manager = manager;

            public void Dispose() => Monitor.Exit(_manager);
        }

        public class MultiThreadLockerDebug : IDisposable
        {
            private readonly DeltaModelManager _manager;
            private readonly int _threadId;

#if DEBUG_LOCK
            [DllImport("kernel32.dll")]
            private static extern int GetCurrentThreadId();

            private readonly static Dictionary<int, string> s_perThread = new Dictionary<int, string>();
#endif

            public MultiThreadLockerDebug(DeltaModelManager manager)
            {
                Monitor.Enter(manager);
#if DEBUG_LOCK
                s_perThread[GetCurrentThreadId()] = new StackTrace().ToString();
#endif
                _manager = manager;
                _threadId = Thread.CurrentThread.ManagedThreadId;
            }

            public void Dispose()
            {
                Monitor.Exit(_manager);

                if (_threadId != Thread.CurrentThread.ManagedThreadId)
                {
                    Log.Error("Lock released from thread other then it was aquired");
                    throw new InvalidOperationException("Lock released from thread other then it was aquired");
                }
            }
        }
    }
}
