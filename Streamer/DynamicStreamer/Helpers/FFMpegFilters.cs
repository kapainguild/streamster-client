using DynamicStreamer.Nodes;
using DynamicStreamer.Queues;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DynamicStreamer
{
    class FFMpegFilters
    {

        public static  FilterPixelFormat GetFilterPixelFormat(VideoFilterChainDescriptor chain)
        {
            if (chain == null || chain.Filters == null || chain.Filters.Length == 0)
                return FilterPixelFormat.NoFilter;

            var last = chain.Filters[chain.Filters.Length - 1].Type;

            switch (last)
            {
                case VideoFilterType.Warm:
                case VideoFilterType.Cold:
                case VideoFilterType.Dark:
                case VideoFilterType.Light:
                case VideoFilterType.Vintage:
                case VideoFilterType.Sepia:
                    return FilterPixelFormat.Rgb;

                case VideoFilterType.Grayscale:
                case VideoFilterType.Contrast:
                case VideoFilterType.Brightness:
                case VideoFilterType.Saturation:
                case VideoFilterType.Gamma:
                    return FilterPixelFormat.Yuv;

                default:
                    return FilterPixelFormat.NoFilter;
            }

        }

        private static string GetFFMpegFilterString(VideoFilterDescriptor filterDesc)
        {
            switch (filterDesc.Type)
            {
                case VideoFilterType.HFlip: return "hflip";
                case VideoFilterType.Warm: return "curves=r='0/0 .50/.53 1/1':g='0/0 0.50/0.48 1/1':b='0/0 .50/.46 1/1'";
                case VideoFilterType.Cold: return "curves=r='0/0 .50/.46 1/1':g='0/0 0.50/0.51 1/1':b='0/0 .50/.54 1/1'";
                case VideoFilterType.Dark: return "curves=preset=darker";
                case VideoFilterType.Light: return "curves=preset=lighter";
                case VideoFilterType.Vintage: return "curves=preset=vintage";
                case VideoFilterType.Sepia: return "colorchannelmixer=.393:.769:.189:0:.349:.686:.168:0:.272:.534:.131";
                case VideoFilterType.Grayscale: return "eq=saturation=0";
                default:
                    return null;
            }
        }

        private static string GetEqFFMpegFilterString(VideoFilterDescriptor[] c)
        {
            var items = new[]
            {
                GetEqFFMpegFilterStringPart(c, VideoFilterType.Contrast, "contrast", 0.5, 1, 1.8),
                GetEqFFMpegFilterStringPart(c, VideoFilterType.Brightness, "brightness", -0.25, 0, 0.25),
                GetEqFFMpegFilterStringPart(c, VideoFilterType.Saturation, "saturation", 0, 1, 3),
                GetEqFFMpegFilterStringPart(c, VideoFilterType.Gamma, "gamma", 0.5, 1, 5),
            }.Where(s => s != null).ToList();
            if (items.Count > 0)
                return "eq=" + string.Join(":", items);

            return null;
        }

        private static string GetEqFFMpegFilterStringPart(VideoFilterDescriptor[] c, VideoFilterType type, string val, double min, double med, double max)
        {
            var item = c.FirstOrDefault(s => s.Type == type);
            if (item != null && item.Value != 0.0)
            {
                var dec = item.Value * 50;
                double calc;
                if (dec >= 0)
                    calc = ((dec) / 50) * (max - med) + med;
                else
                    calc = ((dec) / 50) * (med - min) + med;
                return string.Format(CultureInfo.InvariantCulture, "{0}={1:F2}", val, calc);
            }
            return null;
        }

        public static string GetFFMpegFilterString(VideoFilterChainDescriptor filterChain)
        {
            if (filterChain != null)
            { 
                var res = string.Join(",", filterChain.Filters.Select(s => GetFFMpegFilterString(s)).Concat(new[] { GetEqFFMpegFilterString(filterChain.Filters) }).Where(s => s != null));
                if (!string.IsNullOrWhiteSpace(res))
                        return res;
            }
            return "null";
        }
    }
}
