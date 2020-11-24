using Serilog;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Documents;

namespace Streamster.ClientApp.Win.Services.Vpn
{

    static class VpnApi
    {
        public static RASDEVINFO[] GetDevices()
        {
            int cb = 0;

            CheckedInit("RasEnumDevices", VpnNative.RasEnumDevices(null, ref cb, out var devices));
            if (devices > 0)
            {
                RASDEVINFO[] buffer = new RASDEVINFO[devices];
                buffer[0].dwSize = Marshal.SizeOf<RASDEVINFO>();
                Checked("RasEnumDevices", VpnNative.RasEnumDevices(buffer, ref cb, out devices));
                return buffer;
            }
            return new RASDEVINFO[0];
        }

        public static RASCONN[] GetConnections()
        {
            int cb = 0;
            CheckedInit("RasEnumConnections", VpnNative.RasEnumConnections(null, ref cb, out var connectionCount));
            if (connectionCount > 0)
            {
                RASCONN[] buffer = new RASCONN[connectionCount];
                buffer[0].dwSize = Marshal.SizeOf<RASCONN>();
                Checked("RasEnumConnections", VpnNative.RasEnumConnections(buffer, ref cb, out connectionCount));
                return buffer;
            }
            return new RASCONN[0];
        }

        public static RASENTRYNAME[] GetEntryNames()
        {
            int cb = 0;

            CheckedInit("RasEnumEntries", VpnNative.RasEnumEntries(IntPtr.Zero, IntPtr.Zero, null, ref cb, out var entries));
            if (entries > 0)
            {
                RASENTRYNAME[] buffer = new RASENTRYNAME[entries];
                buffer[0].dwSize = Marshal.SizeOf<RASENTRYNAME>();
                Checked("RasEnumEntries", VpnNative.RasEnumEntries(IntPtr.Zero, IntPtr.Zero, buffer, ref cb, out entries));
                return buffer;
            }
            return new RASENTRYNAME[0];
        }

        public static WaitHandle RegisterForTermination(IntPtr handle)
        {
            var ev = new AutoResetEvent(false);
            try
            {
                Checked("RasEnumEntries", VpnNative.RasConnectionNotification(handle, ev.SafeWaitHandle, RASCN.Disconnection));
            }
            catch
            {
                ev.Dispose();
                throw;
            }
            return ev;
        }

        public static RASCONNSTATUS GetState(IntPtr hConn)
        {
            RASCONNSTATUS status = new RASCONNSTATUS();
            status.size = Marshal.SizeOf<RASCONNSTATUS>();

            Checked("RasGetConnectStatus", VpnNative.RasGetConnectStatus(hConn, ref status));
            return status;
        }

        public static RAS_STATS GetStatisitcs(IntPtr hConn)
        {
            RAS_STATS status = new RAS_STATS();
            status.dwSize = (uint)Marshal.SizeOf<RAS_STATS>();

            Checked("RasGetConnectionStatistics", VpnNative.RasGetConnectionStatistics(hConn, ref status));
            return status;
        }

        public static RAS_PROJECTION_INFO GetProjectionInfoEx(IntPtr hConn)
        {
            RAS_PROJECTION_INFO info = new RAS_PROJECTION_INFO();
            info.version = 4;
            int dwSize = Marshal.SizeOf<RAS_PROJECTION_INFO>();
            Checked("GetProjectionInfoEx", VpnNative.RasGetProjectionInfoEx(hConn, ref info, ref dwSize));
            return info;
        }


        internal static bool IsHangUp(IntPtr hConn)
        {
            RASCONNSTATUS status = new RASCONNSTATUS();
            status.size = Marshal.SizeOf<RASCONNSTATUS>();

            return VpnNative.RasGetConnectStatus(hConn, ref status) != 0; // as described in Hangup description
        }

        public static void HangUp(IntPtr hConn) => Logged("RasHangUp", VpnNative.RasHangUp(hConn));

        private static List<object> _pinnedCallbacks = new List<object>();

