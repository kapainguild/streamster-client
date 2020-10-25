using System.Collections;
using System.Collections.Generic;

namespace Streamster.ClientData.Model
{
    public class VideoFilters
    {
        public bool FlipH { get; set; }

        public VideoFilter[] Items { get; set; }

        public override bool Equals(object obj)
        {
            return obj is VideoFilters filters &&
                   filters.FlipH == FlipH &&
                   StructuralComparisons.StructuralEqualityComparer.Equals(Items, filters.Items);
        }

        public override int GetHashCode()
        {
            int hashCode = 1855599113;
            hashCode = hashCode * -1521134295 + FlipH.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<VideoFilter[]>.Default.GetHashCode(Items);
            return hashCode;
        }
    }

    public class VideoFilter
    {
        public string Name { get; set; }

        public double Value { get; set; }

        public override bool Equals(object obj)
        {
            return obj is VideoFilter filter &&
                   Name == filter.Name &&
                   Value == filter.Value;
        }

        public override int GetHashCode()
        {
            int hashCode = -244751520;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            hashCode = hashCode * -1521134295 + Value.GetHashCode();
            return hashCode;
        }
    }
}
