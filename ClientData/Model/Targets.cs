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

        TargetPromotion Promotion { get; set; }

        TargetLimits Limits { get; set; }
    }

    public class TargetPromotion
    {
        public bool Recommended { get; set; }

        public TargetTag[] Tags { get; set; }
    }

    public class TargetTag
    {
        public string Name { get; set; }

        public string Description { get; set; }
    }

    public class TargetLimits
    {
        public BitrateRange[] Max { get; set; }

        public BitrateRange[] Bitrates { get; set; }
    }

    public record BitrateRange(int ResY, int Fps, int Min, int Max); //change carefully

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
