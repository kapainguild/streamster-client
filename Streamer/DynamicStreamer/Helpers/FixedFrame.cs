using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer
{

    public class FixedFrameData
    {

        public FixedFrameData(string dataId, byte[] buffer, SingleFrameType type)
        {
            DataId = dataId;
            Buffer = buffer;
            Type = type;
        }

        public string DataId { get; set; }

        public byte[] Buffer { get; set; }

        public SingleFrameType Type { get; set; }

        public override bool Equals(object obj)
        {
            return obj is FixedFrameData data &&
                   DataId == data.DataId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(DataId);
        }
    }

    public class FixedFrameConfig
    {
        public FixedFrameConfig(int scaledWidth, int scaledHeight, int v, DirectXContext dx)
        {
            Width = scaledWidth;
            Height = scaledHeight;
            PixelFormat = v;
            Dx = dx;
        }

        public int Width { get; set; }

        public int Height { get; set; }

        public int PixelFormat { get; set; }

        public DirectXContext Dx { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is FixedFrameConfig config)
            {
                if (Dx != null)
                {
                    return Dx == config.Dx;
                }
                else
                {
                    return Dx == config.Dx &&
                           Width == config.Width &&
                            Height == config.Height &&
                            PixelFormat == config.PixelFormat;
                }
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Width);
        }
    }


    public class FixedFrame : IDisposable
    {
        private FixedFrameData _data;
        private FixedFrameConfig _config;

        public RefCountedFrame Frame { get; set; }

        public void Dispose()
        {
            Frame?.RemoveRef();
        }

        public void Update(FixedFrameData data, FixedFrameConfig config, IStreamerBase streamer)
        {
            if (!Equals(data, _data) ||
                !Equals(config, _config))
            {
                Frame?.RemoveRef();
                Frame = null;

                _data = data;
                _config = config;

                if (_data?.DataId != null)
                {
                    var res = SingleFrameHelper.CreateFrame(_data, _config, streamer);
                    if (res != null)
                        Frame = new RefCountedFrame(res);
                }
            }
        }
    }
}