        internal static IntPtr Dial(string entryName, string user, string pwd, Action<uint, RasConnectionState, uint, uint> action)
        {
            var radDialParams = new RASDIALPARAMS
            {
                size = Marshal.SizeOf<RASDIALPARAMS>(),
                domain = "",
                userName = user,
                password = pwd,
                entryName = entryName,
            };

            var rasDialExtensions = new RASDIALEXTENSIONS
            {
                size = Marshal.SizeOf<RASDIALEXTENSIONS>(),
                devSpecificInfo = new RASDEVSPECIFICINFO
                {
                    size = Marshal.SizeOf<RASDEVSPECIFICINFO>(),
                }
            };
            var callback = new VpnNative.RasDialFunc2((a, b, c, messageId, connectionState, error, extendedError) =>
            {
                action(messageId, connectionState, error, extendedError);
                return true;
            });

            PinCallback(callback);

            Checked("RasDial", VpnNative.RasDial(ref rasDialExtensions, null, ref radDialParams, 2, callback, out var handle));

            return handle;
        }

        private static void PinCallback(VpnNative.RasDialFunc2 callback)
        {
            // we pin callbacks so they are not garbage collected
            _pinnedCallbacks.Add(callback);

            // remove old to avoid memory leaks
            if (_pinnedCallbacks.Count > 10)
                _pinnedCallbacks.RemoveAt(0);
        }

        public static void DeleteEntry(string name) => Logged("RasDeleteEntry", VpnNative.RasDeleteEntry(null, name));

        public static void CreateEntry(string entryName, string url, RASDEVINFO device)
        {
            var props = new RASENTRY
            {
                dwSize = Marshal.SizeOf<RASENTRY>(),
                szAutodialDll = "",
                szAutodialFunc = "",
                szAreaCode = "",
                szCustomDialDll = "",
                szDeviceType = device.szDeviceType,
                szDeviceName = device.szDeviceName,
                dwType = (int)(RasEntryTypes.Vpn), //vpn
                dwEncryptionType = 1, // require
                dwFramingProtocol = (int)RasFramingProtocol.Ppp,
                dwfNetProtocols = (int)(RasNetProtocols.Ip | RasNetProtocols.Ipv6), //IP and IPv6
                dwfOptions = (int)(RasEntryOptions.RemoteDefaultGateway | RasEntryOptions.ModemLights | RasEntryOptions.RequireEncrptedPw | RasEntryOptions.PreviewUserPw | RasEntryOptions.PreviewDomain | RasEntryOptions.ShowDialingProgress),
                szLocalPhoneNumber = url,
                szScript = "",
                dwVpnStrategy = 5, //SstpOnly
                szX25Address = "",
                szX25Facilities = "",
                szX25PadType = "",
                szX25UserData = "",
                dwfOptions2 = (int)(RasEntryOptions2.DoNotNegotiateMultilink | RasEntryOptions2.ReconnectIfDropped | RasEntryOptions2.UseTypicalSettings | RasEntryOptions2.IPv6RemoteDefaultGateway),
                szDnsSuffix = "",
                szPrerequisitePbk = "",
                szPrerequisiteEntry = "",
                dwRedialCount = 3000,
                dwRedialPause = 3,
            };
            Checked("RasSetEntryProperties", VpnNative.RasSetEntryProperties(null, entryName, ref props, Marshal.SizeOf<RASENTRY>(), IntPtr.Zero, 0));
        }

        internal static string RasErrorMessage(uint errorCode)
        {
            StringBuilder sb = new StringBuilder(1024);
            if (VpnNative.RasGetErrorString(errorCode, sb, sb.Capacity) > 0)
                return $"Error {errorCode}";
            return $"{errorCode}: {sb}";
        }

        private static void Logged(string name, uint errorcode)
        {
            if (errorcode != 0)
                Log.Warning($"VPN {name} returned {RasErrorMessage(errorcode)}");
        }

        private static void Checked(string name, uint errorcode)
        {
            if (errorcode != 0)
                throw new InvalidOperationException($"{name} failed with {RasErrorMessage(errorcode)}");
        }

        private static void CheckedInit(string name, uint errorcode)
        {
            if (errorcode != 603 && errorcode != 0)
                throw new InvalidOperationException($"Init {name} failed with {RasErrorMessage(errorcode)}");
        }
    }

    static class VpnNative
    {
        public delegate bool RasDialFunc2(IntPtr callbackId, int subEntryId, IntPtr handle, uint message, RasConnectionState state, uint errorCode, uint extendedErrorCode);

