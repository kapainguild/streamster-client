
namespace Streamster.ClientCore.Services
{
    public class LocalSettings
    {
        public string DeviceId { get; set; }

        public string UserName { get; set; }

        public bool SavePassword { get; set; }

        public string Password { get; set; }

        public bool UserRegistered { get; set; }

        public bool AutoLogon { get; set; }

        public bool EnableVideoPreview { get; set; }

        public bool DisableCameraStatusCheck { get; set; }

        public string LastSelectedVideoId { get; set; }

        public string LastSelectedAudioId { get; set; }

        public string LastRunVerion { get; set; }

        public int RemovePreviousVersion { get; set; }

        public bool NotFirstInstall { get; set; }
    }
}
