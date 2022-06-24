using System.Collections.Generic;

namespace Streamster.ClientData.Model
{
    public interface IRoot
    {
        int ModelVersion { get; set; }

        IDictionary<string, ITarget> Targets { get; set; }

        IDictionary<string, IChannel> Channels { get; set; }

        IDictionary<string, IVideoInput> VideoInputs { get; set; }

        IDictionary<string, IAudioInput> AudioInputs { get; set; }

        IDictionary<string, IDevice> Devices { get; set; }

        IDictionary<string, IIngest> Ingests { get; set; }

        IDictionary<string, IOutgest> Outgests { get; set; }

        IDictionary<string, IResource> Resources { get; set; }

        IDictionary<string, IScene> Scenes { get; set; }

        IDictionary<string, ITranscoder> Transcoders { get; set; }

        ISettings Settings { get; set; }

        IPlatforms Platforms { get; set; }
    }
}
