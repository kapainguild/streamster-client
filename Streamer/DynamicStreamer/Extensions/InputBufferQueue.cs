using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace DynamicStreamer.Extensions
{
    class InputBufferQueue : IDisposable
    {
        private volatile bool _continueProcessing = true;

        private object _queueLock = new object();

        private Queue<Packet> _queue = new Queue<Packet>();

        private readonly string _name;
        private readonly IStreamerBase _streamer;
        private readonly int _maxPackets;
        private readonly DirectXContext _dx;
        private readonly int _width;
        private readonly int _height;

        private Packet _last;
        private long _lastEnqueueTime;
        private TimerSubscription _timer;

        public InputBufferQueue(string name, IStreamerBase streamer, int maxPackets, DirectXContext dx, int width, int height)
        {
            _name = name;
            _streamer = streamer;
            _maxPackets = maxPackets;
            _dx = dx;
            _width = width;
            _height = height;

            _timer = streamer.Subscribe(300, OnTimer);
        }

        private void OnTimer()
        {
            lock (_queueLock)
            {
                if (_lastEnqueueTime != 0 && _last != null)
                {
                    var time = Core.GetCurrentTime();

                    if (_queue.Count == 0 && time - _lastEnqueueTime > 1_100_0000)
                    {
                        var packet = _streamer.PacketPool.Rent();
                        packet.CopyContentFrom(_last);
                        packet.SetPts(time);
                        _queue.Enqueue(packet);
                        _lastEnqueueTime = time;

                        Monitor.PulseAll(_queueLock);
                    }
                }
            }
        }

        public void Dequeue(Packet packet)
        {
            lock (_queueLock)
            {
                while (_continueProcessing && _queue.Count == 0)
                    Monitor.Wait(_queueLock);

                if (_queue.Count > 0)
                {
                    var dq = _queue.Dequeue();
                    packet.CopyContentFrom(dq);
                    _streamer.PacketPool.Back(dq);
                }
                else
                    throw new OperationCanceledException();
            }
        }

        public void Enqueue(IntPtr buffer, int size)
        {
            lock (_queueLock)
            {
                if (_queue.Count == _maxPackets)
                {
                    _streamer.PacketPool.Back(_queue.Dequeue());
                    Core.LogWarning($"Queue '{_name}' reached maximum");
                }

                var packet = _streamer.PacketPool.Rent();
                var now = Core.GetCurrentTime();

                if (_dx == null)
                {
                    packet.InitFromBuffer(buffer, size, now);
                }
                else
                {
                    var dx = _dx.Pool.Get("WebBrowserQueue", DirectXResource.Desc(_width,
                                                             _height,
                                                             SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                                                             SharpDX.Direct3D11.BindFlags.ShaderResource,
                                                             SharpDX.Direct3D11.ResourceUsage.Immutable),
                                                             new SharpDX.DataRectangle(buffer, _width * 4));
                    packet.InitFromDirectX(dx, now);
                }
                _queue.Enqueue(packet);

                // update last
                _streamer.PacketPool.Back(_last);
                _last = _streamer.PacketPool.Rent();
                _last.CopyContentFrom(packet);
                _lastEnqueueTime = now;

                Monitor.PulseAll(_queueLock);
            }
        }

        public void Interrupt()
        {
            lock (_queueLock)
            {
                _continueProcessing = false;
                Monitor.PulseAll(_queueLock);
            }
        }

        public void Dispose()
        {
            _timer?.Unsubscribe();
            _timer = null;
            lock (_queueLock)
            {
                foreach (var p in _queue)
                    _streamer.PacketPool.Back(p);
                _queue.Clear();
            }
        }
    }
}
