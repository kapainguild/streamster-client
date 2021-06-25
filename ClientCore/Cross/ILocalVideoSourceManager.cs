using Streamster.ClientData.Model;
using System;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Cross
{
    public interface ILocalVideoSourceManager
    {
        Task<LocalVideoSource[]> GetVideoSourcesAsync();
    }

    public class LocalSource
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public InputDeviceState State { get; set; }

        public InputDeviceType Type { get; set; }
    }

    public class LocalVideoSource : LocalSource
    {
        public LocalVideoSourceCapability[] Capabilities { get; set; }
    }

    public class LocalVideoSourceCapability
    {
        public int W { get; set; }
        public int H { get; set; }
        public double MinF { get; set; }
        public double MaxF { get; set; }
        public LocalVideoSourceCapabilityFormat Fmt { get; set; }

        public override bool Equals(object obj)
        {
            return obj is LocalVideoSourceCapability capability &&
                   W == capability.W &&
                   H == capability.H &&
                   MinF == capability.MinF &&
                   MaxF == capability.MaxF &&
                   Fmt == capability.Fmt;
        }

        public override int GetHashCode() => W + H + (int)Fmt;

        public override string ToString() => $"{GetFormatStr(Fmt)}.{W}x{H}x{MinF}-{MaxF}";

        private string GetFormatStr(LocalVideoSourceCapabilityFormat fmt)
        {
            switch (fmt)
            {
                case LocalVideoSourceCapabilityFormat.Raw: return "R";
                case LocalVideoSourceCapabilityFormat.Empty: return "E";
                case LocalVideoSourceCapabilityFormat.MJpeg:  return "J";
                case LocalVideoSourceCapabilityFormat.Unknown: return "?";
                case LocalVideoSourceCapabilityFormat.H264: return "H264";
                case LocalVideoSourceCapabilityFormat.I420: return "I420";
                case LocalVideoSourceCapabilityFormat.NV12: return "NV12";
            }
            return "??";
        }
    }

    public enum LocalVideoSourceCapabilityFormat
    {
        Raw = 0,
        Empty = 1,
        MJpeg = 2,
        Unknown = 3,
        H264 = 4,
        I420 = 5,
        NV12 = 6
    }
}
