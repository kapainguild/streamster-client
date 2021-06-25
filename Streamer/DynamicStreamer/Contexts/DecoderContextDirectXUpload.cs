using DynamicStreamer.DirectXHelpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer
{
    class DecoderContextDirectXUpload : IDecoderContext
    {
        public const string Type = nameof(DecoderContextDirectXUpload);
        private DirectXUploader _uploader;
        private DirectXContext _dx;

        private DirectXResource _currentFrame;
        private long _currentFramePts;

        public DecoderConfig Config { get; set; }

        public int Open(DecoderSetup setup)
        {
            _dx?.RemoveRef();
            _dx = setup.DirectXContext.AddRef();
            _uploader?.Dispose();
            _uploader = DirectXUploader.Create(_dx, setup.CodecProps.format, setup.CodecProps.width, setup.CodecProps.height);
            Config = new DecoderConfig();
            return 0;
        }

        public int Write(Packet packet)
        {
            _currentFramePts = packet.Properties.Pts;
            _currentFrame?.Dispose();
            _currentFrame = _uploader.Upload(packet.Properties.DataPtr, IntPtr.Zero, IntPtr.Zero);
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

        public void Dispose()
        {
            _currentFrame?.Dispose();
            _dx?.RemoveRef();
            _uploader?.Dispose();
            _uploader = null;
            _currentFrame = null;
            _dx = null;
        }
    }
}
