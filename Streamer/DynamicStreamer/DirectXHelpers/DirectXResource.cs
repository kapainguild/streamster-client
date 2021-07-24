using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;

namespace DynamicStreamer
{
    public class DirectXResource : IDisposable
    {
        private DirectXContext _dx;

        public Texture2D Texture2D { get; set; }

        public bool Cachable { get; }

        public string DebugName { get; set; }

        public CommandList CommandList { get; set; }

        public DateTime InPoolTime { get; set; }

        public DirectXResource(DirectXContext dx, Texture2D texture2D, bool cachable, string name)
        {
            _dx = dx.AddRef();
            Texture2D = texture2D;
            Cachable = cachable;
            DebugName = name;
        }

        public DirectXContext GetDx() => _dx;


        public static Texture2DDescription Desc(int width, int height, 
            Format format = Format.B8G8R8A8_UNorm, 
            BindFlags bindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
            ResourceUsage usage = ResourceUsage.Default, 
            ResourceOptionFlags resourceOptionFlags = ResourceOptionFlags.None,
            CpuAccessFlags cpuAccessFlags = CpuAccessFlags.None)
        {
            return new Texture2DDescription()
            {
                Usage = usage,
                BindFlags = bindFlags,
                Format = format,

                Width = width,
                Height = height,

                CpuAccessFlags = cpuAccessFlags,
                OptionFlags = resourceOptionFlags,
                SampleDescription = new SampleDescription(1, 0),
                ArraySize = 1,
                MipLevels = 1
            };
        }


        public ShaderResourceView GetShaderResourceView()
        {
            if (Texture2D == null)
                throw new InvalidOperationException("Texture2D is null");

            return new ShaderResourceView(_dx.Device, Texture2D); 
        }

        public RenderTargetView GetRenderTargetView()
        {
            if (Texture2D == null)
                throw new InvalidOperationException("Texture2D is null");

            return new RenderTargetView(_dx.Device, Texture2D);
        }

        internal void CleanInternalResources()
        {
            Texture2D?.Dispose();
            Texture2D = null;

            _dx.RemoveRef();
            _dx = null;
        }

        public void Dispose()
        {
            _dx.Pool.Back(this);
        }

        internal IntPtr GetSharedHandle()
        {
            var sharedResource = Texture2D.QueryInterface<SharpDX.DXGI.Resource>();
            return sharedResource.SharedHandle;
        }
    }
}
