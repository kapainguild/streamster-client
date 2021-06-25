using DynamicStreamer.Contexts;
using DynamicStreamer.Screen;
using System;
using Windows.Graphics;

namespace DynamicStreamer.Extensions.ScreenCapture
{
    public class ScreenCaptureContext : IInputContext
    {
        public static string Name = "screencapture";

        private ScreenCaptureEngine _capture;
        private IStreamerBase _streamerBase;

        public ScreenCaptureContext(IStreamerBase streamerBase)
        {
            _streamerBase = streamerBase;
        }

        public InputConfig Config { get; private set; }

        public void Analyze(int duration, int streamsCount)
        {
        }

        public void Dispose()
        {
            _capture?.Dispose();
            _capture = null;
        }

        public void Interrupt()
        {
            _capture?.Interrupt();
        }

        public void Open(InputSetup setup)
        {
            var req = (ScreenCaptureRequest)setup.ObjectInput;
            bool directX = setup.Dx != null;
            _capture = new ScreenCaptureEngine(req, setup.Dx, sz => OnSizeChanged(sz, directX));
            SetConfig(req.InitialSize, directX);
        }

        private void SetConfig(SizeInt32 size, bool directX)
        {
            Config = new InputConfig(
                new InputStreamProperties[]
                {
                    new InputStreamProperties
                    {
                        CodecProps = new CodecProperties
                        {
                            codec_type = AVMediaType.AVMEDIA_TYPE_VIDEO,
                            sample_aspect_ratio = new AVRational {num = 0, den = 1},
                            codec_id = Core.Const.CODEC_ID_RAWVIDEO,
                            width = size.Width,
                            height = size.Height,
                            bits_per_coded_sample = 4*8,
                            format = directX ? Core.PIX_FMT_INTERNAL_DIRECTX : Core.Const.PIX_FMT_BGRA,

                            extradata = new byte[1024]
                        }
                    }
                }
            );
        }

        private void OnSizeChanged(SizeInt32 size, bool directX)
        {
            SetConfig(size, directX);
            _streamerBase.NoneblockingUpdate();
        }

        public void Read(Packet packet, InputSetup setup)
        {
            var loop = setup.NoneResetingOptions.LoopbackOptions;
            SizeInt32 configSize = loop != null ? new SizeInt32 { Width = loop.Width, Height = loop.Height } : new SizeInt32 { Width = 0, Height = 0 };
            _capture.Read(configSize, (b, bitPerPixel, width, height, sourceWidth, dxRes) =>
            {
                if (dxRes == null)
                    return packet.InitFromBuffer(b, bitPerPixel, width, height, sourceWidth, Core.GetCurrentTime(), true);
                else
                {
                    packet.InitFromDirectX(dxRes, Core.GetCurrentTime());
                    return true;
                }
            });
        }
    }
}
