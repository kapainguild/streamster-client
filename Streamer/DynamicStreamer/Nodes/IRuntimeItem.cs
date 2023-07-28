using DynamicStreamer.Queues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicStreamer.Nodes
{
    public interface IRuntimeItem
    {
        NodeName Name { get; }
    }

    public interface IStatProvider : IRuntimeItem
    {
        StatisticItem[] GetStat();
    }

    public interface ISourceQueueHolder : IRuntimeItem
    {
        ISourceQueue InputQueueForOverload { get; }
    }
}
