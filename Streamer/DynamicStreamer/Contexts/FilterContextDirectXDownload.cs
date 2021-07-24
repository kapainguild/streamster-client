using DynamicStreamer.DirectXHelpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.Contexts
{
    class FilterContextDirectXDownload : IFilterContext
    {
        public static string Type = nameof(FilterContextDirectXDownload);
        private DirectXContext _dx;
        private DirectXDownloader _downloader;

        private Frame _currentFrame;
        private readonly IStreamerBase _streamer;

        public void Dispose()
        {
            _downloader?.Dispose();
            _streamer.FramePool.Back(_currentFrame);
            _dx?.RemoveRef();
            _currentFrame = null;
            _dx = null;
            _downloader = null;
        }

        public FilterContextDirectXDownload(IStreamerBase streamer)
        {
            _streamer = streamer;
        }

        public int Open(FilterSetup setup)
        {
            _dx?.RemoveRef();
            _dx = setup.DirectXContext.AddRef();
            var setupInput = setup.InputSetups[0].FilterSpec;
            _downloader = DirectXDownloader.Create(_dx, setup.OutputSpec.pix_fmt, setupInput.width, setupInput.height);
            return 0;
        }

        public ErrorCodes Read(Frame frame)
        {
            if (_currentFrame == null)
                return ErrorCodes.TryAgainLater;
            else
            {
                frame.CopyContentFrom(_currentFrame);
                _streamer.FramePool.Back(_currentFrame);
                _currentFrame = null;
                return ErrorCodes.Ok;
            }
        }

        public int Write(Frame frame, int inputNo)
        {
            if (_currentFrame == null)
                _currentFrame = _streamer.FramePool.Rent();
            if (!_downloader.Download(frame, _currentFrame))
            {
                _streamer.FramePool.Back(_currentFrame);
                _currentFrame = null;
            }
            return 0;
        }
    }
}
