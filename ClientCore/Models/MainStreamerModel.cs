using DeltaModel;
using DynamicStreamer;
using DynamicStreamer.Contexts;
using DynamicStreamer.Extensions;
using DynamicStreamer.Extensions.DesktopAudio;
using DynamicStreamer.Extensions.ScreenCapture;
using DynamicStreamer.Extensions.WebBrowser;
using DynamicStreamer.Nodes;
using DynamicStreamer.Queues;
using DynamicStreamer.Screen;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Services;
using Streamster.ClientCore.Support;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class MainStreamerModel : IAsyncDisposable
    {
        public const string OutputNameRecording = "Recording";
        public const string OutputNameStreamToCloud = "ToCloud";
        public static string StatStreamToCloud = $"X{OutputNameStreamToCloud}.";
        private readonly CoreData _coreData;
        private readonly LocalSourcesModel _localSources;
        private readonly ResourceService _resourceService;
        private readonly IAppEnvironment _appEnvironment;
        private readonly IWindowStateManager _windowStateManager;
        private readonly AudioModel _audioModel;
        private readonly IAppResources _appResource;
        private readonly StreamingSourcesModel _streamingSourcesModel;
        private readonly MainVpnModel _mainVpnModel;
        private readonly StreamerHealthCheck _healthCheck;
        private HardwareEncoderCheck _hardwareEncoderCheck;
        private ClientStreamer _mainStreamer;
        private ClientStreamer _receiverStreamer;

        private string _currentRecordingPath;
        
        private StreamerRebuildContext _lastRebuildContext;

        private StreamerCachingLogger _streamerLogger = new StreamerCachingLogger("**");

        public ScreenRendererModel ScreenRenderer { get; }


        public MainStreamerModel(CoreData coreData, 
            ScreenRendererModel screenRenderer, 
            LocalSourcesModel sources,
            ResourceService resourceService,
            IAppEnvironment appEnvironment,
            IWindowStateManager windowStateManager,
            AudioModel audioModel,
            IAppResources appResource,
            StreamingSourcesModel streamingSourcesModel,
            MainVpnModel mainVpnModel)
        {
            _coreData = coreData;
            _localSources = sources;
            _resourceService = resourceService;
            _appEnvironment = appEnvironment;
            _windowStateManager = windowStateManager;
            _audioModel = audioModel;
            _appResource = appResource;
            _streamingSourcesModel = streamingSourcesModel;
            _mainVpnModel = mainVpnModel;
            ScreenRenderer = screenRenderer;

            _healthCheck = new StreamerHealthCheck(coreData, streamingSourcesModel);

            Core.InitOnMain();
        }

        public void Prepare()
        {
            // this also loads all ffmpeg dlls (av-)
            Core.Init((severity, pattern, message, exception) =>
            {
                //if (message.Contains("Queue input is backward in time"))
                //{
                //    int q = 0;
                //}
                _streamerLogger.Write(severity, pattern, message, exception);
            }, (s, st, id) => new OutputContext());

            _hardwareEncoderCheck = new HardwareEncoderCheck();
            _hardwareEncoderCheck.Start();
        }

        internal void Start()
        {
            if (_coreData.Settings.StreamingToCloud == StreamingToCloudBehavior.AppStart)
                _coreData.Settings.StreamingToCloudStarted = true;

            _coreData.Subscriptions.SubscribeForProperties<IChannel>(s => s.IsOn, (i, c, p) =>
            {
                if (_coreData.Settings.StreamingToCloud == StreamingToCloudBehavior.FirstChannel && i.IsOn && _coreData.Root.Channels.Values.Count(e => e.IsOn) == 1)
                    _coreData.Settings.StreamingToCloudStarted = true;
            });

            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.Resolution, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.Fps, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.Bitrate, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.EncoderType, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.EncoderQuality, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.StreamingToCloudStarted, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.NoStreamWithoutVpn, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.IsRecordingRequested, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.SelectedScene, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.PreferNalHdr, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.DisableQsvNv12Optimization, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.RecordingFormat, RefreshStreamer);

            _coreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(s => s.RendererType, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(s => s.BlenderType, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<IDeviceSettings>(s => s.RendererAdapter, RefreshStreamer);

            _coreData.Subscriptions.SubscribeForAnyProperty<ISceneItem>((i, c, _, _) => RefreshStreamer());
            _coreData.Subscriptions.SubscribeForAnyProperty<ISceneAudio>((i, c, _, _) => RefreshStreamer());

            _coreData.Subscriptions.SubscribeForProperties<IIngest>(s => s.Data, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.AssignedOutgest, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.VpnState, RefreshStreamer);
            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.VpnServerIpAddress, RefreshStreamer);

            ScreenRenderer.OnChanged = () => RefreshStreamer();

            RefreshStreamer();

            TaskHelper.RunUnawaited(CollectStatisticsRoutine(), "CollectStatistics");
        }

        private void RefreshStreamer<T>(T i, ChangeType c, string p) => RefreshStreamer();

        public void RefreshStreamer()
        {
            bool doIStream = _streamingSourcesModel.TryGetCurrentScene(out var scene) && _streamingSourcesModel.IsMySceneSelected();
            // shutdown
            if (doIStream)
            {
                if (_coreData.ThisDevice.RequireOutgest)
                {
                    Log.Information("Withdrawing outgest");
                    _coreData.ThisDevice.RequireOutgest = false;
                }

                if (_receiverStreamer != null)
                {
                    ShutdownStreamer(_receiverStreamer, "receiving streamer");
                    _receiverStreamer = null;
                }
            }
            else
            {
                
                if (_mainStreamer != null)
                {
                    ShutdownStreamer(_mainStreamer, "main streamer");
                    _mainStreamer = null;
                }
            }

            // start
            if (doIStream)
            {
                if (_mainStreamer == null)
                    _mainStreamer = new ClientStreamer("main", _hardwareEncoderCheck);

                _mainStreamer.StartUpdate(RebuildMainStreamerConfig(scene, _mainStreamer.GetDxFailureCounter()));
            }
            else
            {
                if (!_coreData.ThisDevice.RequireOutgest)
                {
                    Log.Information("Requesting outgest");
                    //_coreData.ThisDevice.RequireOutgestType = RequireOutgestType.Rtmp;
                    _coreData.ThisDevice.RequireOutgestType = RequireOutgestType.Tcp;
                    _coreData.ThisDevice.RequireOutgest = true;
                }

                if (_receiverStreamer == null)
                    _receiverStreamer = new ClientStreamer("receiver", _hardwareEncoderCheck);

                _receiverStreamer.StartUpdate(GetReceiverStreamerConfig(_receiverStreamer.GetDxFailureCounter()));
            }
        }

        private void ShutdownStreamer(ClientStreamer streamer, string info)
        {
            Log.Information($"Shutting down: {info}");
            streamer.BlockingUpdate(new ClientStreamerConfig(null, null, null, null, null, null, 0.0, Disposing: true));
            streamer.Dispose();
            streamer.StopFrameProcessing();
            Log.Information($"Shut down: {info}");
        }

        private ClientStreamerConfig GetReceiverStreamerConfig(int dxFailureCounter)
        {
            var outgestId = _coreData.ThisDevice.AssignedOutgest;
            VideoInputTrunkConfig[] inputs = new VideoInputTrunkConfig[0];
            if (outgestId != null && _coreData.Root.Outgests.TryGetValue(outgestId, out var outgest))
            {
                var outgestUrl = GetIngestOutgestUrl(outgest.Data.Output);
                //outgestUrl = outgestUrl.Replace("60", "66");
                inputs = new[] { new VideoInputTrunkConfig("0", new VideoInputConfigFull(
                    new InputSetup(outgest.Data.Type, outgestUrl, outgest.Data.Options, null, null, null, null, 1, AdjustInputType.None, true, false)), 
                    null, PositionRect.Full, PositionRect.Full, true, 0) };
            }

            return new ClientStreamerConfig(
                    inputs,
                    new AudioInputTrunkConfig[0],
                    RebuildVideoEncoder(0, true),
                    RebuildVideoRenderOptions(dxFailureCounter),
                    new AudioEncoderTrunkConfig(0, 0, 0.0, null),
                    new OutputTrunkConfig[0],
                    BitrateDrcRatio: 1.0,
                    Disposing: false
                );
        }

        private ClientStreamerConfig RebuildMainStreamerConfig(IScene scene, int dxFailureCounter)
        {
            int bitrate = _coreData.Settings.Bitrate;
            int audioBitrate = Math.Max(Math.Min(bitrate / 14, 224), 50); // 50..224
            int videoBitrate = bitrate - audioBitrate;

            var rebuildContext = new StreamerRebuildContext();

            var result = new ClientStreamerConfig
            (
                scene.Items.Where(s => s.Value.Source != null).Select(s => RebuildSceneVideo(s.Key, s.Value, rebuildContext)).ToArray(),
                scene.Audios.Select(s => RebuildSceneAudio(s.Key, s.Value, rebuildContext)).Where(s => s != null).ToArray(),

                RebuildVideoEncoder(videoBitrate, false),
                RebuildVideoRenderOptions(dxFailureCounter), 
                RebuildAudioEncoder(audioBitrate),
                new[] { RebuildStreamingOutput(), RebuildRecordingOutput() }.Where(s => s != null).ToArray(),

                BitrateDrcRatio: 1.0,
                Disposing: false
            );

            _lastRebuildContext = rebuildContext;
            return result;
        }

        private OutputTrunkConfig RebuildStreamingOutput()
        {
            var ingest = _coreData.Root.Ingests.Values.FirstOrDefault(s => s.Owner == _coreData.ThisDeviceId);
            bool startStream = ShouldStartStream();

            if (startStream && ingest != null)
            {
                var ingestUrl = GetIngestOutgestUrl(ingest.Data.Output);

                return new OutputTrunkConfig(OutputNameStreamToCloud, new OutputSetup { 
                    Type = ingest.Data.Type, 
                    Output = ingestUrl, 
                    Options = ingest.Data.Options,
                    TimeoutMs = 20000, // 20 seconds before reopen
                }, true);
            }
            return null;
        }

        private OutputTrunkConfig RebuildRecordingOutput()
        {
            if (_coreData.Settings.IsRecordingRequested)
            {
                if (MainSettingsModel.IsValidRecordingPath(_coreData.ThisDevice.DeviceSettings.RecordingsPath))
                {
                    var now = DateTime.Now;
                    string format = _coreData.Settings.RecordingFormat == RecordingFormat.Flv ? "flv" : "mp4";
                    _currentRecordingPath ??= Path.Combine(_coreData.ThisDevice.DeviceSettings.RecordingsPath, now.ToString("yyy_MM_dd__HH_mm_ss") + $".{format}");
                    string output = _currentRecordingPath;
                    return new OutputTrunkConfig(OutputNameRecording, new OutputSetup
                    {
                        Type = format,
                        Output = output,
                        TimeoutMs = 0, // no timeout check
                        Options = ""
                    }, false);
                }
                else Log.Warning($"Bad folder for recorrding: {_coreData.ThisDevice.DeviceSettings.RecordingsPath}");
            }
            else
                _currentRecordingPath = null;
            return null;
        }

        private AudioEncoderTrunkConfig RebuildAudioEncoder(int audioBitrate)
        {
            return new AudioEncoderTrunkConfig(audioBitrate, 44100, 0.0, d => OnAudioFrame(null, d));
        }

        private VideoRenderOptions RebuildVideoRenderOptions(int dxFailureCounter)
        {
            return new VideoRenderOptions(ModelToStreamerTranslator.Translate(_coreData.ThisDevice.DeviceSettings.RendererType), _coreData.ThisDevice.DeviceSettings.RendererAdapter, _windowStateManager.WindowHandle, false, dxFailureCounter);
        }

        private VideoEncoderTrunkConfig RebuildVideoEncoder(int videoBitrate, bool receiverMode)
        {
            Action<FrameOutputData> onUiFrame = null;
            if (ScreenRenderer.IsEnabled.Value)
                onUiFrame = ScreenRenderer.OnFrame;

            var settings = _coreData.Settings;
            var resolution = settings.Resolution;
            return new VideoEncoderTrunkConfig(receiverMode, new EncoderSpec { width = resolution.Width, height = resolution.Height, Quality = ModelToStreamerTranslator.Translate(settings.EncoderQuality) },
                                                ModelToStreamerTranslator.Translate(settings.EncoderType),
                                                ModelToStreamerTranslator.Translate(settings.EncoderQuality),
                                                settings.PreferNalHdr,
                                                !settings.DisableQsvNv12Optimization,
                                                videoBitrate,
                                                settings.Fps,
                                                ModelToStreamerTranslator.Translate(_coreData.ThisDevice.DeviceSettings.BlenderType), 
                                                null, //CreateVideoFilter(VideoFilterAll.Value), - no global filter supported so far
                                                onUiFrame,
                                                new FixedFrameData(nameof(AppData.CanvasBackground), _appResource.AppData.CanvasBackground, SingleFrameType.Png),
                                                new FixedFrameData(nameof(StaticResources.NoSignal), StaticResources.NoSignal, SingleFrameType.Png));
        }

        private AudioInputTrunkConfig RebuildSceneAudio(string id, ISceneAudio s, StreamerRebuildContext rebuildContext)
        {
            rebuildContext.SetAudioSource(id, s.Source);
            var level = s.Muted ? -1000.0 : s.Volume;

            if (s.Source == null)
            {
                rebuildContext.AddAudio(id, InputIssueDesc.NoAudioSelected);
                Log.Warning($"Audio source is null for '{id}'");
                return null;
            }
            else if (s.Source.DesktopAudio)
            {
                return new AudioInputTrunkConfig(id, new InputSetup(DesktopAudioContext.Name, String.Empty), level, f => OnAudioFrame(id, f));
            }
            else if (s.Source.DeviceName != null)
            {
                var device = _localSources.GetLocalAudioDevice(s.Source.DeviceName);
                if (device != null)
                {
                    var opts = DShowOptionsSelector.GetAudioOptions(device);
                    return new AudioInputTrunkConfig(id, new InputSetup("dshow", $"audio={DShowOptionsSelector.GetDeviceName(device)}", opts), level, f => OnAudioFrame(id, f));
                }
                else
                {
                    rebuildContext.AddAudio(id, InputIssueDesc.AudioRemoved);
                    Log.Warning($"Audio device not found for '{id}'-{s.Source.DeviceName}");
                    return null;
                }
            }
            else
            {
                rebuildContext.AddAudio(id, InputIssueDesc.NoAudioSelected);
                Log.Warning($"Audio source is not specified for '{id}'");
                return null;
            }
        }

        private void OnAudioFrame(string id, FrameOutputData data)
        {
            _audioModel.OnAudioFrame(id, data);
            data.Frame.Dispose();
        }

        private VideoInputTrunkConfig RebuildSceneVideo(string id, ISceneItem s, StreamerRebuildContext rebuildContext)
        {
            var input = RebuildInputSource(id, s.Source, s, rebuildContext);

            return new VideoInputTrunkConfig(id, input, RebuildFilters(s.Filters), ModelToStreamerTranslator.Translate(s.Rect), AdjustPtzHFlip(ModelToStreamerTranslator.Translate(s.Ptz), s.Filters), s.Visible, s.ZOrder);
        }

        private PositionRect AdjustPtzHFlip(PositionRect ptz, SceneItemFilters filters)
        {
            if (filters?.Filters != null && filters.Filters.Any(s => s.Type == SceneItemFilterType.HFlip))
                return new PositionRect(1 - ptz.Width - ptz.Left, ptz.Top, ptz.Width, ptz.Height);
            return ptz;
        }

        private VideoInputConfigBase RebuildInputSource(string id, SceneItemSource source, ISceneItem item, StreamerRebuildContext rebuildContext)
        {
            rebuildContext.SetVideoSource(id, source);
            if (source.Device != null)
                return RebuildInputSource_Device(id, source.Device, item, rebuildContext);
            else if (source.Image != null)
                return RebuildInputSource_Image(id, source.Image, rebuildContext);
            else if (source.Lovense != null)
                return RebuildInputSource_Lovense(id, rebuildContext);
            else if (source.Web != null)
                return RebuildInputSource_Web(id, source.Web, rebuildContext);
            else if (source.CaptureDisplay != null)
                return RebuildInputSource_Capture(id, source.CaptureDisplay, false, rebuildContext);
            else if (source.CaptureWindow != null)
                return RebuildInputSource_Capture(id, source.CaptureWindow, true, rebuildContext);
            else
                return GetFailedInputSource(id, rebuildContext, InputIssueDesc.UnknownTypOfSource, "Video source is unknown");
        }

        private VideoInputConfigSingleFrame GetFailedInputSource(string id, StreamerRebuildContext rebuildContext, InputIssueDesc reason, string log)
        {
            Log.Error($"Bad input source in model: {log}");
            rebuildContext.AddVideo(id, reason);
            return new VideoInputConfigSingleFrame(new FixedFrameData(nameof(StaticResources.BadSource), StaticResources.BadSource, SingleFrameType.Png));
        }

        private VideoInputConfigBase RebuildInputSource_Device(string id, SceneItemSourceDevice device, ISceneItem item, StreamerRebuildContext rebuildContext)
        {
            var localDevice = _localSources.GetLocalVideoDevice(device.DeviceName);
            if (localDevice != null)
            {
                var options = DShowOptionsSelector.GetVideoOptions(localDevice, _coreData.Settings.Fps, _coreData.Settings.Resolution, item);
                return new VideoInputConfigFull(new InputSetup(
                    Type: "dshow",
                    Input: $"video={DShowOptionsSelector.GetDeviceName(localDevice)}",
                    Options: options));
            }
            else return GetFailedInputSource(id, rebuildContext, InputIssueDesc.VideoRemoved ,$"Video device '{device?.DeviceName}' not found");
        }

        private VideoInputConfigBase RebuildInputSource_Image(string id, SceneItemSourceImage image, StreamerRebuildContext rebuildContext)
        {
            if (_coreData.Root.Resources.TryGetValue(image.ResourceId, out var resource))
            {
                var data = _resourceService.GetResource(image.ResourceId);
                if (data == null || data.Length == 0)
                    return GetFailedInputSource(id, rebuildContext, InputIssueDesc.ImageNotFound, $"Resource {image.ResourceId} has no data");

                SingleFrameType type = SingleFrameType.Cube;
                if (resource.Info.Type == ResourceType.ImageJpeg)
                    type = SingleFrameType.Jpg;
                else if (resource.Info.Type == ResourceType.ImagePng)
                    type = SingleFrameType.Png;
                else
                    return GetFailedInputSource(id,rebuildContext, InputIssueDesc.ImageUnknownFormat, $"Resource {image.ResourceId} has Unknown format {resource.Info.Type}");

                return new VideoInputConfigSingleFrame(new FixedFrameData(resource.Info.DataHash, data, type));
            }
            else return GetFailedInputSource(id, rebuildContext, InputIssueDesc.ImageNotFound, $"Resource {image.ResourceId} not found");
        }

        private VideoInputConfigBase RebuildInputSource_Lovense(string id, StreamerRebuildContext rebuildContext)
        {
            if (PluginContextSetup.IsLoaded())
                return new VideoInputConfigFull(new InputSetup(Type: PluginContext.PluginName, Input: "", ObjectInput: GetWebBrowserObjectInput(0, 0))); // h, w set in ClientStreamer
            else
                return GetFailedInputSource(id, rebuildContext, InputIssueDesc.PluginIsNotInstalled, $"Lovense plugin is not installed or failed to load");
        }

        private VideoInputConfigBase RebuildInputSource_Web(string id, SceneItemSourceWeb web, StreamerRebuildContext rebuildContext)
        {
            return new VideoInputConfigFull(new InputSetup(Type: WebBrowserContext.Name, Input: web.Url, ObjectInput: GetWebBrowserObjectInput(web.Width, web.Height)));
        }


        private WebBrowserContextSetup GetWebBrowserObjectInput(int width, int height)
        {
            return new WebBrowserContextSetup(Path.Combine(_appEnvironment.GetStorageFolder(), "WB"), _coreData.Root.Settings.Fps, width, height);
        }

        private VideoInputConfigBase RebuildInputSource_Capture(string id, SceneItemSourceCapture capture, bool isWindow, StreamerRebuildContext rebuildContext)
        {
            var ci = _localSources.GetOrCreateCaptureItem(capture.Source, isWindow);

            if (ci?.Wrapped != null)
            {
                var request = new ScreenCaptureRequest
                {
                    Id = $"{capture.Source.CaptureId}",
                    Cursor = capture.CaptureCursor,
                    InitialSize = ci.Wrapped.Size,
                    Item = ci.Wrapped,
                    DebugName = capture.Source.Name
                };
                return new VideoInputConfigFull(new InputSetup(Type: ScreenCaptureContext.Name, Input: ci.Prefix + capture.Source.CaptureId,
                    ObjectInput: request));
            }
            else
                return GetFailedInputSource(id, rebuildContext, InputIssueDesc.CaptureNotFound, $"Capture target '{capture.Source.Name}' not found");
        }

        private VideoFilterChainDescriptor RebuildFilters(SceneItemFilters filters)
        {
            if (filters == null || filters.Filters == null || filters.Filters.Length == 0 || filters.Filters.All(s => !s.Enabled))
                return null;

            return new VideoFilterChainDescriptor(filters.Filters.Where(s => s.Enabled).Select(s => RebuildFilter(s)).Where(s => s != null).ToArray()); ;
        }

        private VideoFilterDescriptor RebuildFilter(SceneItemFilter s)
        {
            if (s.LutResourceId != null)
            {
                var data = _resourceService.GetResource(s.LutResourceId);
                if (data != null && _coreData.Root.Resources.TryGetValue(s.LutResourceId, out var res))
                    return new VideoFilterDescriptor(ModelToStreamerTranslator.Translate(s.Type), s.Value, new FixedFrameData(s.LutResourceId, data, res.Info.Type == ResourceType.LutCube ? SingleFrameType.Cube : SingleFrameType.Png));
                else
                    return null;
            }
            else
                return new VideoFilterDescriptor(ModelToStreamerTranslator.Translate(s.Type), s.Value, null);
        }

        private string GetIngestOutgestUrl(string url)
        {
            var vpn = _coreData.ThisDevice.VpnServerIpAddress;
            if (vpn == null)
            {
                return url;
            }
            else
            {
                UriBuilder builder = new UriBuilder(url);
                builder.Host = vpn;
                return builder.ToString();
            }
        }

        private bool ShouldStartStream()
        {
            bool requested = _coreData.Settings.StreamingToCloudStarted;
            if (requested)
            {
                if (_coreData.Settings.NoStreamWithoutVpn && 
                    _coreData.ThisDevice.VpnState != VpnState.Connected &&
                    _mainVpnModel.IsEnabled)
                {
                    Log.Information("Stream to cloud is not started due to NoStreamWithoutVpn");
                    return false;
                }
            }
            return requested;
        }

        private async Task CollectStatisticsRoutine()
        {
            while (true)
            {
                try
                {
                    CollectStatistics();
                }
                catch(Exception e)
                {
                    Log.Error(e, "Failed to get statistics");
                }

                await Task.Delay(990);
            }
        }

        private void CollectStatistics()
        {
            if (_coreData.ThisDevice == null)
                return;

            var kpi = _coreData.ThisDevice.KPIs;
            
            _healthCheck.ProcessReceivier(_receiverStreamer, kpi);
            _healthCheck.ProcessMain(_mainStreamer, kpi, _lastRebuildContext);
        }

        public async ValueTask DisposeAsync()
        {
            await Task.Run(() =>
            {
                if (_mainStreamer != null)
                    ShutdownStreamer(_mainStreamer, "main streamer");
                if (_receiverStreamer != null)
                    ShutdownStreamer(_receiverStreamer, "receiver streamer");

                Core.Shutdown();
            });
        }
    }


    public class StreamerRebuildContext
    {
        public Dictionary<string, RebuildInfo> Videos { get; } = new Dictionary<string, RebuildInfo>();

        public Dictionary<string, RebuildInfo> Audios { get; } = new Dictionary<string, RebuildInfo>();

        internal void AddVideo(string id, InputIssueDesc reason) => Videos[id].Issue = reason;

        internal void AddAudio(string id, InputIssueDesc reason) => Audios[id].Issue = reason;

        internal void SetVideoSource(string id, SceneItemSource source) => Videos[id] = new RebuildInfo { Video = source };

        internal void SetAudioSource(string id, SceneAudioSource source) => Audios[id] = new RebuildInfo { Audio = source };
    }

    public class RebuildInfo
    {
        public SceneItemSource Video { get; set; }

        public SceneAudioSource Audio { get; set; }

        public InputIssueDesc Issue { get; set; } = InputIssueDesc.None;
    }
}
