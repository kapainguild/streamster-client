using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientData.Model
{
    public interface IPlatforms
    {
        PlatformInfo[] PlatformInfos { get; set; }

        IDictionary<string, IPlatform> Platforms { get; set; } // targetId as id

        IDictionary<string, IChat> Chats { get; set; }
    }

    public interface IPlatform
    {
        public PlatformAuthenticationType AuthenticationType { get; set; }

        AuthenticationData AuthenticationData { get; set; }

        StaticAuthenticationData StaticAuthenticationData { get; set; }

        UserPasswordAuthentication UserPasswordAuthentication { get; set; }

        BrowserTokenAuthentication BrowserTokenAuthentication { get; set; }

        int ResetCounter { get; set; }
    }

    public interface IChat
    {
        string TargetId { get; set; }

        ChatState State { get; set; }

        string StateDetails { get; set; }
    }

    public class PlatformInfo
    {
        public string TargetId { get; set; }

        public PlatformInfoFlags Flags { get; set; }                                                 
    }

    public enum PlatformInfoFlags
    {
        GetKey = 1,
        Chats = 2
    }

    public enum PlatformAuthenticationType
    {
        OAuthServer = 0,
        UserPassword = 1,
        WebBrowser = 2
    }

    public enum ChatState
    {
        Initializing = 0,
        Connecting = 1,
        NotAuthenticated = 2,
        Connected= 3,
        ConnectionFailed = 4,
        ConnectionNotInitialized = 5,

    }

    public class AuthenticationData
    {
        public string OAuthAuthenticationUrl { get; set; }
    }

    public class StaticAuthenticationData
    {
        public WebBrowserLoginSearch WebBrowserLoginSearch { get; set; }
    }

    public record UserPasswordAuthentication(string User, string Password, bool CanSave);

    public record BrowserTokenAuthentication(Dictionary<string, string> KeyValues);

    public record WebBrowserLoginSearch(string StartUrl, WebBrowserLoginSearchEntry[] Entries);

    public record WebBrowserLoginSearchEntry(string parameter, string Url, string Method, string header, string regex);
}
