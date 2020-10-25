using System;

namespace Streamster.ClientData.Model
{
    public interface ITarget
    {
        string Id { get; set; }

        string Name { get; set; }

        string WebUrl { get; set; }

        string Hint { get; set; }

        TargetFlags Flags { get; set; }

        string DefaultRtmpUrl { get; set; }
    }


    [Flags]
    public enum TargetFlags
    {
        Key = 1,
        Url = 2,

        Adult = 4,
        Vlog = 8,
        Gaming = 16,
        Education = 64,
        Religion = 128,
        Music = 256
    }
}
