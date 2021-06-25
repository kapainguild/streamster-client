using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientData.Model
{
    public class CaptureSource
    {
        public long CaptureId { get; set; }

        public string Name { get; set; }

        public int W { get; set; }

        public int H { get; set; }

        public override bool Equals(object obj)
        {
            return obj is CaptureSource source &&
                   CaptureId == source.CaptureId &&
                   Name == source.Name &&
                   W == source.W &&
                   H == source.H;
        }

        public override int GetHashCode()
        {
            int hashCode = -1919740922;
            hashCode = hashCode * -1521134295 + CaptureId.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            return hashCode;
        }

        public override string ToString() => $"{Name} [{W}x{H}])";
    }
}
