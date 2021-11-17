using DynamicStreamer.DirectXHelpers;
using DynamicStreamer.Helpers;
using DynamicStreamer.Nodes;
using SharpDX;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DynamicStreamer.Contexts
{
    public enum BlendingType { Smart, Linear, Lanczos, BilinearLowRes, Bicubic, Area}

    public record VideoBlenderSetup(int Width, int Height, int Fps, int DelayFromRuntimeFrames, int PushPipelineDelayFrames, long MaxDelay, long ComebackDelay, int OutputPixelFormat, BlendingType BlendingType, DirectXContext Dx, VideoBlenderSetupWeakOptions WeakOptions) : IDisposable
    {
        public void Dispose()
        {
            WeakOptions.Dispose();
        }

        public override string ToString()
        {
            var pixelFormat = Dx != null ? "dx" : Core.Const.GetVideoFormat(OutputPixelFormat);
            var output = $"{Width}-{Height}-{Fps}-{pixelFormat}";
            if (WeakOptions.FilterChain != null)
                output += "+" + WeakOptions.FilterChain;

            var inputs = string.Join("|", (IEnumerable<VideoBlenderInputDescription>)WeakOptions.Inputs);

            return $"{inputs} ={BlendingType}=> {output}";
        }
    }

    public class VideoBlenderInputDescription
    {
        public string Id { get; set; }
        public int SourceId { get; set; }
        public PositionRect Rect { get; set; }
        public bool Visible { get; set; }
        public int ZOrder { get; set; }
        public PositionRect Ptz { get; set; }
        public int PixelFormat { get; set; } = -1;
        public RefCountedFrame FixedFrame { get; set; }
        public VideoBlenderInputBehavior Behavior { get; set; }

        public VideoFilterChainDescriptor FilterChain { get; set; }

        public void Dispose()
        {
            FixedFrame?.RemoveRef();
        }

        public override string ToString()
        {
            var r = $"{Id}";
            if (!Rect.IsFullScreen())
                r +=$" Pos{Rect.Left:F2}-{Rect.Top:F2}-{Rect.Width:F2}-{Rect.Height:F2}";
            if (!Ptz.IsFullScreen())
                r += $" Ptz{Ptz.Left:F2}-{Ptz.Top:F2}-{Ptz.Width:F2}-{Ptz.Height:F2}";
            if (PixelFormat >= 0)
                r += "-" + Core.Const.GetVideoFormat(PixelFormat);
            if (FixedFrame != null)
                r += " fixed-frame";
            if (FilterChain != null)
                r += "+" + FilterChain;
            return r;
        }
    }

    public record VideoBlenderInputBehavior(int SameFrameDelayMs, int BlackScreenDelayMs);

    public class VideoBlenderSetupWeakOptions : IDisposable
    {
        public VideoBlenderInputDescription[] Inputs { get; set; }

        public PixelFormatGroup PixelFormatGroup { get; set; }

        public FixedFrameData NoSignalData { get; set; }

        public VideoFilterChainDescriptor FilterChain { get; set; }

        public void Dispose()
        {
            foreach( var i in Inputs)
                i.Dispose();
        }

        public override bool Equals(object obj)
        {
            return true;
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }

    public class VideoBlenderContext : IDisposable
    {
        private VideoBlenderSetup _setup;
        private VideoBlenderHelper _helper = new VideoBlenderHelper();
        private List<VideoBlenderInputRuntime> _inputRuntimes = new List<VideoBlenderInputRuntime>();
        private List<VideoBlenderInputRuntime> _inputRuntimesSorted = new List<VideoBlenderInputRuntime>();
        private int _sourceIdOffset = 0;
        private readonly int _fps;
        private readonly PayloadPool<Frame> _framePool;
        private readonly IStreamerBase _streamer;
        private readonly OverloadController _overloadController;
        private readonly Action _pushPipeline;
        private long _currentFpsTicks = 0;
        private long _delayFromRuntime;
        private long _pushPipelineDelay;
        private TimerSubscription _timer;
        private long _lastReadTime;

        private readonly DirectXPipeline<BlendingConstantBuffer> _directXPipeline;
        private readonly DirectXPipeline<BlendingConstantBuffer> _directXPipelineLowRes;
        private DeviceContext _defferedContext;
        private readonly DirectXFilterRuntime _dxFilterRuntime = new DirectXFilterRuntime();

        private static Dictionary<BlendingType, BlendingTypeDescriptor> s_descriptors;
        private readonly NodeName _name;

        static VideoBlenderContext()
        {
            s_descriptors = new Dictionary<BlendingType, BlendingTypeDescriptor>
            {
                [BlendingType.Linear] = new BlendingTypeDescriptor("default.hlsl", "VSDefault", "PSDrawAlphaDivide"),//new BlendingTypeDescriptor("simple.hlsl", "vs", "ps"),
                [BlendingType.Lanczos] = new BlendingTypeDescriptor("lanczos_scale.hlsl", "VSDefault", "PSDrawLanczosRGBADivide"),
                [BlendingType.BilinearLowRes] = new BlendingTypeDescriptor("bilinear_lowres_scale.hlsl", "VSDefault", "PSDrawLowresBilinearRGBADivide"),
                [BlendingType.Bicubic] = new BlendingTypeDescriptor("bicubic_scale.hlsl", "VSDefault", "PSDrawBicubicRGBADivide"),
                [BlendingType.Area] = new BlendingTypeDescriptor("area.hlsl", "VSDefault", "PSDrawAreaRGBADivide"),
            };
        }


        public VideoBlenderContext(VideoBlenderSetup setup, PayloadPool<Frame> framePool, IStreamerBase streamer, OverloadController overloadController, Action pushPipeline)
        {
            _name = new NodeName("VE", null, "BL", 1);
            _fps = setup.Fps;
            _framePool = framePool;
            _streamer = streamer;
            _overloadController = overloadController;
            _pushPipeline = pushPipeline;
            _delayFromRuntime = ToTime(setup.DelayFromRuntimeFrames); // 3 frames in client
            _pushPipelineDelay = ToTime(setup.PushPipelineDelayFrames); // 3 frames

            Reconfigure(setup);

            _timer = _streamer.Subscribe(15, OnTimer);
            if (setup.Dx != null)
            {
                if (setup.BlendingType == BlendingType.Smart)
                {
                    _directXPipeline = LoadPipline(BlendingType.Linear, setup.Dx);
                    _directXPipelineLowRes = LoadPipline(BlendingType.BilinearLowRes, setup.Dx);
                }
                else 
                    _directXPipeline = LoadPipline(setup.BlendingType, setup.Dx);

            }

            _currentFpsTicks = ToTicks(Core.GetCurrentTime() - 600_000); // -60ms
        }

        private DirectXPipeline<BlendingConstantBuffer> LoadPipline(BlendingType blendingType, DirectXContext dx)
        {
            var desc = s_descriptors[blendingType];
            return new DirectXPipeline<BlendingConstantBuffer>(new DirectXPipelineConfig
            {
                Blend = true,
                PixelShaderFile = desc.ShaderFile,
                PixelShaderFunction = desc.PixelShaderFunction,

                VertexShaderFile = desc.ShaderFile,
                VertexShaderFunction = desc.VetextShaderFunction,

            }, dx);
        }

        public void Dispose()
        {
            _dxFilterRuntime?.Dispose();
            _directXPipeline?.Dispose();
            _directXPipelineLowRes?.Dispose();
            _defferedContext?.Dispose();
            _timer.Unsubscribe();

            foreach (var toRemove in _inputRuntimes)
                toRemove.Dispose(_framePool);

            _helper?.Dispose();
            _helper = null;
        }

        public int Write(Data<Frame> data, VideoBlenderSetup setup, StatisticDataOfBlenderNode stat)
        {
            if (!ReferenceEquals(_setup, setup))
            {
                if (IsSourceChanged(_setup, setup))
                    Reconfigure(setup);
                _setup = setup;
            }

            if (data != null)
            {
                var sourceId = data.SourceId - _sourceIdOffset;

                if (sourceId >= 0 && sourceId < _inputRuntimes.Count)
                {
                    var queue = _inputRuntimes[sourceId].Frames;
                    queue.AddLast(data);

                    var inputStat = stat.GetInput(sourceId);
                    inputStat.InFrames++;
                    inputStat.QueueSize = queue.Count;
                    inputStat.Delay = (Core.GetCurrentTime() - data.Payload.GetPts());

                    while (queue.Count > 60)
                    {
                        var first = queue.First.Value;
                        queue.RemoveFirst();
                        _streamer.FramePool.Back(first.Payload);
                        Core.LogWarning($"Removing frame from {sourceId} due to queue > 60", "Removing frame from");
                    }
                }
                else
                {
                    _streamer.FramePool.Back(data.Payload);
                    Core.LogWarning($"Unexpected source Id {sourceId}");
                }
            }
            return 0;
        }

        private Data<Frame> GetRuntimeFrame(VideoBlenderInputRuntime runtime)
        {
            if (runtime.Frames.Count > 0)
            {
                long first = ToTicks(runtime.Frames.First.Value.Payload.GetPts());

                if (_currentFpsTicks < first)
                    return runtime.Frames.First.Value;


                if (first + runtime.Tension >= _currentFpsTicks && runtime.Tension > -1)
                {
                    runtime.Tension--;
                }

                while (first + runtime.Tension < _currentFpsTicks && runtime.Frames.Count > 1)
                {
                    _framePool.Back(runtime.Frames.First.Value.Payload);
                    runtime.Frames.RemoveFirst();
                    first = ToTicks(runtime.Frames.First.Value.Payload.GetPts());

                    if (first + runtime.Tension < _currentFpsTicks && runtime.Tension < 1) // this is kind of shock absorber. Can have values from -1 to 1
                    {
                        // no need to remove more this cycle
                        runtime.Tension++;
                    }
                }

                if (runtime.BlackScreenDelay >= 0 && ToTime(_currentFpsTicks) - ToTime(first) > runtime.BlackScreenDelay) // to old
                {
                    //Core.LogInfo($"{Core.FormatTicks(ToTime(_currentFpsTicks))} - {Core.FormatTicks(ToTime(first))} Blend NoSignal");
                    return GetOrCreateNoSignalFrame(runtime);
                }
                else
                {
                    //Core.LogInfo($"{Core.FormatTicks(ToTime(_currentFpsTicks))} - {Core.FormatTicks(ToTime(first))} -({runtime.Frames.Count})- {Core.FormatTicks(runtime.Frames.Last.Value.Payload.GetPts())} Blend");
                    return runtime.Frames.First.Value;
                }
            }
            else
            {
                var now = Core.GetCurrentTime();

                if (now - runtime.StartupTime > 500_0000) // 500 ms
                    return GetOrCreateNoSignalFrame(runtime);
                else
                    return null;
            }
        }

        private void OnTimer()
        {
            long now = Core.GetCurrentTime();
            if (now - _lastReadTime > _pushPipelineDelay)
            {
                _pushPipeline();
            }
        }

        public ErrorCodes Read(Frame resultPayload, out PayloadTrace resultTrace)
        {
            long now = Core.GetCurrentTime();
            _lastReadTime = now;
            long currentFpsTime = ToTime(_currentFpsTicks);
            long currentFrameDelay = now - currentFpsTime;

            if (currentFrameDelay > _setup.MaxDelay) //2 sec
            {
                // we too late
                Core.LogError($"Blender skips batch due to high delay from now {currentFrameDelay / 10_000}ms");

                _currentFpsTicks = ToTicks(now - _setup.ComebackDelay); // -300 ms
                currentFpsTime = ToTime(_currentFpsTicks);
                currentFrameDelay = now - currentFpsTime;
            }

            bool runtimesReady = currentFrameDelay > 0; // don't go behind real-time. Especially relevant for cases when all _inputRuntimes are Fixed images

            if (currentFrameDelay > 0 && currentFrameDelay <= _delayFromRuntime)
            {
                foreach (var runtime in _inputRuntimes)
                {
                    bool ok = false;
                    if (runtime.Frames.Count > 0)
                    {
                        long first = ToTicks(runtime.Frames.First.Value.Payload.GetPts());
                        long last = ToTicks(runtime.Frames.Last.Value.Payload.GetPts());

                        if (_currentFpsTicks <= last)
                        {
                            ok = true;
                        }
                    }
                    else if (runtime.FixedFrame != null)
                    {
                        ok = true;
                    }

                    if (!ok)
                    {
                        runtimesReady = false;
                        break;
                    }
                }
            }

            // prepare frame
            if (runtimesReady)
            {
                bool hasAnyContent;
                if (_setup.Dx == null)
                    hasAnyContent = RenderFFMpeg(resultPayload, out resultTrace);
                else
                    hasAnyContent = RenderDirectX(resultPayload, out resultTrace);

                if (hasAnyContent)
                {
                    if (_overloadController == null)
                        _currentFpsTicks += 1;
                    else 
                        _overloadController.Increment(ref _currentFpsTicks, 1);
                    return ErrorCodes.Ok;
                }
            }
            resultTrace = null;
            return ErrorCodes.TryAgainLater;
        }

        private Data<Frame> GetRuntimeOrFixedFrame(VideoBlenderInputRuntime runtime)
        {
            if (runtime.FixedFrame == null)
                return GetRuntimeFrame(runtime);
            else
                return new Data<Frame>(runtime.FixedFrame.Instance.Item, 0, 0, null);
        }

        private void UpdateTrace(Data<Frame> frameData, ref PayloadTrace trace)
        {
            if (frameData.Trace != null)
            {
                if (trace == null)
                    trace = PayloadTrace.Create(_name, frameData.Trace, 0);
                else
                    trace.AddPrevious(frameData.Trace);
            }
        }

        private bool RenderDirectX(Frame resultPayload, out PayloadTrace trace)
        {
            trace = null;
            bool hasAnyContent = false;
            var dx = _setup.Dx;
            try
            {
                if (dx.IsBrokenAndLog("Blender"))
                    return false;

                var texture = dx.Pool.Get("Blender", DirectXResource.Desc(_setup.Width, _setup.Height, SharpDX.DXGI.Format.B8G8R8A8_UNorm, BindFlags.ShaderResource | BindFlags.RenderTarget, ResourceUsage.Default, ResourceOptionFlags.None));

                _defferedContext = _defferedContext ?? new DeviceContext(dx.Device);
                //using 
                using (var rtv = texture.GetRenderTargetView())
                {
                    int counter = 0;

                    foreach (var runtime in _inputRuntimesSorted)
                    {
                        var frameData = GetRuntimeOrFixedFrame(runtime);
                        if (frameData != null && runtime.Description.Visible)
                        {
                            hasAnyContent = true;

                            UpdateTrace(frameData, ref trace);

                            var xResource = frameData.Payload.DirectXResourceRef.Instance;

                            if (xResource.CommandList != null)
                            {
                                _defferedContext.ExecuteCommandList(xResource.CommandList, false);
                                xResource.CommandList.Dispose();
                                xResource.CommandList = null;
                            }

                            var pipeline = GetPipeline(runtime, texture, xResource);
                            SetDebugColor(pipeline, counter++);
                            UpdatePositionInDxPipeline(runtime, pipeline, xResource.Texture2D.Description.Width, xResource.Texture2D.Description.Height);

                            var filteredResource = runtime.DirectXFilterRuntime.Render(dx, runtime.Description.FilterChain, _defferedContext, xResource);
                            if (ReferenceEquals(filteredResource, xResource))
                            {
                                using (var srv = filteredResource.GetShaderResourceView())
                                    pipeline.Render(_defferedContext, rtv, srv);
                                dx.Flush(_defferedContext, "Blender Flush All");
                            }
                            else
                            {
                                dx.Flush(_defferedContext, "Blender Flush All");
                                using (var srvFiltered = filteredResource.GetShaderResourceView())
                                    pipeline.Render(_defferedContext, rtv, srvFiltered);
                                dx.Flush(_defferedContext, "Blender Flush All");
                                filteredResource.Dispose();
                            }
                        }
                    }
                }

                if (hasAnyContent)
                {
                    if (trace != null)
                        trace = PayloadTrace.Create(_name, trace);

                    dx.Flush(_defferedContext, "Blender Flush All");
                    var allFilteredResource = _dxFilterRuntime.Render(dx, _setup.WeakOptions.FilterChain, _defferedContext, texture);
                    dx.Flush(_defferedContext, "Blender Flush All");

                    resultPayload.InitFromDirectX(allFilteredResource, _currentFpsTicks);

                    if (allFilteredResource != texture)
                        dx.Pool.Back(texture);
                }
            }
            catch (Exception e)
            {
                dx.Broken(e);
            }

            return hasAnyContent;
        }

        private DirectXPipeline<BlendingConstantBuffer> GetPipeline(VideoBlenderInputRuntime runtime, DirectXResource canvas, DirectXResource image)
        {
            if (_directXPipelineLowRes != null)
            {
                if (runtime.Description.Ptz.Width > 0 && runtime.Description.Ptz.Height > 0)
                {
                    var heightScale = runtime.Description.Rect.Height / runtime.Description.Ptz.Height;
                    var widthScale = runtime.Description.Rect.Width / runtime.Description.Ptz.Width;
                    if (heightScale * canvas.Texture2D.Description.Height < 0.8 * image.Texture2D.Description.Height ||
                        widthScale * canvas.Texture2D.Description.Width < 0.8 * image.Texture2D.Description.Width)
                        return _directXPipelineLowRes;
                }
            }
            return _directXPipeline;
        }

        private void UpdatePositionInDxPipeline(VideoBlenderInputRuntime runtime, DirectXPipeline<BlendingConstantBuffer> pipeline,  int width, int height)
        {
            var rect = runtime.Description.Rect;
            var ptz = runtime.Description.Ptz;

            var currect = runtime.VertexBuffer;
            pipeline.UpdatePosition(new RectangleF((float)rect.Left, (float)rect.Top, (float)rect.Width, (float)rect.Height),
                new RectangleF((float)ptz.Left, (float)ptz.Top, (float)ptz.Width, (float)ptz.Height),
                runtime.Description.FilterChain?.HasHFlip() == true, runtime.Description.FilterChain?.HasVFlip() == true, ref currect);
            runtime.VertexBuffer = currect;

            pipeline.SetExternalPosition(new Viewport(0, 0, _setup.Width, _setup.Height), currect);

            pipeline.SetConstantBuffer(new BlendingConstantBuffer
            { 
                ViewProj = Matrix.Identity,
                base_dimension = new Vector2(width, height),
                base_dimension_i = new Vector2(1f / width, 1f/height),
                undistort_factor = 1.0f
            }, true);
        }

        private void SetDebugColor(DirectXPipeline<BlendingConstantBuffer> pipeline, int counter)
        {
            if (counter == 0)
                pipeline.SetDebugColor(0, 0.1f, 0.3f, 1);
            else
                pipeline.ResetDebugColor();
        }

        private bool RenderFFMpeg(Frame resultPayload, out PayloadTrace trace)
        {
            bool first = true;
            trace = null;

            var frames = _inputRuntimesSorted.Where(s => s.Description.Visible).
                                        Select(s => new { runtime = s, frameData = GetRuntimeOrFixedFrame(s) }).
                                        Where(s => s.frameData != null).ToList();

            if (frames.Count == 1)
            {
                resultPayload.CopyContentFromAndSetPts(frames[0].frameData.Payload, _currentFpsTicks);
            }
            else if (frames.Count > 1)
            {
                Frame frameMainCopy = null;
                foreach (var s in frames)
                {
                    var frameData = s.frameData;
                    var runtime = s.runtime;
                    var frame = frameData.Payload;
                    UpdateTrace(frameData, ref trace);

                    if (first)
                    {
                        frameMainCopy = _framePool.Rent();
                        frameMainCopy.CopyContentFrom(frame);
                        _helper.Init(frameMainCopy, _setup.WeakOptions.PixelFormatGroup.BlendType);
                        first = false;
                    }
                    else
                    {
                        if (frame.Properties.Height != runtime.SlicesHeight)
                        {
                            runtime.SlicesHeight = frame.Properties.Height;
                            runtime.Slices = GetSlices(runtime, frame.Properties.Height);
                        }

                        int x = (int)(runtime.Description.Rect.Left * _setup.Width);
                        int y = (int)(runtime.Description.Rect.Top * _setup.Height);

                        if (runtime.Slices == null)
                        {
                            _helper.Add(frame, x, y, 0, frame.Properties.Height);
                        }
                        else
                        {
                            Task.WhenAll(runtime.Slices.Select(s => Task.Run(() => _helper.Add(frame, x, y, s.yOffset, s.yCount))).ToArray()).Wait();
                        }
                    }
                }
                _helper.Get(resultPayload, _currentFpsTicks);
                _framePool.Back(frameMainCopy);
            }

            if (frames.Count > 0)
            {
                if (trace != null)
                    trace = PayloadTrace.Create(_name, trace);

                if (resultPayload.Properties.Width != _setup.Width ||
                    resultPayload.Properties.Height != _setup.Height ||
                    resultPayload.Properties.Format != _setup.OutputPixelFormat)
                {
                    Core.LogWarning($"Ignoring wrongly blended packet " +
                        $"{resultPayload.Properties.Width}x{resultPayload.Properties.Height}x{resultPayload.Properties.Format} != Setup({_setup.Width}x{_setup.Height}x{_setup.OutputPixelFormat})");
                    return false;
                }
                return true;
            }
            return false;
        }

        private (int yOffset, int yCount)[] GetSlices(VideoBlenderInputRuntime runtime, int height)
        {
            int slices = 4;
            if (height < 50)
                return null;
            else if (height < 200)
                slices = 2;
            else if (height > 1920 && Environment.ProcessorCount >= 8)
                slices = 8;

            int sliceHeight = height / slices;

            return Enumerable.Range(0, slices).Select(s => (s * sliceHeight, s == slices - 1 ? height - sliceHeight * (slices - 1) : sliceHeight)).ToArray();
        }

        private Data<Frame> GetOrCreateNoSignalFrame(VideoBlenderInputRuntime runtime)
        {
            
            int width = (int)(runtime.Description.Rect.Width * _setup.Width);
            int height = (int)(runtime.Description.Rect.Height * _setup.Height);

            if (runtime.EmptyFrame == null)
                runtime.EmptyFrame = new FixedFrame();

            runtime.EmptyFrame.Update(_setup.WeakOptions.NoSignalData, new FixedFrameConfig(width, height, runtime.Description.PixelFormat, _setup.Dx), _streamer);
            return new Data<Frame>(runtime.EmptyFrame.Frame.Instance.Item, 0, 0, null);
        }

        private bool IsSourceChanged(VideoBlenderSetup setup1, VideoBlenderSetup setup2)
        {
            if (setup1.WeakOptions.Inputs.Length != setup2.WeakOptions.Inputs.Length)
            {
                //Core.LogInfo("Count changed");
                return true;
            }

            for (int q = 0; q < setup1.WeakOptions.Inputs.Length; q++)
            {
                if (!setup1.WeakOptions.Inputs[q].Equals(setup2.WeakOptions.Inputs[q]))
                {
                    //Core.LogInfo($"{q} input changed {setup1.WeakOptions.Inputs[q]} => {setup2.WeakOptions.Inputs[q]}");
                    return true;
                }
            }

            return false;
        }

        private void Reconfigure(VideoBlenderSetup setup)
        {
            if (setup.WeakOptions.Inputs.Length == _inputRuntimes.Count &&
                setup.WeakOptions.Inputs.Select(s => s.Id).SequenceEqual(_inputRuntimes.Select(s => s.Description.Id)))
            {
                // light update
                for (int q = 0; q < _inputRuntimes.Count; q++)
                    UpdateRuntime(_inputRuntimes[q], setup.WeakOptions.Inputs[q], setup);
            }
            else
            {
                // hard update
                List<VideoBlenderInputRuntime> newRuntimes = new List<VideoBlenderInputRuntime>();
                foreach (var ic in setup.WeakOptions.Inputs)
                {
                    var old = _inputRuntimes.FirstOrDefault(s => s.Description.Id == ic.Id);
                    if (old != null)
                    {
                        _inputRuntimes.Remove(old);
                        newRuntimes.Add(old);
                        UpdateRuntime(old, ic, setup);
                    }
                    else
                    {
                        var n = new VideoBlenderInputRuntime();
                        n.StartupTime = Core.GetCurrentTime();
                        newRuntimes.Add(n);
                        UpdateRuntime(n, ic, setup);
                    }
                }
                foreach (var toRemove in _inputRuntimes)
                {
                    toRemove.Dispose(_framePool);
                }

                _inputRuntimes = newRuntimes;
            }
            _inputRuntimesSorted = _inputRuntimes.OrderBy(s => s.Description.ZOrder).ToList();
            _sourceIdOffset = setup.WeakOptions.Inputs[0].SourceId;

            _setup = setup;
        }

        private void UpdateRuntime(VideoBlenderInputRuntime runtime, VideoBlenderInputDescription desc, VideoBlenderSetup setup)
        {
            runtime.Description = desc;
            runtime.BlackScreenDelay = desc.Behavior.BlackScreenDelayMs * 10_000L;
            runtime.SameFrameDelay = desc.Behavior.SameFrameDelayMs * 10_000L;

            
            if (runtime.Frames.Any(s => !Compatible(s, setup, desc)))
            {
                runtime.Frames = new LinkedList<Data<Frame>>(runtime.Frames.Where(s => Compatible(s, setup, desc)));
            }

            var oldRef = runtime.FixedFrame;
            runtime.FixedFrame = desc.FixedFrame?.AddRef();
            oldRef?.RemoveRef();
        }

        private bool Compatible(Data<Frame> s, VideoBlenderSetup setup, VideoBlenderInputDescription desc)
        {
            if (setup.Dx != null)
                return (s.Payload.DirectXResourceRef != null);
            else
                return (s.Payload.DirectXResourceRef == null && s.Payload.Properties.Format == desc.PixelFormat);
        }

        private long ToTicks(long v) => v * _fps / 10_000_000;

        private long ToTime(long v) => v * 10_000_000 / _fps;
    }

    public record BlendingTypeDescriptor(string ShaderFile, string VetextShaderFunction, string PixelShaderFunction);

    public class VideoBlenderInputRuntime
    {
        public VideoBlenderInputDescription Description { get; set; }

        public LinkedList<Data<Frame>> Frames { get; set; } = new LinkedList<Data<Frame>>();

        public FixedFrame EmptyFrame { get; set; }

        public RefCountedFrame FixedFrame { get; set; }

        public DirectXFilterRuntime DirectXFilterRuntime { get; } = new DirectXFilterRuntime();

        public VertexBuffer VertexBuffer { get; set; }

        public long SameFrameDelay { get; set; }

        public long BlackScreenDelay { get; set; }

        public int SlicesHeight { get; set; } = -1;

        public int Tension { get; set; }

        public (int yOffset, int yCount)[] Slices { get; set; }

        public long StartupTime { get; internal set; }

        public void Dispose(PayloadPool<Frame> framePool)
        {
            foreach (var frame in Frames)
                framePool.Back(frame.Payload);

            FixedFrame?.RemoveRef();

            EmptyFrame?.Dispose();

            DirectXFilterRuntime?.Dispose();

            VertexBuffer?.Dispose();
        }
    }
}
