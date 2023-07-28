using Castle.Core.Logging;
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
                //if (SynchronizationContext.Current == null)
                //    throw new InvalidOperationException("Change to data is called on none UI thread");
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
        DeltaModelManager _manager;

        public IDisposable GetLocker()
        {
            return new MultiThreadLocker(_manager);
        }

        public void SetManager(DeltaModelManager manager)
        {
            _manager = manager;
        }

        public class MultiThreadLocker : IDisposable
        {
            private readonly DeltaModelManager _manager;
#if DEBUG
            private readonly int _threadId;
#endif

            public MultiThreadLocker(DeltaModelManager manager)
            {
                Monitor.Enter(manager);
                _manager = manager;

#if DEBUG
                _threadId = Thread.CurrentThread.ManagedThreadId;
#endif
            }

            public void Dispose()
            {
                Monitor.Exit(_manager);

#if DEBUG
                if (_threadId != Thread.CurrentThread.ManagedThreadId)
                {
                    Log.Error("Lock released from thread other then it was aquired");
                    throw new InvalidOperationException("Lock released from thread other then it was aquired");
                }
#endif
            }
        }
    }
}
