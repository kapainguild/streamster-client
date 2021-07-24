
namespace DynamicStreamer.Contexts
{
    class FilterContextDirectXPassThru : IFilterContext
    {
        public static string Type = nameof(FilterContextDirectXPassThru);
        private DirectXContext _dx;

        private DirectXResource _currentFrame;
        private long _currentFramePts;
        private int _width;
        private int _height;


        public void Dispose()
        {
            _currentFrame?.Dispose();
            _dx?.RemoveRef();
            _currentFrame = null;
            _dx = null;
        }

        public int Open(FilterSetup setup)
        {
            _dx?.RemoveRef();
            _dx = setup.DirectXContext.AddRef();
            var setupInput = setup.InputSetups[0].FilterSpec;
            _width = setupInput.width;
            _height = setupInput.height;
            return 0;
        }

        public ErrorCodes Read(Frame frame)
        {
            if (_currentFrame == null)
                return ErrorCodes.TryAgainLater;
            else
            {
                frame.InitFromDirectX(_currentFrame, _currentFramePts);
                _currentFrame = null;
                return ErrorCodes.Ok;
            }
        }

        public int Write(Frame frame, int inputNo)
        {
            _currentFramePts = frame.Properties.Pts;
            _currentFrame?.Dispose();
            _currentFrame = _dx.Pool.Get("passthru2", DirectXResource.Desc(_width,
                                                             _height,
                                                             SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                                                             SharpDX.Direct3D11.BindFlags.ShaderResource,
                                                             SharpDX.Direct3D11.ResourceUsage.Immutable),
                                                             new SharpDX.DataRectangle(frame.Properties.DataPtr0, _width * 4));
            return 0;
        }
    }
}
