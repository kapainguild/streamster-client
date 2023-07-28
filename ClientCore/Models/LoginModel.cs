using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class LoginModel
    {
        private readonly LocalSettingsService _settingsService;
        private readonly ConnectionService _connectionService;
        private readonly NotificationService _notificationService;
        private readonly MainModel _main;
        private readonly UpdateModel _updateModel;
        private readonly IAppEnvironment _environment;
        private readonly IdService _idService;

        public bool WithoutRegistrationEnabled { get; } = true;

        public RootModel Root { get; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public bool UserRegistered { get; }

        public Action DoLogin { get; }

        public Action DoAnonymousLogin { get; }

        public Property<bool> ControlsEnabled { get; } = new Property<bool>(true);

        public Property<bool> ControlsVisible { get; } = new Property<bool>(true);

        public bool SavePassword { get; set; }

        public bool Loaded { get; }

        public Property<bool> Connected { get; } = new Property<bool>();

        public string Version { get; }

        public NotificationModel Notifications => _notificationService.Model;

        public LoginModel(RootModel root, MainModel main, UpdateModel updateModel, 
            LocalSettingsService settingsService, 
            ConnectionService connectionService, 
            NotificationService notificationService, 
            IAppEnvironment environment,
            IdService idService)
        {
            Root = root;

            _settingsService = settingsService;
            _connectionService = connectionService;
            _notificationService = notificationService;
            _main = main;
            _updateModel = updateModel;
            _environment = environment;
            _idService = idService;
            var s = settingsService.Settings;

            SavePassword = s.SavePassword;
            UserName = s.UserName;
            Password = s.Password;
            UserRegistered = s.UserRegistered;

            Version = ClientVersionHelper.GetVersion();

            DoLogin = async () => await LoginAsync(true);
            DoAnonymousLogin = async () => await LoginAsync(false);

            if (s.AutoLogon && s.SavePassword && s.UserRegistered)
                _ = DoAutoLogin();
        }

        private async Task DoAutoLogin()
        {
            ControlsVisible.Value = false;

            await Task.Delay(500);

            await LoginAsync(true);
        }

        private async Task LoginAsync(bool asRegistered)
        {
            ControlsEnabled.Value = false;
            bool succeed = false;
            try
            {
                _environment.SetHighPriorityToApplication();
                _environment.StartObtainProcessorName();

                UserName = UserName?.Trim();

                await StoreLoginData(asRegistered);

                if (asRegistered && (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(Password)))
                    throw new WrongUserNamePasswordException();

                NetworkCredential credentials = asRegistered ? new NetworkCredential(UserName, Password) : null;

                _notificationService.SetProgress("Initializing...");

                _main.BeforeConnect();
                await _idService.WaitDeviceId();
                var response = await _connectionService.StartAsync(credentials);

                await _main.StartAsync();

                Connected.Value = true;

                await _main.DisplayAsync(Task.Delay(800), response.UpperVersions, _connectionService.Claims.AppUpdatePath);
                _notificationService.Clear(this);
                succeed = true;
            }
            catch (WrongUserNamePasswordException)
            {
                _notificationService.SetError(this, "Wrong user name/password or your account is not active");
            }
            catch (ConnectionServiceException e)
            {
                _notificationService.SetError(this, e.Message, e);
            }
            catch(HubConnectionException e)
            {
                _notificationService.SetError(this, $"Something went wrong with app. Please contact service administrator ({e.Message})", e);
            }
            catch (Exception e)
            {
                _notificationService.SetError(this, $"Something went wrong with the app. Please contact service administrator ({e.Message})", e);
            }

            if (!succeed)
            {
                Connected.Value = false;
                ControlsEnabled.Value = true;
                ControlsVisible.Value = true;
            }
        }

        private Task StoreLoginData(bool asRegistered) => _settingsService.ChangeSettings(s =>
        {
            var password = SavePassword ? Password : null;
            bool changed = s.SavePassword != SavePassword 
                            || s.UserName != UserName 
                            || s.Password != password 
                            || s.UserRegistered != asRegistered;

            if (changed)
            {
                s.SavePassword = SavePassword;
                s.UserName = UserName;
                s.Password = password;
                s.UserRegistered = asRegistered;
            }
            return changed;
        });
        
    }
}
