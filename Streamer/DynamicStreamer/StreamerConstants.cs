using System.Linq;
using System.Runtime.InteropServices;

namespace DynamicStreamer
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct StreamerConstants
    {
        public int PIX_FMT_RGBA;
        public int PIX_FMT_BGRA;
        public int PIX_FMT_ARGB;
        public int PIX_FMT_ABGR;
        public int PIX_FMT_RGB24;
        public int PIX_FMT_BGR24;

        public int PIX_FMT_YUVJ422P;
        public int PIX_FMT_YUV422P;

        public int PIX_FMT_YUV420P;
        public int PIX_FMT_YUVJ420P;
        public int PIX_FMT_YUVA420P;
        public int PIX_FMT_NV12;
        public int PIX_FMT_NV21;

        public int PIX_FMT_YUYV422;

        public int SAMPLE_FMT_S16;
        public int SAMPLE_FMT_FLTP;
        public int SAMPLE_FMT_FLT;

        public int CODEC_ID_PNG;
        public int CODEC_ID_MJPEG;
        public int CODEC_ID_RAWVIDEO;
        public int CODEC_ID_PCM_S16LE;
        public int CODEC_ID_PCM_F32LE;

        internal string GetCodecName(int codec_id)
        {
            if (codec_id == CODEC_ID_MJPEG) return "mjpeg";
            if (codec_id == CODEC_ID_PNG) return "png";
            if (codec_id == CODEC_ID_RAWVIDEO) return "raw";
            if (codec_id == CODEC_ID_PCM_S16LE) return "raw_s16";
            if (codec_id == CODEC_ID_PCM_F32LE) return "raw_f32";

            return $"codec  ??? ({codec_id})";
        }

        public string GetAudioFormat(int format)
        {
            if (format == SAMPLE_FMT_S16) return "s16";
            if (format == SAMPLE_FMT_FLTP) return "fltp";
            if (format == SAMPLE_FMT_FLT) return "flt";

            return $"sample_fmt ??? ({format})";
        }

        public string GetVideoFormat(int format)
        {
            if (format == -1) return "Dx";
            if (format == PIX_FMT_RGBA) return "rgba";
            if (format == PIX_FMT_BGRA) return "bgra";
            if (format == PIX_FMT_ARGB) return "argb";
            if (format == PIX_FMT_ABGR) return "abgr";
            if (format == PIX_FMT_RGB24) return "rgb";
            if (format == PIX_FMT_BGR24) return "bgr";
            if (format == PIX_FMT_YUVJ422P) return "yuvj422p";
            if (format == PIX_FMT_YUV422P) return "yuv422p";
            if (format == PIX_FMT_YUV420P) return "yuv420p";
            if (format == PIX_FMT_YUVJ420P) return "yuvj420p";
            if (format == PIX_FMT_YUVA420P) return "yuva420p";
            if (format == PIX_FMT_NV12) return "nv12";
            if (format == PIX_FMT_NV21) return "nv21";
            if (format == PIX_FMT_YUYV422) return "yuyv422";

            return $"pix_fmt ??? ({format})";
        }
    }


    public class StreamerConstants2
    {
        public PixelFormatGroup Yuv420 { get; }
        public PixelFormatGroup Rgb { get; }

        public StreamerConstants2(StreamerConstants c)
        {
            Yuv420 = new PixelFormatGroup
            {
                BlendType = 0,
                MainFormats = new[] { c.PIX_FMT_YUV420P, c.PIX_FMT_YUVJ420P, c.PIX_FMT_YUVA420P, c.PIX_FMT_NV12, c.PIX_FMT_NV21 },
                OverlayFormats = new[] { c.PIX_FMT_YUVA420P }
            };
            Yuv420.ConcatFormats = Yuv420.MainFormats.Union(Yuv420.OverlayFormats).ToArray();

            Rgb = new PixelFormatGroup
            {
                BlendType = 1,
                MainFormats = new[] { c.PIX_FMT_BGR24, c.PIX_FMT_RGB24, c.PIX_FMT_ARGB, c.PIX_FMT_RGBA, c.PIX_FMT_ABGR, c.PIX_FMT_BGRA },
                OverlayFormats = new[] { c.PIX_FMT_ARGB, c.PIX_FMT_RGBA, c.PIX_FMT_ABGR, c.PIX_FMT_BGRA }
            };
            Rgb.ConcatFormats = Rgb.MainFormats.Union(Rgb.OverlayFormats).ToArray();
        }
    }

    public class PixelFormatGroup
    {
        public int BlendType { get; set; }

        public int[] MainFormats { get; set; }

        public int[] OverlayFormats { get; set; }

        public int[] ConcatFormats { get; set; }
    }
}
