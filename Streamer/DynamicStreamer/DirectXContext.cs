using DynamicStreamer.DirectXHelpers;
using DynamicStreamer.Extension;
using Serilog;
using SharpDX.Direct3D11;
using SharpDX.WIC;
using System;
using System.Threading;

namespace DynamicStreamer
{
    public class DirectXContext : IDirectXContext, IDisposable
    {
        private int _refCount;

        private readonly IStreamerBase _streamer;

        public VideoRenderOptions CreationOptions { get; }

        public Device Device { get; private set; }

        public string VertexProfile { get; } = "vs_4_0";

        public string PixelProfile { get; } = "ps_4_0";

        public DirectXResourcePool Pool { get; }

        public IntPtr CtxNativePointer => Device.ImmediateContext.NativePointer;

        public ImagingFactory ImagingFactory2 { get; private set; }

        public AdapterInfo AdapterInfo { get; }

        public bool Nv12Supported { get; set; }

        public bool AdapterIsEqualToWindowAdapter { get;  }

        public bool IsBroken { get; set; }

        public DirectXContext(Device device, VideoRenderOptions options, AdapterInfo item2, bool adapterIsEqualToWindowAdapter, IStreamerBase streamer)
        {
            AdapterIsEqualToWindowAdapter = AdapterIsEqualToWindowAdapter;
            _refCount = 1;
            Device = device;
            CreationOptions = options;
            ImagingFactory2 = new ImagingFactory();
            Pool = new DirectXResourcePool(this);

            var nv12Support = device.CheckFormatSupport(SharpDX.DXGI.Format.NV12);

            AdapterInfo = item2;
            _streamer = streamer;
            Nv12Supported = nv12Support.HasFlag(FormatSupport.RenderTarget) && nv12Support.HasFlag(FormatSupport.Texture2D);
        }

        internal DirectXContext AddRef()
        {
            if (Interlocked.Increment(ref _refCount) > 1) // otherwise it means 0 => 1
                return this;
            return null;
        }

        internal void Broken(Exception e)
        {
            Log.Error(e, $"DirectX failure {GetHashCode()}");
            lock (this)
            {
                if (IsBroken)
                    return;
                IsBroken = true;
            }
            _streamer?.ReinitDirectX();
        }

        public void RemoveRef()
        {
            if (Interlocked.Decrement(ref _refCount) <= 0)
                Dispose();
        }

        public void Flush(DeviceContext deffered, string actionName)
        {
            using (var cl = deffered.FinishCommandList(false))
                RunOnContext(ctx => ctx.ExecuteCommandList(cl, false), actionName);
        }

        //static int counter = 1;

        public void RunOnContext(Action<DeviceContext> action, string actionName)
        {
            if (IsBroken)
                return;
            try
            {
                //counter++;
                //if (counter % 1000 == 0)
                //    Device.ImmediateContext.Dispose();

                lock (this)
                {
                    action(Device.ImmediateContext);
                }
            }
            catch (Exception e)
            {
                Broken(e);
            }
        }

        public object CreateCopy(Texture2D texture)
        {
            var d = texture.Description;
            var res = Pool.Get("CreateCopy", DirectXResource.Desc(d.Width, d.Height, d.Format));
            RunOnContext(ctx =>
            {
                ctx.CopyResource(texture, res.Texture2D);
            }, "CreateCopy");

            return res;
        }

        public void Dispose()
        {
            Device?.Dispose();
            Device = null;
            ImagingFactory2?.Dispose();
            ImagingFactory2 = null;
        }
    }
}
