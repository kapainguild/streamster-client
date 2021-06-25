using System;
using System.Runtime.InteropServices;

namespace DynamicStreamer.Extensions.DesktopAudio
{
    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioCaptureClient
    {
        int GetBuffer(out IntPtr data, out int numFramesRead, out AudioClientBufferFlags flags, out long devicePosition, out long qpcPosition);
        int ReleaseBuffer(int numFramesRead);
        int GetNextPacketSize(out int captureSize);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioClient
    {
        [PreserveSig]
        int Initialize(AudioClientShareModeEnum shareMode,
            AudioClientStreamFlagsEnum streamFlags,
            long hnsBufferDuration,
            long hnsPeriodicity,
            IntPtr pFormat,
            [MarshalAs(UnmanagedType.LPStruct)] Guid audioSessionGuid);

        int GetBufferSize(out int bufferSize);

        [return: MarshalAs(UnmanagedType.I8)]
        long GetStreamLatency();

        int GetCurrentPadding(out int currentPadding);

        [PreserveSig]
        int IsFormatSupported(
            AudioClientShareModeEnum shareMode,
            IntPtr pFormat,
            IntPtr closestMatchPtr);

        int GetMixFormat(out IntPtr deviceFormatPointer);

        int GetDevicePeriod(out long defaultDevicePeriod, out long minimumDevicePeriod);

        int Start();

        int Stop();

        int Reset();

        int SetEventHandle(IntPtr eventHandle);

        [PreserveSig]
        int GetService([MarshalAs(UnmanagedType.LPStruct)] Guid interfaceId,
           [Out, MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    }

    [ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioRenderClient
    {
        int GetBuffer(int numFramesRequested, out IntPtr ptr);
        int ReleaseBuffer(int numFramesWritten, AudioClientBufferFlags flags);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        int Activate(ref Guid id, ClsCtxEnum clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);

        int OpenPropertyStore(StorageAccessModeEnum stgmAccess, [MarshalAs(UnmanagedType.IUnknown)] out object properties);

        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        int GetState(out DeviceStateEnum state);
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(DataFlowEnum dataFlow, DeviceStateEnum stateMask, [MarshalAs(UnmanagedType.IUnknown)] out object devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(DataFlowEnum dataFlow, RoleEnum role, out IMMDevice endpoint);

        int GetDevice(string id, out IMMDevice deviceName);

        int RegisterEndpointNotificationCallback([MarshalAs(UnmanagedType.IUnknown)] object client);

        int UnregisterEndpointNotificationCallback([MarshalAs(UnmanagedType.IUnknown)] object client);
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumerator
    {
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 2)]
    public struct WaveFormatEx
    {
        public ushort WaveFormat;
        public short Channels;
        public int SampleRate;
        public int AverageBytesPerSecond;
        public short BlockAlign;
        public short BitsPerSample;
        public short ExtraSize;
        public short SamplesUnion;
        public int ChannelMask;
        public Guid SubFormat;
    }

    public enum AudioClientBufferFlags
    {
        None = 0x0,
        DataDiscontinuity = 0x1,
        Silent = 0x2,
        TimestampError = 0x3
    };

    public enum AudioClientShareModeEnum
    {
        Shared,
        Exclusive
    };

    public enum AudioClientStreamFlagsEnum
    {
        None = 0x0,
        StreamFlagsLoopback = 0x00020000,
        StreamFlagsEventCallback = 0x00040000
    };

    public enum ClsCtxEnum
    {
        All = 0x1 | 0x2 | 0x4 | 0x10
    };

    public enum DataFlowEnum
    {
        Render,
        Capture,
        All
    };

    public enum DeviceStateEnum
    {
        Active = 0x00000001,
        Disabled = 0x00000002,
        NotPresent = 0x00000004,
        Unplugged = 0x00000008,
        All = 0x0000000F
    }

    public enum RoleEnum
    {
        Console,
        Multimedia,
        Communications
    };

    public enum StorageAccessModeEnum
    {
        Read,
        Write,
        ReadWrite
    };
}
