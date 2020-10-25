
namespace Streamster.ClientCore.Cross
{
    public interface IAppEnvironment
    {
        string GetStorageFolder();

        string GetProcessorName();

        void StartObtainProcessorName();

        string GetClientId();

        void SetHighPriorityToApplication();

        void OpenUrl(string url);

        void GetObsVersions(out string obs, out string obsCam);

        void CopyToClipboard(string str);
    }
}
