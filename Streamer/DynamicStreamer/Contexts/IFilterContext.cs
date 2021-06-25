using DynamicStreamer.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynamicStreamer
{
    public record FilterInputSetup(FilterInputSpec FilterSpec)
    {
        public override string ToString() => FilterSpec.ToString();
    }

    public class FilterSetup
    {
        public string Type { get; set; }

        public FilterInputSetup[] InputSetups = new FilterInputSetup[0];

        public FilterOutputSpec OutputSpec;

        public DirectXContext DirectXContext { get; set; }

        public string FilterSpec { get; set; }

        public override bool Equals(object obj)
        {
            return obj is FilterSetup setup &&
                    Type == setup.Type &&
                    DirectXContext == setup.DirectXContext &&
                    InputSetups.SequenceEqual(setup.InputSetups) &&
                    OutputSpec.Equals(setup.OutputSpec) &&
                    FilterSpec == setup.FilterSpec;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(InputSetups, OutputSpec, FilterSpec, Type, DirectXContext);
        }

        public override string ToString()
        {
            if (Type == FilterContextNull.Type)
                return "null";

            var inputs = string.Join(":", InputSetups.Select(s => s.ToString()));

            if (Type == FilterContextDirectXDownload.Type)
                return $"dx({inputs}) => {OutputSpec}";

            if (Type == FilterContextDirectXTransform.Type)
                return $"dx({inputs}) => dx(nv12)";

            if (Type == FilterContextDirectXUpload.Type)
                return $"{inputs} => dx";
            
            return $"{FilterSpec} ({inputs} => {OutputSpec})";
        }
    }

    public interface IFilterContext : IDisposable
    {
        int Open(FilterSetup setup);

        int Write(Frame frame, int inputNo);

        ErrorCodes Read(Frame frame);
    }
}

