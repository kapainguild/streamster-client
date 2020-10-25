using DirectShowLib;
using Serilog;
using Streamster.ClientCore;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Streamster.ClientApp.Win.Services
{
    class LocalVideoSourceManager : ILocalVideoSourceManager
    {
        public const int PreviewFps = 5;
        private readonly LocalSettingsService _localSettingsService;
        private Action<IVideoSource> _videoSourceChanged;
        private Action<IVideoSource, VideoInputPreview> _previewAvailable;

        private List<VideoSource> _videoSources = new List<VideoSource>();
        

        private string _runningSourceId = null;

        private Task _currentObservation = Task.CompletedTask;

        private Task<IVideoSource[]> _retrieveVideoSourceListTask;

        public LocalVideoSourceManager(LocalSettingsService localSettingsService)
        {
            _videoSourceChanged = (d) => { };
            _previewAvailable = (d, p) => { };
            _localSettingsService = localSettingsService;
        }

        public void Start(Action<IVideoSource> videoSourceChanged, Action<IVideoSource, VideoInputPreview> previewAvailable)
        {
            Log.Information($"Starting VideoSourceManager ({_localSettingsService.Settings.EnableVideoPreview}, {_localSettingsService.Settings.DisableCameraStatusCheck})");
            _videoSourceChanged = videoSourceChanged;
            _previewAvailable = previewAvailable;

            List<VideoSource> snapshot;
            lock (this)
            {
                snapshot = _videoSources.ToList();
            }

            snapshot.ForEach(s => _videoSourceChanged(s));
        }

        public void SetRunningSource(string videoSourceId)
        {
            var changed = new List<VideoSource>();
            lock(this)
            {
                if (_runningSourceId != videoSourceId)
                {
                    var o = _videoSources.FirstOrDefault(s => s.Id == _runningSourceId);
                    if (o != null)
                    {
                        changed.Add(o);
                        o.State = InputState.Unknown;
                    }

                    var n = _videoSources.FirstOrDefault(s => s.Id == videoSourceId);
                    if (n != null)
                    {
                        changed.Add(n);
                        n.State = InputState.Running;
                    }

                    _runningSourceId = videoSourceId;
                }
            }

            changed.ForEach(s => _videoSourceChanged(s));
        }


        public void StartObservation()
        {
            List<VideoSource> snapshot;
            lock (this)
            {
                _videoSources.ForEach(s => s.State = InputState.Unknown);
                snapshot = _videoSources.ToList();
            }
            snapshot.ForEach(s => _videoSourceChanged(s));

            _ = StartObservationAync();
        }


        public void StopObservation()
        {
            _ = StopObservationAsync();
        }

        private async Task StopObservationAsync()
        {
            lock(this)
            {
                _videoSources.ForEach(s => s.FullUpdateTask?.CancellationTokenSource?.Cancel());
            }
            await Task.WhenAny(_currentObservation, Task.Delay(2500));
        }

        private async Task StartObservationAync()
        {
            await StopObservationAsync();
            var sources = (await RetrieveSourcesListAsync()).OfType<VideoSource>().ToList();

            _currentObservation = Task.WhenAll(sources.Select(s => UpdateVideoSourceAsync(s, true)));
            await _currentObservation;
        }

        public async Task<IVideoSource[]> RetrieveSourcesListAsync()
        {
            Task<IVideoSource[]> current = null;
            lock(this)
            {
                if (_retrieveVideoSourceListTask == null)
                    _retrieveVideoSourceListTask = Task.Run(() => RetrieveVideoSourceList());
                current = _retrieveVideoSourceListTask;
            }
            try
            {
                return await current;
            }
            finally
            {
                lock (this)
                {
                    _retrieveVideoSourceListTask = null;
                }
            }
        }

        private IVideoSource[] RetrieveVideoSourceList()
        {
            var devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            //var devices = new DsDevice[0];

            List<VideoSource> snapshot = null;

            lock (this)
            {
                var newDevices = devices.Where(s => !_videoSources.Any(r => r.Id == s.DevicePath)).Select(s => new VideoSource
                {
                    Name = s.Name,
                    Id = s.DevicePath,
                    DsDevice = s,
                    Type = s.DevicePath.ToLowerInvariant().Contains("?\\usb#") ? InputType.USB : InputType.Virtual
                }).ToList();
                var existingDevices = _videoSources.Where(s => devices.Any(r => r.DevicePath == s.Id)).ToList();
                var removedDevices = _videoSources.Where(s => !devices.Any(r => r.DevicePath == s.Id)).ToList();

                _videoSources.AddRange(newDevices);
                removedDevices.ForEach(d => d.State = InputState.Removed);
                existingDevices.Concat(newDevices).ToList().ForEach(d => d.State = InputState.Unknown);

                snapshot = _videoSources.ToList();
            }
            snapshot.ForEach(s => _videoSourceChanged(s));
            return snapshot.OfType<IVideoSource>().ToArray();
        }


        public async Task<IVideoSource> GetUpdatedVideoSourceAsync(string videoSourceId)
        {
            VideoSource source = null;

            lock(this)
            {
                source = _videoSources.FirstOrDefault(s => s.Id == videoSourceId);
            }

            if (await UpdateVideoSourceAsync(source, false))
                return source;

            return null;
        }

        private async Task<bool> UpdateVideoSourceAsync(VideoSource source, bool getFrames)
        {
            Task updateTask = Task.CompletedTask;
            bool taskSet = false;

            lock (this)
            {
                if (source == null || source.State == InputState.Removed)
                    return false;

                if (source.FullUpdateTask != null)
                {
                    updateTask = source.FullUpdateTask.Task;
                }
                else if ((DateTime.UtcNow - source.LastUpdateTime).TotalSeconds > 5 || getFrames)
                {
                    source.FullUpdateTask = new TaskEntry(null, null);
                    source.FullUpdateTask.CancellationTokenSource = new CancellationTokenSource();

                    source.FullUpdateTask.Task = UpdateVideoSourceAsync(source, getFrames, source.FullUpdateTask.CancellationTokenSource.Token);
                    updateTask = source.FullUpdateTask.Task;
                    taskSet = true;
                }
                else
                    updateTask = Task.CompletedTask;
            }

            try
            {
                await updateTask;
            }
            finally
            {
                if (taskSet)
                {
                    lock (this)
                    {
                        source.FullUpdateTask = null;
                    }
                }
            }
            return true;
        }

        private async Task UpdateVideoSourceAsync(VideoSource source, bool getFrames, CancellationToken cancellationToken)
        {
            await TaskHelper.GoToPool().ConfigureAwait(false);
            if (source.Capabilities == null)
            {
                Log.Information($"Update ({source.Name}): Getting caps");
                source.InternalCapabilities = GetVideoSourceCaps(source.DsDevice);
                source.Capabilities = source.InternalCapabilities.Select(s => s.GetClone()).ToArray();
            }

            Log.Information($"Update ({source.Name}): Starting");
            if (source.Id == _runningSourceId)
            {
                SetState(source, InputState.Running);
            }
            else if (IsBlackListed(source)/* || !device.Name.ToLower().Contains("integ")*/)
            {
                SetState(source, InputState.Ready);
            }
            else
            {
                if (ObsHelper.IsObsAudioVideoAndItIsOff(source.Name))
                {
                    SetState(source, InputState.ObsIsNotStarted);
                    return;
                }

                if (_localSettingsService.Settings.DisableCameraStatusCheck)
                {
                    SetState(source, InputState.Ready);
                }
                else
                {
                    await GetVideoSourceStateAndFramesAsync(source, getFrames, cancellationToken);
                }
            }
                
        }

        public static void AddCaptureFilter(IFilterGraph2 filterGraph, DsDevice dsDevice, out IBaseFilter baseFilter)
        {
            Log.Information($"Binding {dsDevice.Name}");
            var iid = new Guid("56a86895-0ad4-11ce-b03a-0020af0ba770");
            dsDevice.Mon.BindToObject(null, null, ref iid, out var rawFilter);
            baseFilter = (IBaseFilter)rawFilter;
            var local = baseFilter;
            Checked(() => filterGraph.AddFilter(local, "captureFilter"), "AddFilter", dsDevice);
        }

        private async Task GetVideoSourceStateAndFramesAsync(VideoSource source, bool getFrames, CancellationToken cancellationToken)
        {
            bool videoPreviewEnabled = _localSettingsService.Settings.EnableVideoPreview;

            Log.Information($"Update ({source.Name}): Retrieving (Preview={videoPreviewEnabled}, Frames={getFrames})");

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            ICaptureGraphBuilder2 captureGraphBuilder = null;
            IMediaControl mediaControl = null;

            IBaseFilter sourceFilter = null;
            ISampleGrabber grabber = null;
            IBaseFilter nullRenderer = null;
            int hr = 0;

            try
            {
                nullRenderer = (IBaseFilter)new NullRenderer();
            }
            catch(Exception e)
            {
                Log.Warning(e, "Something wrong with DShow");
                SetState(source, InputState.Ready);
                return;
            }
            
            try
            {
                var filterGraph = new FilterGraph() as IFilterGraph2;
                mediaControl = (IMediaControl)filterGraph;

                captureGraphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
                Checked(() => captureGraphBuilder.SetFiltergraph(filterGraph), "SetFiltergraph", null);

                //setup source
                AddCaptureFilter(filterGraph, source.DsDevice, out sourceFilter);

                if (getFrames && videoPreviewEnabled)
                    SetCapabilities(source, sourceFilter);

                //setup null renderer
                Checked(() => filterGraph.AddFilter(nullRenderer, "null"), "AddNullRenderer", null);

                if (videoPreviewEnabled)
                {
                    //setup gabber
                    grabber = (ISampleGrabber)new SampleGrabber();
                    var mediaType = new AMMediaType
                    {
                        majorType = MediaType.Video,
                        subType = MediaSubType.RGB24,
                        formatType = FormatType.VideoInfo
                    };
                    Checked(() => grabber.SetMediaType(mediaType), "Grabber.SetMediaType", source.DsDevice);
                    DsUtils.FreeAMMediaType(mediaType);

                    hr = grabber.SetCallback(new LocalVideoSourceGrabber(source.Name, getFrames, grabber,
                        (b) =>
                        {
                            if (getFrames)
                            {
                                SetState(source, InputState.Ready, source.State != InputState.Ready);
                                source.Preview = b;
                                _previewAvailable(source, b);
                            }
                            else
                                SetState(source, InputState.Ready);
                            Task.Run(() => tcs.TrySetResult(true));
                        }), 0);

                    Checked(() => hr, "SetCallback", null);
                    Checked(() => filterGraph.AddFilter((IBaseFilter)grabber, "grabber"), "AddGrabber", null);
                }
                Log.Information($"Update ({source.Name}): Rendering");
                hr = captureGraphBuilder.RenderStream(PinCategory.Capture, MediaType.Video, sourceFilter, (IBaseFilter)grabber, nullRenderer);
                Marshal.ReleaseComObject(captureGraphBuilder);
                captureGraphBuilder = null;
                Checked(() => hr, "RenderStream", null);

                Log.Information($"Update ({source.Name}): Rendered");

                var res = mediaControl.Run();
                Log.Information($"Update ({source.Name}): Ran");

                if (res == 1)
                    res = mediaControl.GetState(0, out var state);

                if (res != 0)
                    SetState(source, InputState.InUseByOtherApp);
                else
                {
                    if (videoPreviewEnabled)
                    {
                        if (await Task.WhenAny(tcs.Task, Task.Delay(6_000, cancellationToken)) != tcs.Task)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            SetState(source, InputState.Failed);
                            Log.Warning($"Source failed '{source.Name}'");
                        }
                        else
                        {
                            SetState(source, InputState.Ready);
                            if (getFrames)
                            {
                                WaitHandle.WaitAny(new[] { cancellationToken.WaitHandle });
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }
                    }
                    else
                    {
                        SetState(source, InputState.Ready);
                    }
                }
            }
            catch (Exception e) when (e is TaskCanceledException || e is OperationCanceledException)
            {
                Log.Information($"Update ({ source.Name}): Cancelled");
            }
            catch (Exception e)
            {
                SetState(source, InputState.Failed);
                Log.Error(e, $"Update ({source.Name}): Failed");
            }

            Log.Information($"Update ({source.Name}): Releasing");
            try
            {
                mediaControl?.Stop();
                ReleaseComObject(captureGraphBuilder);
                ReleaseComObject(sourceFilter);
                ReleaseComObject(captureGraphBuilder);
                ReleaseComObject(mediaControl);
                ReleaseComObject(grabber);
                ReleaseComObject(nullRenderer);
            }
            catch(Exception e)
            {
                Log.Error(e, $"ReleaseComObject('{source.Name}') failed");
            }
            Log.Information($"Update ({source.Name}): Done");
        }

        private void SetCapabilities(VideoSource source, IBaseFilter sourceFilter)
        {
            try
            {
                var cap = FindBestPreviewCapability(source.InternalCapabilities);
                if (cap == null)
                    return;

                Log.Information($"'{cap}' for Previewing '{source.Name}'");
                object pin = DsFindPin.ByCategory(sourceFilter, PinCategory.Capture, 0);

                if (pin == null)
                    pin = sourceFilter;

                if (pin is IAMStreamConfig streamConfig)
                {
                    GetMediaTypeInfo(cap.MediaType, out var h, out var w, out _, out var v, out var v2);

                    if (v != null)
                    {
                        TrySetFps(ref v.AvgTimePerFrame, cap, 5);
                        Marshal.StructureToPtr(v, cap.MediaType.formatPtr, false);
                    }
                    else if (v2 != null)
                    {
                        TrySetFps(ref v2.AvgTimePerFrame, cap, 5);
                        Marshal.StructureToPtr(v2, cap.MediaType.formatPtr, false);
                    }
                    
                    Checked(() => streamConfig.SetFormat(cap.MediaType), "SetFormat", source.DsDevice);
                }
                else
                    throw new InvalidOperationException("IAMStreamConfig not found");
            }
            catch (Exception e)
            {
                Log.Warning(e, $"SetCapabilities failed for '{source.Name}'");
            }
        }

        private void TrySetFps(ref long avgTimePerFrame, VideoSourceCapability cap, int fps)
        {
            if (cap.MinF > PreviewFps)
            {
                avgTimePerFrame = 10000000 / cap.MinF;
            }
            else if (PreviewFps > cap.MaxF)
            {
                avgTimePerFrame = 10000000 / cap.MaxF;
            }
            else
            {
                avgTimePerFrame = 10000000 / PreviewFps;
            }
        }

        private VideoSourceCapability FindBestPreviewCapability(VideoSourceCapability[] caps)
        {
            double ratio = 16d / 9d;
            double dratio = 1d / 9d;

            int minWidth = 420;

            var shortlist = caps.Where(s => s.Fmt == VideoInputCapabilityFormat.Raw).ToList();
            if (shortlist.Count == 0)
                shortlist = caps.Where(s => s.Fmt == VideoInputCapabilityFormat.Empty).ToList();

            shortlist = shortlist.Where(s => s.W>= minWidth).ToList();

            shortlist = shortlist.OrderBy(s => s.W).Where(s => (double)s.W / s.H > ratio - dratio && (double)s.W / s.H < ratio + dratio).ToList();

            if (shortlist.Count > 0)
            {
                var inFps = shortlist.FirstOrDefault(s => s.MinF <= PreviewFps && PreviewFps <= s.MaxF);

                if (inFps == null)
                {
                    var smallRes = shortlist.First();
                    shortlist = shortlist.Where(s => s.W == smallRes.W && s.H == smallRes.H).ToList();
                    int minFps = shortlist.Min(s => s.MinF);
                    return shortlist.FirstOrDefault(s => s.MinF == minFps);
                }
                return inFps;
            }
            return null;
        }

        public static void ReleaseComObject(object obj)
        {
            if (obj != null)
                Marshal.ReleaseComObject(obj);
        }

        private bool IsBlackListed(VideoSource source)
        {
            if (source.Name.ToLowerInvariant() == "lovense smartcam") // TODO: remove one day as it seems works fine already
                return true;
            return false;
        }

        private void SetState(VideoSource source, InputState state, bool log = true)
        {
            source.LastUpdateTime = DateTime.UtcNow;
            source.State = state;
            if (log)
                Log.Information($"Update ({source.Name}): '{state}'");
            _videoSourceChanged(source);
        }

        private VideoSourceCapability[] GetVideoSourceCaps(DsDevice device)
        {
            var list = new List<VideoSourceCapability>();
            IntPtr pCaps = IntPtr.Zero;

            IFilterGraph2 filterGraph2 = null;
            IBaseFilter sourceFilter = null;
            IAMStreamConfig streamConfig = null;
            object pin = null;
            try
            {
                filterGraph2 = new FilterGraph() as IFilterGraph2;
                if (filterGraph2 == null) 
                    throw new NotSupportedException("filter2 is null");

                LocalVideoSourceManager.AddCaptureFilter(filterGraph2, device, out sourceFilter);

                pin = DsFindPin.ByCategory(sourceFilter, PinCategory.Capture, 0);

                if (pin == null)
                    pin = sourceFilter;

                streamConfig = pin as IAMStreamConfig;
                if (streamConfig == null) 
                    throw new NotSupportedException("pin is null");

                int count = 0;
                int size = 0;
                Checked(() => streamConfig.GetNumberOfCapabilities(out count, out size), "GetNumberOfCapabilities", null);

                if (count <= 0)
                    throw new NotSupportedException("This video source does not report capabilities.");
                if (size != Marshal.SizeOf(typeof(VideoStreamConfigCaps)))
                    throw new NotSupportedException("Unable to retrieve video source capabilities. This video source requires a larger VideoStreamConfigCaps structure.");

                // Alloc memory for structure
                pCaps = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(VideoStreamConfigCaps)));

                for (int i = 0; i < count; i++)
                {
                    AMMediaType mediaType = null;
                    Checked(() => streamConfig.GetStreamCaps(i, out mediaType, pCaps), "GetStreamCaps", null);

                    VideoStreamConfigCaps caps = (VideoStreamConfigCaps)Marshal.PtrToStructure(pCaps, typeof(VideoStreamConfigCaps));

                    var format = GetMediaTypeInfo(mediaType, out var height, out var width, out var compression, out var videoInfoHeader, out var videoInfoHeader2);

                    var result = new VideoSourceCapability()
                    {
                        MaxF = GetFps(caps.MinFrameInterval),
                        MinF = GetFps(caps.MaxFrameInterval),
                        Fmt = format,
                        Compression = compression,
                        W = width,
                        H = height,
                        MediaType = mediaType
                    };

                    list.Add(result);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"Error during retreiving caps for '{device.Name}'");
            }
            finally
            {
                if (pCaps != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pCaps);
            }

            try
            {
                ReleaseComObject(sourceFilter);
                ReleaseComObject(filterGraph2);
                ReleaseComObject(streamConfig);
                ReleaseComObject(pin);
            }
            catch (Exception e)
            {
                Log.Error(e, $"ReleaseComObject('{device.Name}') failed");
            }

            Log.Information($"Caps {device.Name}: {string.Join("; ", list.Select(s => s.ToString()))}");

            return list.ToArray();
        }

        public static VideoInputCapabilityFormat GetMediaTypeInfo(AMMediaType mediaType, out int height, out int width, out int compression, out VideoInfoHeader v, out VideoInfoHeader2 v2)
        {
            compression = -1;
            v = null;
            v2 = null;
            if (mediaType.formatType == FormatType.VideoInfo)
            {
                v = new VideoInfoHeader();
                Marshal.PtrToStructure(mediaType.formatPtr, v);
                height = v.BmiHeader.Height;
                width = v.BmiHeader.Width;
                compression = v.BmiHeader.Compression;
            }
            else if (mediaType.formatType == FormatType.VideoInfo2)
            {
                v2 = new VideoInfoHeader2();
                Marshal.PtrToStructure(mediaType.formatPtr, v2);

                height = v2.BmiHeader.Height;
                width = v2.BmiHeader.Width;
                compression = v2.BmiHeader.Compression;
            }
            else
                throw new InvalidOperationException($"Invalid media type FormatType={mediaType}");

            switch (compression)
            {
                case 0x47504A4D: return VideoInputCapabilityFormat.MJpeg; 
                case 0x32595559: return VideoInputCapabilityFormat.Raw;
                case 0x34363248: return VideoInputCapabilityFormat.H264;
                case 0x0: return VideoInputCapabilityFormat.Raw;
                default: return VideoInputCapabilityFormat.Unknown; 
            }
        }

        public static void Checked(Func<int> action, string name, DsDevice device)
        {
            if (device != null)
                Log.Information($"In{name}({device.Name})");
            int hr = action();
            if (device != null)
                Log.Information($"Out{name}({device.Name})");
            if (hr < 0)
            {
                var error = DsError.GetErrorText(hr);
                throw new InvalidOperationException($"{name} failed. {error} ({hr})");
            }
        }

        public static int GetFps(long interval)
        {
            if (interval == 0) return 30;
            return (int)((double)10000000 / interval);
        }
    }
    
    class PreviewData
    {
        public byte[] Bytes { get; set; }

        public int Weight { get; set; }
    }


    class VideoSource : IVideoSource
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public InputState State { get; set; }

        public VideoInputCapability[] Capabilities { get; set; }

        public VideoSourceCapability[] InternalCapabilities { get; set; }

        public VideoInputPreview Preview { get; set; }

        public DsDevice DsDevice { get; set; }

        public InputType Type { get; set; }

        public DateTime LastUpdateTime { get; set; } = new DateTime(1970, 01, 01, 0, 0, 0, DateTimeKind.Utc);

        public TaskEntry FullUpdateTask { get; set; }
    }

    class VideoSourceCapability : VideoInputCapability
    {
        public int Compression { get; set; }

        public AMMediaType MediaType { get; set; }

        public override string ToString()
        {
            return $"{GetCompressionStr(Compression)}.{W}x{H}x{MinF}-{MaxF}";
        }

        private string GetCompressionStr(int compression)
        {
            switch (compression)
            {
                case 0x47504A4D: return "J";
                case 0x32595559: return "R";
                case -1: return "-";
                default: return compression.ToString();
            }
        }
    }
}
