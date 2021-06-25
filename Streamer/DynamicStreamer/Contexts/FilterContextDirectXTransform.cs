using DynamicStreamer.DirectXHelpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.Contexts
{
    class FilterContextDirectXTransform : IFilterContext
    {
        public static string Type = nameof(FilterContextDirectXTransform);
        private DirectXContext _dx;
        private DirectXTransformer _transform;

        private DirectXResource _currentFrame;
        private long _currentFramePts;

        private readonly IStreamerBase _streamer;

        public void Dispose()
        {
            _transform?.Dispose();
            _currentFrame?.Dispose();
            _dx?.RemoveRef();
            _currentFrame = null;
            _dx = null;
            _transform = null;
        }

        public FilterContextDirectXTransform(IStreamerBase streamer)
        {
            _streamer = streamer;
        }

        public int Open(FilterSetup setup)
        {
            _dx?.RemoveRef();
            _dx = setup.DirectXContext.AddRef();
            var setupInput = setup.InputSetups[0].FilterSpec;
            _transform = DirectXTransformer.Create(_dx, setup.OutputSpec.pix_fmt, setupInput.width, setupInput.height);
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
            _currentFrame = _transform.Process(frame);
            return 0;
        }
    }
}
