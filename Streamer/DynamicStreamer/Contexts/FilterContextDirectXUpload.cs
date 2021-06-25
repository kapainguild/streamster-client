using DynamicStreamer.DirectXHelpers;

namespace DynamicStreamer.Contexts
{
    class FilterContextDirectXUpload : IFilterContext
    {
        public static string Type = nameof(FilterContextDirectXUpload);
        private DirectXContext _dx;
        private DirectXUploader _uploader;

        private DirectXResource _currentFrame;
        private long _currentFramePts;


        public void Dispose()
        {
            _uploader?.Dispose();
            _currentFrame?.Dispose();
            _dx?.RemoveRef();
            _currentFrame = null;
            _dx = null;
            _uploader = null;
        }

        public int Open(FilterSetup setup)
        {
            _dx?.RemoveRef();
            _dx = setup.DirectXContext.AddRef();
            var setupInput = setup.InputSetups[0].FilterSpec;
            _uploader = DirectXUploader.Create(_dx, setupInput.pix_fmt, setupInput.width, setupInput.height);
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
            _currentFrame = _uploader.Upload(frame.Properties.DataPtr0, frame.Properties.DataPtr1, frame.Properties.DataPtr2);
            return 0;
        }
    }
}
