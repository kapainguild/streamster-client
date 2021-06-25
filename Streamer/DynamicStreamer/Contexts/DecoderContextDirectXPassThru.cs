namespace DynamicStreamer.Contexts
{
    class DecoderContextDirectXPassThru : IDecoderContext
    {
        public const string Type = nameof(DecoderContextDirectXPassThru);
        private long _currentFramePts;
        private RefCounted<DirectXResource> _currentResource;

        public DecoderConfig Config { get; set; }

        private DirectXContext _dx;
        private int _width;
        private int _height;

        public int Open(DecoderSetup setup)
        {
            Config = new DecoderConfig();

            if (setup.CodecProps.format == Core.Const.PIX_FMT_BGRA)
            {
                _dx = setup.DirectXContext;
                _width = setup.CodecProps.width;
                _height = setup.CodecProps.height;
            }
            return 0;
        }

        public void RemoveCurrent()
        {
            _currentResource?.RemoveRef();
            _currentResource = null;
        }

        public int Write(Packet packet)
        {
            RemoveCurrent();

            _currentFramePts = packet.Properties.Pts;

            if (_dx != null)
            {
                _currentResource = new RefCounted<DirectXResource>(_dx.Pool.Get("passthru", DirectXResource.Desc(_width,
                                                             _height,
                                                             SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                                                             SharpDX.Direct3D11.BindFlags.ShaderResource,
                                                             SharpDX.Direct3D11.ResourceUsage.Immutable),
                                                             new SharpDX.DataRectangle(packet.Properties.DataPtr, _width*4)));
            }
            else 
                _currentResource = packet.DirectXResourceRef.AddRef();
            return 0;
        }

        public ErrorCodes Read(Frame frame)
        {
            if (_currentResource == null)
                return ErrorCodes.TryAgainLater;
            else
            {
                frame.InitFromDirectX(_currentResource, _currentFramePts);
                RemoveCurrent();
                return ErrorCodes.Ok;
            }
        }

        public void Dispose()
        {
            RemoveCurrent();
        }
    }
}
