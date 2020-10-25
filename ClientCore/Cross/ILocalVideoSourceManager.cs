using Streamster.ClientData.Model;
using System;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Cross
{
    public interface ILocalVideoSourceManager
    {
        void Start(Action<IVideoSource> videoDeviceChanged, Action<IVideoSource, VideoInputPreview> previewAvailable);

        void StartObservation();

        void StopObservation();

        Task<IVideoSource[]> RetrieveSourcesListAsync();

        Task<IVideoSource> GetUpdatedVideoSourceAsync(string videoDeviceId);

        void SetRunningSource(string videoDeviceId);
    }

    public interface IBaseSource
    {
        string Id { get; }

        string Name { get; }

        InputState State { get; }

        InputType Type { get; }
    }

    public interface IVideoSource : IBaseSource
    {
        VideoInputCapability[] Capabilities { get; }
    }
}