        [DllImport("rasapi32.dll", CharSet = CharSet.Auto)]
        public static extern uint RasDeleteEntry(string lpszPhonebook, string lpszEntryName);

        [DllImport("rasapi32.dll", CharSet = CharSet.Auto)]
        public static extern uint RasSetEntryProperties(string lpszPhonebook, string lpszEntry, ref RASENTRY lpRasEntry, int dwEntryInfoSize, IntPtr lpbDeviceInfo, int dwDeviceInfoSize);

        [DllImport("rasapi32.dll", CharSet = CharSet.Auto)]
        public static extern uint RasGetConnectionStatistics(IntPtr hRasConn, ref RAS_STATS lpStatistics);

        [DllImport("rasapi32.dll", CharSet = CharSet.Auto)]
        public static extern uint RasGetConnectStatus(IntPtr hrasconn, [In, Out] ref RASCONNSTATUS lprasconnstatus);

        [DllImport("rasapi32.dll", CharSet = CharSet.Auto)]
        public static extern uint RasEnumConnections([In, Out] RASCONN[] rasconn, [In, Out] ref int cb, [Out] out int connections);

        [DllImport("rasapi32.dll", CharSet = CharSet.Auto)]
        public static extern uint RasEnumDevices([In, Out] RASDEVINFO[] lpRasDevInfo, [In, Out] ref int lpcb, [Out] out int lpcDevices);

        [DllImport("rasapi32.dll", CharSet = CharSet.Auto)]
        public static extern uint RasEnumEntries(IntPtr reserved, IntPtr lpszPhonebook, [In, Out] RASENTRYNAME[] lprasentryname, [In, Out] ref int lpcb, [Out] out int lpcEntries);

        [DllImport("rasapi32.dll")]
        public static extern uint RasHangUp(IntPtr hRasConn);

        [DllImport("rasapi32.dll", CharSet = CharSet.Auto)]
        public extern static uint RasGetErrorString(uint uErrorValue, StringBuilder lpszErrorString, [In] int cBufSize);

        [DllImport("rasapi32.dll", CharSet = CharSet.Auto)]
        public static extern uint RasDial(ref RASDIALEXTENSIONS lpRasDialExtensions, string lpszPhonebook, ref RASDIALPARAMS lpRasDialParams, int dwNotifierType, Delegate lpvNotifier, out IntPtr lphRasConn);

        [DllImport("rasapi32.dll", CharSet = CharSet.Auto)]
        public static extern uint RasConnectionNotification(IntPtr hRasConn, SafeHandle hEvent, RASCN dwFlags);

        [DllImport("rasapi32.dll", CharSet = CharSet.Auto)]
        public static extern uint RasGetProjectionInfoEx(IntPtr hRasConn, ref RAS_PROJECTION_INFO info, [In, Out] ref int bufferSize);
    }

