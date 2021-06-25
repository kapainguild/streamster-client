using SharpDX;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DynamicStreamer.DirectXHelpers
{
    public class DirectXResourcePool
    {
        private int _countInField = 0;
        private readonly DirectXContext _dx;
        private bool _active;
        private Dictionary<Texture2DDescription, LinkedList<DirectXResource>> _perType = new Dictionary<Texture2DDescription, LinkedList<DirectXResource>>();

        public DirectXResourcePool(DirectXContext dx)
        {
            _dx = dx;
            _active = true;
        }


        public (int pooled, int inField) CleanUp(DateTime now)
        {
            var old = now - TimeSpan.FromSeconds(10);

            List<DirectXResource> all = new List<DirectXResource>();

            int pooled = 0;

            lock (this)
            {
                foreach (var list in _perType.Values)
                {
                    while (list.Count > 0 && list.First.Value.InPoolTime < old)
                    {
                        all.Add(list.First.Value);
                        list.RemoveFirst();
                    }

                    pooled += list.Count;
                }
            }

            all.ForEach(s => s.CleanInternalResources());

            return (pooled, _countInField);
        }


        public void Deactivate()
        {
            List<DirectXResource> all = null;
            lock (this)
            {
                _active = false;

                all = _perType.SelectMany(s => s.Value).ToList();
                _perType.Values.ToList().ForEach(s => s.Clear());
            }

            all.ForEach(s => s.CleanInternalResources());
        }

        public DirectXResource Get(string debugName, Texture2DDescription desc) 
        {
            Interlocked.Increment(ref _countInField);

            lock (this)
            {
                if (!_perType.TryGetValue(desc, out var list))
                {
                    list = new LinkedList<DirectXResource>();
                    _perType[desc] = list;
                }

                if (list.Count > 0)
                {
                    var res = list.Last.Value;
                    list.RemoveLast();
                    res.DebugName = debugName;
                    return res;
                }
            }

            return Safe(() => new DirectXResource(_dx, new Texture2D(_dx.Device, desc), true, debugName));
        }

        private DirectXResource Safe(Func<DirectXResource> getter)
        {
            try
            {
                return getter();
            }
            catch (Exception e)
            {
                _dx.Broken(e);
            }
            return new DirectXResource(_dx, null, false, "fake");
        }

        public DirectXResource Get(string debugName, Texture2DDescription desc, DataBox dataBox) 
        {
            Interlocked.Increment(ref _countInField);
            return Safe(() => new DirectXResource(_dx, new Texture2D(_dx.Device, desc, new[] { dataBox }), false, debugName));
        }

        public DirectXResource Get(string debugName, Texture2DDescription desc, DataRectangle data) 
        {
            Interlocked.Increment(ref _countInField);
            return Safe(() => new DirectXResource(_dx, new Texture2D(_dx.Device, desc, data), false, debugName));
        }


        public void Back(DirectXResource resource)
        {
            if (resource != null)
            {
                Interlocked.Decrement(ref _countInField);

                if (resource.CommandList != null)
                {
                    resource.CommandList.Dispose();
                    resource.CommandList = null;
                }

                if (!resource.Cachable)
                {
                    resource.CleanInternalResources();
                    return;
                }

                resource.InPoolTime = DateTime.UtcNow;

                lock (this)
                {
                    if (_active)
                    {
                        _perType[resource.Texture2D.Description].AddLast(resource);
                        return;
                    }
                }

                if (resource != null)
                    resource.CleanInternalResources();
            }
        }
    }
}
