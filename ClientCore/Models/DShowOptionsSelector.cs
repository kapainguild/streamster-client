using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientData.Model;
using System;
using System.Linq;

namespace Streamster.ClientCore.Models
{
    public static class DShowOptionsSelector
    {
        private const string Eq = "^";
        private const string Sep = "`";

        private const int UnknownBufferSize = 40000 * 2000;
        private static string UnknownOptions = $"fflags{Eq}nobuffer{Sep}rtbufsize{Eq}{UnknownBufferSize}";

        public static string GetDeviceName(LocalSource localDevice)
        {
            if (localDevice.Name == null)
            {
                Log.Error($"Local device {localDevice.Id} has no Name");
                return "name_unknown";
            }

            if (!localDevice.Name.Contains(':')) // cannot use : as it is seprator between audio:video parts
                return localDevice.Name;
            else
                return localDevice.Id.Replace(":", "_"); // ffmpeg support selection by id
        }

        public static string GetAudioOptions(LocalAudioSource device)
        {
            var def = $"fflags{Eq}nobuffer{Sep}audio_buffer_size{Eq}50";

            var caps = device.Capabilities;
            if (caps != null && caps.Length > 0)
            {
                var ideal = caps.FirstOrDefault(s => s.MinimumChannels <= 2 && 2 <= s.MaximumChannels &&
                                                     s.MinimumSampleFrequency <= 44100 && 44100 <= s.MaximumSampleFrequency);

                if (ideal != null)
                    return def + $"{Sep}sample_rate{Eq}44100{Sep}channels{Eq}2";
            }
            return def;
        }

        public static string GetVideoOptions(LocalVideoSource device, int fps, Resolution resolution, ISceneItem item)
        {
            var caps = device.Capabilities;
            if (caps == null || caps.Length == 0)
            {
                Log.Warning($"Video device {device.Name} does not contain capabilities");
                return UnknownOptions;
            }

            var optimalResolution = resolution;
            if (item.ZoomBehavior == ZoomResolutionBehavior.Always)
                optimalResolution = new Resolution(3840, 2160); // 4k
            else if(item.ZoomBehavior == ZoomResolutionBehavior.DependingOnZoom)
                optimalResolution = new Resolution((int)(optimalResolution.Width * item.Rect.W / item.Ptz.W), (int)(optimalResolution.Height * item.Rect.H / item.Ptz.H));

            if (caps.Length == 1)
                return GetVideoOptions(caps[0], fps);

            var ideal = caps.FirstOrDefault(s => s.Fmt == LocalVideoSourceCapabilityFormat.Raw && s.W == optimalResolution.Width && s.H == optimalResolution.Height && s.MinF <= fps && fps <= s.MaxF);

            if (ideal != null)
                return GetVideoOptions(ideal, fps);

            var max = caps.Select(c => new { cap = c, score = GetScore(c, fps, optimalResolution) }).Aggregate((i1, i2) => i1.score > i2.score ? i1 : i2).cap;

            return GetVideoOptions(max, fps);
        }

        private static string GetVideoOptions(LocalVideoSourceCapability cap, int fps)
        {
            return $"video_size{Eq}{cap.W}x{cap.H}{Sep}" +
                           GetInputFps(cap, fps) +
                           $"fflags{Eq}nobuffer{Sep}" +
                           GetFormatString(cap) +
                           $"rtbufsize{Eq}{GetVideoBufferSize(cap.Fmt, cap.W)}"; 
        }

        private static string GetFormatString(LocalVideoSourceCapability cap)
        {
            switch (cap.Fmt)
            {
                case LocalVideoSourceCapabilityFormat.Raw:      return $"pixel_format{Eq}yuyv422{Sep}";
                case LocalVideoSourceCapabilityFormat.Empty:    return $"pixel_format{Eq}bgr24{Sep}"; 
                case LocalVideoSourceCapabilityFormat.MJpeg:    return $"vcodec{Eq}mjpeg{Sep}";
                case LocalVideoSourceCapabilityFormat.I420:     return $"pixel_format{Eq}yuv420p{Sep}";
                case LocalVideoSourceCapabilityFormat.NV12:     return $"pixel_format{Eq}nv12{Sep}";

                default: return "";
            }
        }

        private static string GetInputFps(LocalVideoSourceCapability cap, int fps)
        {
            int result = -1;
            if (cap.MinF <= fps && fps <= cap.MaxF)
                result = fps;
            else if (cap.MinF > fps && cap.MinF == Math.Round(cap.MinF, 0))
                result = (int)Math.Round(cap.MinF, 0);
            else if (fps > cap.MaxF && cap.MaxF == Math.Round(cap.MaxF, 0))
                result = (int)Math.Round(cap.MaxF, 0);

            if (result > 0)
                return $"framerate{Eq}{result}{Sep}";
            else 
                return "";
        }

        private static int GetScore(LocalVideoSourceCapability c, int fps, Resolution resolution)
        {
            int score = 0;

            if (fps < c.MinF)
                score -= ((int)c.MinF - fps);
            else if (fps > c.MaxF)
                score -= (fps - (int)c.MaxF) * 2;

            if ((double)resolution.Height / resolution.Width != (double)c.H / c.W)
                score -= 10;

            double diff = (double)c.W / resolution.Width;

            if (diff < 1.0) //cap lower
                score -= (int)((1.0 - diff) * 30); // half of width = -15 score
            else
                score -= (int)((diff - 1.0) * 15); // twice bigger = -15 score


            switch (c.Fmt)
            {
                case LocalVideoSourceCapabilityFormat.Raw: 
                    break;
                case LocalVideoSourceCapabilityFormat.MJpeg:
                    score -= 2;
                    break;
                case LocalVideoSourceCapabilityFormat.I420: 
                case LocalVideoSourceCapabilityFormat.NV12: 
                    score -= 3; 
                    break;
                case LocalVideoSourceCapabilityFormat.Empty:
                    score -= 100;
                    break;
                default: 
                    score -= 200;
                    break;
            }

            return score;
        }

        private static int GetVideoBufferSize(LocalVideoSourceCapabilityFormat format, int width)
        {
            width = Math.Max(width, 1024);

            if (format == LocalVideoSourceCapabilityFormat.MJpeg)
                return width * 4000; // about 70 frames
            else
                return width * 40000; // about 27 frames
        }
    }
}
