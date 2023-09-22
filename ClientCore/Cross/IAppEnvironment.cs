
namespace Streamster.ClientCore.Cross
{
    public interface IAppEnvironment
    {
        string GetStorageFolder();

        string GetProcessorName();

        void StartObtainProcessorName();

        string GetClientId();

        string GetDeviceName();

        void SetHighPriorityToApplication();

        void OpenUrl(string url);

        void GetObsVersions(out string obs, out string obsCam);

        string GetObsStreamUrl(out string service);

        void CopyToClipboard(string str);

        void PreventSleepMode(bool bEnable);
    }
}
