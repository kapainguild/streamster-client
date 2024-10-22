
using CefSharp.DevTools.CSS;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Streamster.ClientCore.Models.Chats
{
    public class WebBrowserAuthenticateModel : BaseAuthenticateModel
    {
        private readonly PlatformsModel _platformsModel;
        private readonly IPlatform _platform;

        private bool _resultSent = false;

        public WebBrowserAuthenticateModel(IAppEnvironment appEnvironment, PlatformsModel platformsModel, IPlatform platform)
        {
            ShowWebView2 = appEnvironment.IsWebView2Available();
            _platformsModel = platformsModel;
            _platform = platform;
            if (ShowWebView2)
            {
                UserDataPath = Path.Combine(appEnvironment.GetStorageFolder(), "AuthWebView2");
            }
            else
            {
                UserDataPath = Path.Combine(appEnvironment.GetStorageFolder(), "AuthCEF");
            }

            Login = () => 
            {
                Parameters.Clear();
                _resultSent = false;
                OpenEvent?.Invoke(this, EventArgs.Empty); 
            };
        }

        public event EventHandler OpenEvent;

        public event EventHandler CloseEvent;

        public string Title { get; set; }

        public string Description { get; set; }

        public bool ShowWebView2 { get; set; } 

        public WebBrowserLoginSearch LoginSearch { get; internal set; }

        public Dictionary<string, string> Parameters { get; } = new Dictionary<string, string>();

        public string UserDataPath { get; set; }

        public bool AddValue(string parameter, string value)
        {
            Log.Information($"Logging into '{LoginSearch.StartUrl}': Received {parameter}={value}");
            bool stop = false;
            lock (this)
            {
                Parameters[parameter] = value;
                stop = Parameters.Count >= LoginSearch.Entries.Select(s => s.parameter).Distinct().Count();
            }
            if (stop && !_resultSent)
            {
                _resultSent = true;
                CloseEvent?.Invoke(this, EventArgs.Empty);
            }
            return stop;
        }

        public void NotifyParent()
        {
            Dictionary<string, string> result;
            lock (this)
            {
                result = new Dictionary<string, string>(Parameters);
            }
            _platformsModel.WebBrowserModelAuthenticated(result, _platform);
        }
    }
}
