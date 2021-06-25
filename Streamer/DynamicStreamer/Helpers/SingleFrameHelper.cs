using DynamicStreamer.DirectXHelpers;
using System;

namespace DynamicStreamer
{
    class SingleFrameHelper
    {
        public static FromPool<Frame> CreateFrame(FixedFrameData data, FixedFrameConfig config, IStreamerBase streamer)
        {
            if (config.Dx == null)
                return CreateFrameFFMpeg(data, config, streamer);
            return CreateFrameDirectX(data, config, streamer);
        }

        private static FromPool<Frame> CreateFrameDirectX(FixedFrameData data, FixedFrameConfig config, IStreamerBase streamer)
        {
            var res = TextureLoader.Load(config.Dx, data.Buffer);
            var frame = streamer.FramePool.Rent();
            frame.InitFromDirectX(res, 0);

            return new FromPool<Frame>(frame, streamer.FramePool);
        }

        public static FromPool<Frame> CreateFrameFFMpeg(FixedFrameData data, FixedFrameConfig config, IStreamerBase streamer)
        {
            DecoderContextFFMpeg decoder = null;
            FilterContextFFMpeg filter = null;
            Packet packet = null;
            Frame decodedFrame = null;
            Frame filteredFrame = null;

            Frame result = null;

            try
            {
                decoder = new DecoderContextFFMpeg();

                int codecId = 0;
                int pix_fmt = 0;

                switch (data.Type)
                {
                    case SingleFrameType.Png:
                        codecId = Core.Const.CODEC_ID_PNG;
                        pix_fmt = Core.Const.PIX_FMT_RGBA;
                        break;
                    case SingleFrameType.Jpg:
                        codecId = Core.Const.CODEC_ID_MJPEG;
                        pix_fmt = Core.Const.PIX_FMT_YUVJ422P;
                        break;
                    default:
                        throw new NotSupportedException();
                }

                var decoderSetup = new DecoderSetup(DecoderContextFFMpeg.Type, new CodecProperties { codec_id = codecId, format = pix_fmt }, null);

                Core.Checked(decoder.Open(decoderSetup), "Open decoder failed");

                packet = streamer.PacketPool.Rent();
                packet.InitFromBuffer(data.Buffer, Core.GetCurrentTime());
                Core.Checked(decoder.Write(packet), "Write packet to decoder");
                decodedFrame = streamer.FramePool.Rent();
                Core.Checked((int)decoder.Read(decodedFrame), "Read frame from decoder");
                var desiredWidth = config.Width;
                var desiredHeight = config.Height;
                string filterSpec = desiredWidth == decodedFrame.Properties.Width && desiredHeight == decodedFrame.Properties.Height ? "null" : $"scale=w={desiredWidth}:h={desiredHeight}";

                filter = new FilterContextFFMpeg();

                var filterSetup = new FilterSetup
                {
                    Type = FilterContextFFMpeg.Type,
                    FilterSpec = $"[in0]{filterSpec}[out]",
                    InputSetups = new[]
                    {
                        new FilterInputSetup(new FilterInputSpec
                        {
                            width = decodedFrame.Properties.Width,
                            height = decodedFrame.Properties.Height,
                            pix_fmt = decodedFrame.Properties.Format,
                            sample_aspect_ratio = new AVRational { num = 1, den = 1},
                            time_base = new AVRational { num = 1, den = 1000},
                            color_range = decoder.Config.CodecProperties.color_range,
                            BestQuality = 1
                        })
                    },
                    OutputSpec = new FilterOutputSpec
                    {
                        pix_fmt = config.PixelFormat
                    }
                };

                Core.Checked(filter.Open(filterSetup), "Failed to create filter");
                Core.Checked(filter.Write(decodedFrame, 0), "Write frame to filter");
                filteredFrame = streamer.FramePool.Rent();
                Core.Checked((int)filter.Read(filteredFrame), "Read frame from filter");

                result = filteredFrame;
                filteredFrame = null;
            }
            catch (Exception e)
            {
                Core.LogError(e, $"Failed to precreate image {data.DataId}");
            }

            decoder?.Dispose();
            filter?.Dispose();
            streamer.PacketPool.Back(packet);
            streamer.FramePool.Back(decodedFrame);
            streamer.FramePool.Back(filteredFrame);

            if (result == null)
                return null;
            return new FromPool<Frame>(result, streamer.FramePool);
        }
    }
}
