using Newtonsoft.Json;
using Serilog;
using Streamster.ClientCore.Cross;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Services
{
    public class LocalSettingsService
    {
        private const string FileName = "settings.data";
        private string _file;

        public LocalSettingsService(IAppEnvironment environment)
        {
            _file = Path.Combine(environment.GetStorageFolder(), FileName);

            Safe(() =>
            {
                if (File.Exists(_file))
                {
                    var encrypted = File.ReadAllBytes(_file);
                    var bytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                    var str = Encoding.UTF8.GetString(bytes);
                    Settings = JsonConvert.DeserializeObject<LocalSettings>(str);
                }
            });

            Settings = Settings ?? new LocalSettings();
        }

        public async Task ChangeSettings(Func<LocalSettings, bool> changer)
        {
            await Task.Run(() =>
            {
                lock (this)
                {
                    if (changer(Settings))
                    {
                        Save();
                    }
                }
            });
        }

        public Task ChangeSettingsUnconditionally(Action<LocalSettings> changer)
        {
            return ChangeSettings(s =>
            {
                changer(s);
                return true;
            });
        }

        private void Save()
        {
            Safe(() =>
            {
                var str = JsonConvert.SerializeObject(Settings);
                var bytes = Encoding.UTF8.GetBytes(str);
                var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_file, encrypted);
            });
        }

        public static void Safe(Action action, [CallerMemberName] string methodName = null)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Log.Warning(e, "Execute.Safe from {ExceptionSource}", methodName);
            }
        }

        public LocalSettings Settings { get; private set; }
    }
}
