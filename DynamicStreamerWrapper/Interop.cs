using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Streamster.DynamicStreamerWrapper
{
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    [Guid("9ADD4A25-2BD3-42E5-A06F-C8548ECC9A95")]
    public interface IDynamicStreamer
    {
        [DispId(1)]
        void SetEncoder(string videoCodec, string videoOptions, string fallbackvideoCodec, string fallbackVideoOptions, int videoMaxBitrate, string audioCodec, string audioOptions, int audioMaxBitrate);
        [DispId(2)]
        void SetInput(string type, string input, string options, int fps, int width, int height);
        [DispId(3)]
        int AddOutput(string type, string input, string options, IDynamicStreamerDecoderCallback pCallback);
        [DispId(4)]
        void RemoveOutput(int id);

        [DispId(5)]
        [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)]
        Array GetStatistics();
        [DispId(6)]
        void SetCallback(IDynamicStreamerCallback pCallback);
        [DispId(7)]
        void SetFilter(string videoFilter);
        [DispId(8)]
        void SetDirectFrameCallback(IDynamicStreamerDecoderCallback pCallback);
        [DispId(9)]
        int GetSupportedCodecs();
    }

    [Guid("B2991AF4-6762-431C-A615-EB3B7B3CE883")]
    [InterfaceType(1)]
    public interface IDynamicStreamerDecoderCallback
    {
        void NotifyFrame(int width, int height, int length, long data);
    }

    [ComImport]
    [Guid("B2991AF4-6762-431C-A615-EB3B7B3CE882")]
    [InterfaceType(1)]
    public interface IDynamicStreamerCallback
    {
        void NotifyError(int errorCode, string errorMessage, string pattern);
    }

    [ClassInterface(ClassInterfaceType.None)]
    [Guid("E070BC4F-392A-43AA-9E89-039B68859242")]
    [ComImport]
    public class DynamicStreamerClass
    {
    }

    [ComImport]
    [CoClass(typeof(DynamicStreamerClass))]
    [Guid("9ADD4A25-2BD3-42E5-A06F-C8548ECC9A95")]
    public interface DynamicStreamer : IDynamicStreamer
    {
    }

    [Guid("9738B6EE-8144-4105-955E-9F9FEA664EAF")]
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IDynamicStreamerStatistics
    {
        [DispId(1)]
        long Interval { [DispId(1)] get; }

        [DispId(2)]
        int Overall { [DispId(2)] get; }

        [DispId(3)]
        int id { [DispId(3)] get; }

        [DispId(4)]
        [return: MarshalAs(UnmanagedType.SafeArray, SafeArraySubType = VarEnum.VT_VARIANT)]
        Array GetValues();

        [DispId(5)]
        int GetError([MarshalAs(UnmanagedType.BStr)] out string errorMessage);
    }

    public enum StatisticType
    {
        statisticTypeVideoFrames,
        statisticTypeVideoBytes,
        statisticTypeAudioFrames,
        statisticTypeAudioBytes,
        statisticTypeProcessingTime,
        statisticTypeDropped,
        statisticTypeErrors,
        statisticTypeCount,
    }
}
