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
        AuthenticationData AuthenticationData { get; set; }

        UserPasswordAuthentication UserPasswordAuthentication { get; set; }
    }

    public interface IChat
    {
        string TargetId { get; set; }

        ChatState State { get; set; }
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
        OAuthServer,
        UserPassword,
    }

    public enum ChatState
    {
        Initializing,
        Connecting,
        NotAuthenticated,
        Connected,
        ConnectionFailed
    }

    public class AuthenticationData
    {
        public PlatformAuthenticationType AuthenticationType { get; set; }

        public string OAuthAuthenticationUrl { get; set; }
    }


    public class UserPasswordAuthentication
    {
        public string User { get; set; }

        public string Password{ get; set; }

        public bool CanSave { get; set; }
    }

    
}
