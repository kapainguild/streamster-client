using System;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Cross
{
    public interface ILocalAudioSourceManager
    {
        void Start(Action<IAudioSource> audioDeviceChanged);

        Task<IAudioSource[]> RetrieveSourcesListAsync();

        void SetRunningSource(string localAudioId);
    }

    public interface IAudioSource : IBaseSource
    {
    }
}
