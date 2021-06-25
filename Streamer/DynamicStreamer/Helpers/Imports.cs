using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicStreamer
{
    public enum ErrorCodes : int
    {
        TryAgainLater = -11,
        InvalidArgument = -22,
        TimeoutOrInterrupted = unchecked((int)0xabb6a7bb),

        Ok = 0,

        NullFilter = -33000000,
        ContextIsNotOpened = -33000006,

        InternalErrorUnknown = -33000001,
        InternalErrorUnknown2 = -33000002,
        InternalErrorUnknown3 = -33000003,
        InternalErrorUnknown4 = -33000004,
        InternalErrorUnknown5 = -33000005,
        InternalErrorLast = -33001000,
    };




    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct PacketProperties
    {
        public long Pts;
        public long Dts;
        public IntPtr DataPtr;
        public int Size;
        public int StreamIndex;
        public int Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct FramePlaneDesc
    {
        public IntPtr Data;
        public int Stride;
        public int StrideCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct FrameProperties
    {
        public int Width;
        public int Height;
        public int Samples;
        public int Format;
        public long Pts;
        public IntPtr DataPtr0;
        public IntPtr DataPtr1;
        public IntPtr DataPtr2;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct AVRational
    {
        public int num; ///< Numerator
        public int den; ///< Denominator

        public override bool Equals(object obj)
        {
            return obj is AVRational rational &&
                   num == rational.num &&
                   den == rational.den;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(num, den);
        }

        public static bool operator ==(AVRational left, AVRational right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AVRational left, AVRational right)
        {
            return !(left == right);
        }
    }

    public enum AVMediaType
    {
        AVMEDIA_TYPE_UNKNOWN = -1,  /// Usually treated as AVMEDIA_TYPE_DATA
        AVMEDIA_TYPE_VIDEO,
        AVMEDIA_TYPE_AUDIO,
        AVMEDIA_TYPE_DATA,          /// Opaque data information usually continuous
        AVMEDIA_TYPE_SUBTITLE,
        AVMEDIA_TYPE_ATTACHMENT,    /// Opaque data information usually sparse
        AVMEDIA_TYPE_NB
    };

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct CodecProperties
    {
        public AVMediaType codec_type;
        public int codec_id;
        public int codec_tag;
        public int extradata_size;
        public int format;
        public long bit_rate;
        public int bits_per_coded_sample;
        public int bits_per_raw_sample;
        public int profile;
        public int level;
        public int width;
        public int height;
        public AVRational sample_aspect_ratio;

        public int field_order;

        public int color_range;
        public int color_primaries;
        public int color_trc;
        public int color_space;
        public int chroma_location;
        public int video_delay;

        public ulong channel_layout;
        public int channels;
        public int sample_rate;
        public int block_align;
        public int frame_size;
        public int initial_padding;
        public int trailing_padding;
        public int seek_preroll;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
        public byte[] extradata;

        public override bool Equals(object obj)
        {
            if (obj is CodecProperties properties)
            {
                return codec_type == properties.codec_type &&
                        codec_id == properties.codec_id &&
                        codec_tag == properties.codec_tag &&
                        extradata_size == properties.extradata_size &&
                        format == properties.format &&
                         bit_rate == properties.bit_rate && 
                        bits_per_coded_sample == properties.bits_per_coded_sample &&
                        bits_per_raw_sample == properties.bits_per_raw_sample &&
                        profile == properties.profile &&
                        level == properties.level &&
                        width == properties.width &&
                        height == properties.height &&
                        sample_aspect_ratio == properties.sample_aspect_ratio &&
                        field_order == properties.field_order &&
                        color_range == properties.color_range &&
                        color_primaries == properties.color_primaries &&
                        color_trc == properties.color_trc &&
                        color_space == properties.color_space &&
                        chroma_location == properties.chroma_location &&
                        video_delay == properties.video_delay &&
                        channel_layout == properties.channel_layout &&
                        channels == properties.channels &&
                        sample_rate == properties.sample_rate &&
                        block_align == properties.block_align &&
                        frame_size == properties.frame_size &&
                        initial_padding == properties.initial_padding &&
                        trailing_padding == properties.trailing_padding &&
                        seek_preroll == properties.seek_preroll && 
                        extradata.SequenceEqual(properties.extradata);
            }
            return false;
        }

        internal bool EqualsNoBitrate(CodecProperties properties)
        {
            // allows to switch encoder bitrate  without reinit of output
            //&& extradata.SequenceEqual(properties.extradata);
            //extradata_size == properties.extradata_size &&
            // bit_rate == properties.bit_rate && 

            return codec_type == properties.codec_type &&
                    codec_id == properties.codec_id &&
                    codec_tag == properties.codec_tag &&
                    format == properties.format &&
                    bits_per_coded_sample == properties.bits_per_coded_sample &&
                    bits_per_raw_sample == properties.bits_per_raw_sample &&
                    profile == properties.profile &&
                    level == properties.level &&
                    width == properties.width &&
                    height == properties.height &&
                    sample_aspect_ratio == properties.sample_aspect_ratio &&
                    field_order == properties.field_order &&
                    color_range == properties.color_range &&
                    color_primaries == properties.color_primaries &&
                    color_trc == properties.color_trc &&
                    color_space == properties.color_space &&
                    chroma_location == properties.chroma_location &&
                    video_delay == properties.video_delay &&
                    channel_layout == properties.channel_layout &&
                    channels == properties.channels &&
                    sample_rate == properties.sample_rate &&
                    block_align == properties.block_align &&
                    frame_size == properties.frame_size &&
                    initial_padding == properties.initial_padding &&
                    trailing_padding == properties.trailing_padding &&
                    seek_preroll == properties.seek_preroll; 
        }

        public override int GetHashCode() => throw new NotSupportedException();

        public override string ToString()
        {
            if (codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
            {
                return $"{Core.Const.GetCodecName(codec_id)} {sample_rate}x{channels}ch {Core.Const.GetAudioFormat(format)}";
            }
            else if (codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                return $"{width}x{height} {Core.Const.GetVideoFormat(format)} {Core.Const.GetCodecName(codec_id)}";
            }
            else return $"Unknown AVMediaType({codec_type})";
        }
    };

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct InputStreamProperties
    {
        public CodecProperties CodecProps;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct OutputStreamProperties
    {
        public CodecProperties CodecProps;

        public AVRational input_time_base;

        public override bool Equals(object obj)
        {
            if (obj is OutputStreamProperties properties)
            {
                return input_time_base == properties.input_time_base &&
                    CodecProps.EqualsNoBitrate(properties.CodecProps); 
            }
            return false;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct DecoderProperties
    {
        public int pix_fmt;
        public int sample_fmt;
        public ulong channel_layout;

    };

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct FilterInputSpec
    {
        //video
        public int pix_fmt;
        public int width;
        public int height;
        public AVRational sample_aspect_ratio;
        public int color_range;
        public int BestQuality;

        //audio
        public int sample_rate;
        public int sample_fmt;
        public ulong channel_layout;

        //common
        public AVRational time_base;

        public override string ToString()
        {
            if (sample_fmt == 0)
                return $"{width}x{height} {Core.Const.GetVideoFormat(pix_fmt)}";
            else
                return $"{sample_rate} {Core.Const.GetAudioFormat(sample_fmt)}";
        }
    };


    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct FilterOutputSpec
    {
        public int pix_fmt;

        public int sample_rate;
        public int sample_fmt;
        public ulong channel_layout;
        public int required_frame_size;

        public override string ToString()
        {
            if (sample_fmt == 0)
                return $"{Core.Const.GetVideoFormat(pix_fmt)}";
            else
                return $"{sample_rate} {Core.Const.GetAudioFormat(sample_fmt)}";
        }
    };


    public enum VideoEncoderQuality { Quality = 0, BalancedQuality = 1, Balanced = 2, Speed = 3 }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct EncoderSpec
    {
        //video
        public AVRational sample_aspect_ratio;
        public int width;
        public int height;

        //audio
        public int sample_rate;
        public ulong channel_layout;

        //common
        public AVRational time_base;
        public VideoEncoderQuality Quality;

        public override string ToString()
        {
            if (width != 0)
                return $"{width}x{height}";
            else
                return "";
        }
    };

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct EncoderBitrate
    {
        public int bit_rate;
        public int max_rate;
        public int buffer_size;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct EncoderProperties
    {
        //video
        public int pix_fmt;

        //audio
        public int sample_fmt;
        public int required_frame_size;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct OutputStreamsConfig
    {
        public int count;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public CodecProperties[] CodecProperties;
    };
}
