using DynamicStreamer.Nodes;
using System.Collections.Generic;

namespace DynamicStreamer
{
    public class StreamerRuntimeConfig
    {
        public int Version { get; set; }

        public List<(IRuntimeItem item, object setup)> Nodes { get; } = new List<(IRuntimeItem item, object setup)>();

        public DirectXContext Dx { get; set; }

        public bool EnableObjectTracking { get; set; }

        public void Add(IRuntimeItem node, object setup) => Nodes.Add((node, setup));
    }
}
