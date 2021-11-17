using DynamicStreamer.Contexts;
using DynamicStreamer.Helpers;
using DynamicStreamer.Nodes;
using DynamicStreamer.Queues;
using System;
using System.Linq;

namespace DynamicStreamer
{
    public record ClientStreamerConfig
    (
        VideoInputTrunkConfig[] VideoInputTrunks,
        AudioInputTrunkConfig[] AudioInputTrunks,
        VideoEncoderTrunkConfig VideoEncoderTrunk,
        VideoRenderOptions VideoRenderOptions,
        AudioEncoderTrunkConfig AudioEncoderTrunk,
        OutputTrunkConfig[] OutputTrunks,
        double BitrateDrcRatio,
        bool Disposing
    );

    public class VideoFilterChainDescriptor
    {
        public VideoFilterChainDescriptor(VideoFilterDescriptor[] filters)
        {
            Filters = filters;
        }

        public VideoFilterDescriptor[] Filters { get; }

        public override bool Equals(object obj)
        {
            return obj is VideoFilterChainDescriptor descriptor &&
                   Filters.SequenceEqual(descriptor.Filters);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Filters);
        }

        public override string ToString() => string.Join("+", Filters.Select(s => s.ToString()));

        internal bool HasHFlip() => (Filters.Count(s => s.Type == VideoFilterType.HFlip) & 1) > 0;