    public enum RASCN
    {
        Connection = 0x1,
        Disconnection = 0x2,
        BandwidthAdded = 0x4,
        BandwidthRemoved = 0x8,
        Dormant = 0x10,
        Reconnection = 0x20
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct RASDEVINFO
    {
        public int dwSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16 + 1)]
        public string szDeviceType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128 + 1)]
        public string szDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct RASENTRYNAME
    {
        public int dwSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256 + 1)]
        public string szEntryName;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260 + 1)]
        public string szPhonebook;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
    struct RASENTRY
    {
        public int dwSize;
        public int dwfOptions;
        public int dwCountryID;
        public int dwCountryCode;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10 + 1)]
        public string szAreaCode;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128 + 1)]
        public string szLocalPhoneNumber;
        public int dwAlternateOffset;
        public RASIPADDR ipaddr;
        public RASIPADDR ipaddrDns;
        public RASIPADDR ipaddrDnsAlt;
        public RASIPADDR ipaddrWins;
        public RASIPADDR ipaddrWinsAlt;
        public int dwFrameSize;
        public int dwfNetProtocols;
        public int dwFramingProtocol;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szScript;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szAutodialDll;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szAutodialFunc;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16 + 1)]
        public string szDeviceType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128 + 1)]
        public string szDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32 + 1)]
        public string szX25PadType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 200 + 1)]
        public string szX25Address;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 200 + 1)]
        public string szX25Facilities;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 200 + 1)]
        public string szX25UserData;
        public int dwChannels;
        public int dwReserved1;
        public int dwReserved2;
        public int dwSubEntries;
        public int dwDialMode;
        public int dwDialExtraPercent;
        public int dwDialExtraSampleSeconds;
        public int dwHangUpExtraPercent;
        public int dwHangUpExtraSampleSeconds;
        public int dwIdleDisconnectSeconds;
        public int dwType;
        public int dwEncryptionType;
        public int dwCustomAuthKey;
        public Guid guidId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szCustomDialDll;
        public int dwVpnStrategy;
        public int dwfOptions2;
        public int dwfOptions3;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szDnsSuffix;
        public int dwTcpWindowSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szPrerequisitePbk;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256 + 1)]
        public string szPrerequisiteEntry;
        public int dwRedialCount;
        public int dwRedialPause;
        RASIPV6ADDR ipv6addrDns;
        RASIPV6ADDR ipv6addrDnsAlt;
        public int dwIPv4InterfaceMetric;
        public int dwIPv6InterfaceMetric;
        RASIPV6ADDR ipv6addr;
        public int dwIPv6PrefixLength;
        public int dwNetworkOutageTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RASIPADDR
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] addr;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RASIPV6ADDR
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] addr;
    }

    enum RasEntryOptions
    {
        UseCountrAndAreaCodes = 0x00000001,
        SpecificIpAddr = 0x00000002,
        SpecificNameServers = 0x00000004,
        IpHeaderCompression = 0x00000008,
        RemoteDefaultGateway = 0x00000010,
        DisableLcpExtensions = 0x00000020,
        TerminalBeforeDial = 0x00000040,
        TerminalAfterDial = 0x00000080,
        ModemLights = 0x00000100,
        SwCompression = 0x00000200,
        RequireEncrptedPw = 0x00000400,
        RequireMsEncrptedPw = 0x00000800,
        RequireDataEncrption = 0x00001000,
        NetworkLogon = 0x00002000,
        UseLogonCredentials = 0x00004000,
        PromoteAlternates = 0x00008000,
        SecureLocalFiles = 0x00010000,
        RequireEAP = 0x00020000,
        RequirePAP = 0x00040000,
        RequireSPAP = 0x00080000,
        Custom = 0x00100000,
        PreviewPhoneNumber = 0x00200000,
        SharedPhoneNumbers = 0x00800000,
        PreviewUserPw = 0x01000000,
        PreviewDomain = 0x02000000,
        ShowDialingProgress = 0x04000000,
        RequireCHAP = 0x08000000,
        RequireMsCHAP = 0x10000000,
        RequireMsCHAP2 = 0x20000000,
        RequireW95MSCHAP = 0x40000000
    }

    enum RasEntryOptions2
    {
        None = 0x0,
        SecureFileAndPrint = 0x1,
        SecureClientForMSNet = 0x2,
        DoNotNegotiateMultilink = 0x4,
        DoNotUseRasCredentials = 0x8,
        UsePreSharedKey = 0x10,
        Internet = 0x20,
        DisableNbtOverIP = 0x40,
        UseGlobalDeviceSettings = 0x80,
        ReconnectIfDropped = 0x100,
        SharePhoneNumbers = 0x200,
        SecureRoutingCompartment = 0x400,
        UseTypicalSettings = 0x800,
        IPv6SpecificNameServer = 0x1000,
        IPv6RemoteDefaultGateway = 0x2000,
        RegisterIPWithDns = 0x4000,
        UseDnsSuffixForRegistration = 0x8000,
        IPv4ExplicitMetric = 0x10000,
        IPv6ExplicitMetric = 0x20000,
        DisableIkeNameEkuCheck = 0x40000,
        DisableClassBasedStaticRoute = 0x80000,
        IPv6SpecificAddress = 0x100000,
        DisableMobility = 0x200000,
        RequireMachineCertificates = 0x400000
    }

    enum RasEntryTypes
    {
        Phone = 1,
        Vpn = 2,
        Direct = 3,
        Internet = 4
    }

    enum RasEntryEncryption
    {
        None = 0,
        Require = 1,
        RequireMax = 2,
        Optional = 3
    }

    enum RasNetProtocols
    {
        NetBEUI = 0x00000001,
        Ipx = 0x00000002,
        Ip = 0x00000004,
        Ipv6 = 0x00000008
    }

    enum RasFramingProtocol
    {
        Ppp = 0x00000001,
        Slip = 0x00000002,
        Ras = 0x00000004
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct RASDIALEXTENSIONS
    {
        public int size;
        public int options;
        public IntPtr handle;
        public IntPtr reserved;
        public IntPtr reserved1;
        public RASEAPINFO eapInfo;
        public bool skipPppAuth;
        public RASDEVSPECIFICINFO devSpecificInfo;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct RASEAPINFO
    {
        public int sizeOfEapData;
        public IntPtr eapData;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    struct RASDEVSPECIFICINFO
    {
        public int size;
        public IntPtr cookie;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
    struct RASDIALPARAMS
    {
        public int size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256 + 1)]
        public string entryName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128 + 1)]
        public string phoneNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128 + 1)]
        public string callbackNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256 + 1)]
        public string userName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256 + 1)]
        public string password;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 15 + 1)]
        public string domain;
        public int subEntryId;
        public IntPtr callbackId;
        public int interfaceIndex;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct RAS_STATS
    {
        public uint dwSize;
        public uint dwBytesXmited;
        public uint dwBytesRcved;
        public uint dwFramesXmited;
        public uint dwFramesRcved;
        public uint dwCrcErr;
        public uint dwTimeoutErr;
        public uint dwAlignmentErr;
        public uint dwHardwareOverrunErr;
        public uint dwFramingErr;
        public uint dwBufferOverrunErr;
        public uint dwCompressionRatioIn;
        public uint dwCompressionRatioOut;
        public uint dwBps;
        public uint dwConnectionDuration;
    }

    enum RasConnectionState
    {
        OpenPort = 0,
        PortOpened,
        ConnectDevice,
        DeviceConnected,
        AllDevicesConnected,
        Authenticate,
        AuthNotify,
        AuthRetry,
        AuthCallback,
        AuthChangePassword,
        AuthProject,
        AuthLinkSpeed,
        AuthAck,
        PostCallbackAuthentication,
        Authenticated,
        PrepareForCallback,
        WaitForModemReset,
        WaitForCallback,
        Projected,
        StartAuthentication,
        CallbackComplete,
        LogOnNetwork,
        SubEntryConnected,
        SubEntryDisconnected,
        ApplySettings,
        Interactive = 0x1000,
        RetryAuthentication,
        CallbackSetByCaller,
        PasswordExpired,
        InvokeEapUI,
        Connected = 0x2000,
        Disconnected,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    struct RASCONNSTATUS
    {
        public int size;
        public RasConnectionState connectionState;
        public uint errorCode;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16 + 1)]
        public string deviceType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128 + 1)]
        public string deviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128 + 1)]
        public string phoneNumber;
        public RASTUNNELENDPOINT localEndPoint;
        public RASTUNNELENDPOINT remoteEndPoint;
        public RasConnectionSubState connectionSubState;
    }

    enum RasConnectionSubState
    {
        None = 0,
        Dormant,
        Reconnecting,
        Reconnected = 0x2000
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RASTUNNELENDPOINT
    {
        public int type;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] addr;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Auto)]
    struct RASCONN
    {
        public int dwSize;
        public IntPtr hrasconn;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szEntryName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string szDeviceType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szPhonebook;
        public int dwSubEntry;
        public Guid guidEntry;
        public int dwFlags;
        public Guid luid;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RAS_PROJECTION_INFO
    {
        public uint version;
        public uint type;

        public uint ipv4NegotiationError;
        public RASIPADDR ipv4Address;
        public RASIPADDR ipv4ServerAddress;
        public uint ipv4Options;
        public uint ipv4ServerOptions;
        public uint ipv6NegotiationError;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] interfaceIdentifier;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] serverInterfaceIdentifier;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bundled;
        [MarshalAs(UnmanagedType.Bool)]
        public bool multilink;
        public uint authenticationProtocol;
        public uint authenticationData;
        public uint serverAuthenticationProtocol;
        public uint serverAuthenticationData;
        public uint eapTypeId;
        public uint serverEapTypeId;
        public uint lcpOptions;
        public uint serverLcpOptions;
        public uint ccpCompressionAlgorithm;
        public uint serverCcpCompressionAlgorithm;
        public uint ccpOptions;
        public uint serverCcpOptions;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
        public byte[] extra;
    }
}
