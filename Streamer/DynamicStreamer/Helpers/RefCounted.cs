using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicStreamer
{
    public class RefCounted<T> where T:IDisposable
    {
        private int _count;

        public T Instance { get; }

        public RefCounted(T t)
        {
            Instance = t;
            _count = 1;
        }

        public  RefCounted<T> AddRef()
        {
            if (Interlocked.Increment(ref _count) > 1) // otherwise it means 0 => 1
                return this;
            return null;
        }

        public void RemoveRef()
        {
            if (Interlocked.Decrement(ref _count) <= 0)
                Instance.Dispose();
        }
    }

    public class RefCountedFrame
    {
        private int _count;

        public FromPool<Frame> Instance { get; }

        public RefCountedFrame(FromPool<Frame> t)
        {
            Instance = t;
            _count = 1;
        }

        public RefCountedFrame AddRef()
        {
            if (Interlocked.Increment(ref _count) > 1) // otherwise it means 0 => 1
                return this;
            return null;
        }

        public void RemoveRef()
        {
            if (Interlocked.Decrement(ref _count) <= 0)
                Instance.Dispose();
        }
    }
}