        internal bool HasVFlip() => (Filters.Count(s => s.Type == VideoFilterType.VFlip) & 1) > 0;
    }

    public record VideoFilterDescriptor(VideoFilterType Type, double Value, FixedFrameData Data)
    {
        public override string ToString()
        {
            return $"{Type}/{Value:F2}";
        }
    }

    public enum VideoFilterType
    {
        None,

        HFlip,
        VFlip,

        Warm,
        Cold,
        Dark,
        Light,
        Vintage,
        Sepia,
        Grayscale,

        Contrast,
        Brightness,
        Saturation,
        Gamma,

        Hue,
        Opacity,
        Sharpness,

        UserLut,

        Azure,
        B_W,
        Chill,
        Pastel,
        Romantic,
        Sapphire,
        Wine,


    }



    public record VideoInputConfigBase();
    public record VideoInputConfigFull(InputSetup Setup) : VideoInputConfigBase;
    public record VideoInputConfigSingleFrame(FixedFrameData Data) : VideoInputConfigBase; 

    public record VideoInputTrunkConfig(string Id, VideoInputConfigBase Detail, VideoFilterChainDescriptor FilterChain, PositionRect PositionRect, PositionRect PtzRect, bool Visible, int ZOrder);

    public record AudioInputTrunkConfig(string Id, InputSetup Setup, double VolumeDb, Action<FrameOutputData> OnAudioFrame);

    public record VideoEncoderTrunkConfig(
        bool ReceiverMode,
        EncoderSpec EncoderSpec,
        VideoEncoderType EncoderType,
        VideoEncoderQuality EncoderQuality,
        bool EncoderPreferNalHdr,
        bool EnableQsvNv12Optimization,
        int Bitrate,
        int FPS,
        BlendingType BlendingType,
        VideoFilterChainDescriptor FilterChain,
        Action<FrameOutputData> OnUiFrame,
        FixedFrameData Background,
        FixedFrameData NoSignal);

    public record AudioEncoderTrunkConfig(int Bitrate, int sample_rate, double VolumeDb, Action<FrameOutputData> OnAudioFrame);

    public record OutputTrunkConfig(string Id, OutputSetup OutputSetup, bool RequireBitrateControl);

    public record MixingFilterAudioSource(IDecoderContext DecoderContext, IFilterContext FilterContext, int SourceId);

    public enum SingleFrameType { Png, Jpg, Cube }

    public enum VideoRenderType { Unknown, HardwareAuto, HardwareSpecific, SoftwareFFMPEG, SoftwareDirectX }

    public enum VideoEncoderType { Software, Hardware, Auto }

    public enum FilterPixelFormat { NoFilter, Rgb, Yuv, Both }


    public class VideoRenderOptions
    {
        public VideoRenderOptions(VideoRenderType type, string adapter, IntPtr hwnd, bool tracking, int dxFailureCounter)
        {
            Type = type;
            Adapter = adapter;
            MainWindowHandle = hwnd;
            EnableObjectTracking = tracking;
            DxFailureCounter = dxFailureCounter;
        }

        public VideoRenderType Type { get; set; }
        public string Adapter { get; set; }
        public IntPtr MainWindowHandle { get; set; }
        public bool EnableObjectTracking { get; set; }
        public int DxFailureCounter { get; }

        public override bool Equals(object obj)
        {
            if (obj is VideoRenderOptions options &&
                   Type == options.Type &&
                   DxFailureCounter == options.DxFailureCounter &&
                   MainWindowHandle.Equals(options.MainWindowHandle) &&
                   EnableObjectTracking == options.EnableObjectTracking)
            {
                if (Type == VideoRenderType.HardwareSpecific)
                    return Adapter == options.Adapter;
                else
                    return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Adapter, MainWindowHandle, EnableObjectTracking);
        }

        public override string ToString() => $"{Type}:{Adapter}:{MainWindowHandle}:{EnableObjectTracking}:F{DxFailureCounter}";
    }

    public class Trunk : IDisposable
    {
        public string Id { get; set; }

        public IDisposable Detail { get; set; }

        public void Dispose()
        {
            Detail.Dispose();
        }
    }

    public class VideoInputTrunkSingleFrame : IDisposable
    {
        public FixedFrame FixedFrame { get; set; }

        public VideoInputTrunkSingleFrame()
        {
            FixedFrame = new FixedFrame();
        }

        public void Dispose()
        {
            FixedFrame.Dispose();
        }
    }

    public class VideoInputTrunkFull : IDisposable
    {
        public InputNode Input { get; set; }

        public FpsQueue<Packet> InputFpsLimitQueue { get; set; }

        public UnorderedStreamQueue<Packet> DecoderQueue { get; set; }

        public NodePool<IDecoderContext, DecoderSetup, Packet, Frame> DecoderPool { get; set; }

        public OrderedStreamQueue<Frame> FilterQueue { get; set; }

        public NodePool<IFilterContext, FilterSetup, Frame, Frame> FilterPool { get; set; }

        public OrderedStreamQueue<Frame> Filter2Queue { get; set; }

        public FilterNode Filter2 { get; set; }

        public VideoInputTrunkFull(string trunkId, IStreamerBase streamer, Action inputChanged)
        {
            DecoderQueue = new UnorderedStreamQueue<Packet>(new NodeName("V", trunkId, "Dq", 2), streamer.PacketPool);
            FilterQueue = new OrderedStreamQueue<Frame>(new NodeName("V", trunkId, "F1q", 3), streamer.FramePool);
            Filter2Queue = new OrderedStreamQueue<Frame>(new NodeName("V", trunkId, "F2q", 4), streamer.FramePool);

            Input = new InputNode(new NodeName("V", trunkId, "I", 0), inputChanged, streamer);

            DecoderPool = new NodePool<IDecoderContext, DecoderSetup, Packet, Frame>(new NodeName("V", trunkId, "D", 2), streamer, i => new DecoderNode(new NodeName("V", trunkId, "D", 2), streamer));
            FilterPool = new NodePool<IFilterContext, FilterSetup, Frame, Frame>(new NodeName("V", trunkId, "F1", 3), streamer, i => new FilterNode(new NodeName("V", trunkId, "F1", 3), streamer));
            Filter2 = new FilterNode(new NodeName("V", trunkId, "F2", 4), streamer);
        }

        public void Dispose()
        {
            Input.Dispose();
            DecoderPool.Dispose();
            FilterPool.Dispose();
            Filter2.Dispose();

            DecoderQueue.Dispose();
            FilterQueue.Dispose();
            Filter2Queue.Dispose();
        }
    }

    public class VideoEncoderTrunk : IDisposable
    {
        public UnorderedStreamQueue<Frame> BlenderQueue { get; set; }

        public VideoBlenderNode Blender { get; set; }

        public FixedFrame BackgroundFrame { get; set; }

        public DuplicateQueue<Frame> EncoderAndUiFilterDuplicateQueue { get; set; }

        public FpsQueue<Frame> UiFpsFilterQueue { get; set; }

        public UnorderedStreamQueue<Frame> UiFilterQueue { get; set; }

        public OrderedStreamQueue<Frame> EncoderQueue { get; set; } 

        public FilterNode UiFilter { get; set; }


        public UnorderedStreamQueue<Frame> PreEncoderFilterQueue { get; set; }


        public NodePool<IFilterContext, FilterSetup, Frame, Frame> PreEncoderFilterPool { get; set; }

        public FrameOutput UiOut { get; set; }

        public EncoderNode EncoderNode { get; set; }

        public VideoEncoderTrunk(IStreamerBase streamer, OverloadController overloadController)
        {
            BlenderQueue = new UnorderedStreamQueue<Frame>(new NodeName("VE", null, "BLq", 1), streamer.FramePool);
            Blender = new VideoBlenderNode(new NodeName("VE", null, "BL", 1), streamer, overloadController);

            PreEncoderFilterQueue = new UnorderedStreamQueue<Frame>(new NodeName("VE", null, "FPreq", 3), streamer.FramePool);
            PreEncoderFilterPool = new NodePool<IFilterContext, FilterSetup, Frame, Frame>(new NodeName("VE", null, "FPre", 3), streamer, i => new FilterNode(new NodeName("VE", null, "FPre", 3, i), streamer));

            EncoderQueue = new OrderedStreamQueue<Frame>(new NodeName("VE", null, "Eq", 4), streamer.FramePool);
            EncoderNode = new EncoderNode(new NodeName("VE", null, "E", 4), streamer);
            

            EncoderAndUiFilterDuplicateQueue = new DuplicateQueue<Frame>(streamer.FramePool);
            UiFilterQueue = new UnorderedStreamQueue<Frame>(new NodeName("VE", null, "FUIq", 9), streamer.FramePool, 2);

            BackgroundFrame = new FixedFrame();
        }

        public void Dispose()
        {
            EncoderNode.Dispose();
            PreEncoderFilterPool.Dispose();
            UiFilter?.Dispose();
            Blender.Dispose();

            BackgroundFrame.Dispose();

            UiFilterQueue.Dispose();
            EncoderQueue.Dispose();
            PreEncoderFilterQueue.Dispose();
        }
    }

    public class AudioInputTrunk : IDisposable
    {
        public string Id { get; set; }

        public InputNode Input { get; set; }

        public UnorderedStreamQueue<Packet> DecoderQueue { get; set; } 
        public DecoderNode Decoder { get; set; }
        public UnorderedStreamQueue<Frame> FilterQueue { get; set; } 

        public FilterNode Filter { get; set; }

        public DuplicateQueue<Frame> MixerAndUiFilterQueue { get; set; }

        public FrameOutput UiOutput { get; set; }

        public void Dispose()
        {
            Input?.Dispose();
            Decoder?.Dispose();
            Filter?.Dispose();

            FilterQueue?.Dispose();
            DecoderQueue?.Dispose();
        }
    }

    public class AudioEncoderTrunk : IDisposable
    {
        public AudioMixingQueue MixingFilterQueue { get; set; } 

        public FilterNode MixingFilter { get; set; }

        public DuplicateQueue<Frame> EncoderAndUiFilterQueue { get; set; }

        public FrameOutput UiFilterQueue { get; set; }

        public UnorderedStreamQueue<Frame> EncoderQueue { get; set; } 

        public EncoderNode EncoderNode { get; set; }

        public void Dispose()
        {
            MixingFilter?.Dispose();
            EncoderNode?.Dispose();
            MixingFilterQueue?.Dispose();
            EncoderQueue?.Dispose();
        }
    }

    public class OutputTrunk : IDisposable
    {
        public string Id { get; set; }

        public OutputNode OutputNode { get; set; }

        public void Dispose()
        {
            OutputNode.Dispose();
        }
    }

    public class PositionRect
    {
        public static PositionRect Full = new PositionRect { Left = 0, Top = 0, Width = 1, Height = 1 };

        public PositionRect()
        {
        }

        public PositionRect(double x, double y, double width, double height)
        {
            Left = x;
            Top = y;
            Width = width;
            Height = height;
        }

        public double Top { get; set; }

        public double Left { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double Right => Left + Width;

        public double Bottom => Top + Height;

        public bool IsFullScreen() => DoubleEqual(Top, 0.0) && DoubleEqual(Left, 0.0) && DoubleEqual(Width, 1.0) && DoubleEqual(Height, 1.0);

        private static bool DoubleEqual(double d1, double d2) => Math.Abs(d1 - d2) < 0.00001;

        public bool Contains(double x, double y)
        {
            if (x >= Left && x - Width <= Left && 
                y >= Top && y - Height <= Top)
            {
                return true;
            }
            return false;
        }

        public override bool Equals(object obj)
        {
            return obj is PositionRect rect &&
                   Top == rect.Top &&
                   Left == rect.Left &&
                   Width == rect.Width &&
                   Height == rect.Height;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Top, Left, Width, Height);
        }
    }
}
