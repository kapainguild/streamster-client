using System;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Cross
{
    public interface ILocalAudioSourceManager
    {
        Task<LocalAudioSource[]> GetAudioSourcesAsync();
    }

    public class LocalAudioSource : LocalSource
    {
        public LocalAudioSourceCapability[] Capabilities { get; set; }
    }

    public class LocalAudioSourceCapability
    {
        public int MinimumChannels { get; set; }
        public int MaximumChannels { get; set; }
        public int MinimumSampleFrequency { get; set; }
        public int MaximumSampleFrequency { get; set; }

        public override string ToString()
        {
            return $"{MinimumChannels}-{MaximumChannels}x{MinimumSampleFrequency}-{MaximumSampleFrequency}";
        }

        public bool IsStandart() => MinimumChannels == 1 &&
                                    MaximumChannels == 2 &&
                                    MinimumSampleFrequency == 11025 &&
                                    MaximumSampleFrequency == 44100;
    }
}
