using Serilog;
using Streamster.ClientCore.Logging;
using Streamster.ClientCore.Services;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using Streamster.DynamicStreamerWrapper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class MainStreamerModel
    {
        private const string Eq = "^";
        private const string Sep = "`";


        private readonly ConnectionService _connectionService;
        private readonly RootModel _rootModel;
        private readonly LogService _logService;
        private readonly TransientMessageModel _transientMessage;
        private Streamer _dynamicStreamer = null;
        private DynamicStreamerState _dynamicStreamerState;

        private Streamer _receiverStreamer = null;
        private string _receiverStreamerState = null;
        private bool _statisticsStarted;
        private bool _promoIsShown;
        private int _vpnMessage = -1;

        private StreamerLogger _streamerLogger = new StreamerLogger("**");



        public Resolution[] Resolutions { get; } = new Resolution[]
        {
            new Resolution(3840, 2160),
            new Resolution(2560, 1440),
            new Resolution(1920, 1080),
            new Resolution(1280, 720),
            new Resolution(960, 720),
            new Resolution(960, 540),
            new Resolution(640, 360),
        };

        public int[] FpsList { get; } = new[] { 60, 30, 25, 20, 15, 10 };

        public int MinBitrate { get; } = 800;

        public int MaxBitrate { get; set; }

        public Property<int> ActualBitrate { get; } = new Property<int>();

        public Property<IndicatorState> ActualBitrateState { get; } = new Property<IndicatorState>();

        public CoreData CoreData { get; }

        public MainFiltersModel Filters { get; }

        public ScreenRendererModel ScreenRenderer { get; }

        public MainSourcesModel VideoSource { get; }

        public MainVpnModel Vpn { get; }

        public Action<object> SelectResolution { get; }

        public Action<object> SelectFps { get; }

        public Property<string> Promo { get; } = new Property<string>();

        public Property<string> PromoUrl { get; } = new Property<string>();

        public Property<bool> ChangeStreamParamsDisabled { get; } = new Property<bool>();

        public MainStreamerModel(CoreData coreData, MainFiltersModel filters, 
            ScreenRendererModel screenRenderer, 
            ConnectionService connectionService, 
            MainSourcesModel videoSource,
            RootModel rootModel, 
            LogService logService, 
            MainVpnModel vpnModel,
            TransientMessageModel transientMessage)
        {
            CoreData = coreData;
            Filters = filters;
            ScreenRenderer = screenRenderer;
            _connectionService = connectionService;
            VideoSource = videoSource;
            _rootModel = rootModel;
            _logService = logService;
            Vpn = vpnModel;
            _transientMessage = transientMessage;
            SelectResolution = o => CoreData.Settings.Resolution = (Resolution)o;
            SelectFps = o => CoreData.Settings.Fps = (int)o;

            SetActualBitrate(0, IndicatorState.Unknown);
        }

        public async Task PrepareAsync()
        {
            _dynamicStreamer = new Streamer(_streamerLogger);
            _dynamicStreamerState = new DynamicStreamerState();
            await VideoSource.PrepareAsync(this);
        }

        internal async Task StartAsync()
        {
            MaxBitrate = _connectionService.Claims.MaxBitrate;
            if (CoreData.Settings.StreamingToCloud == StreamingToCloudBehavior.AppStart)
                CoreData.Settings.StreamingToCloudStarted = true;

            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.Resolution, (i, c, p) => RefreshStreamer());
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.Fps, (i, c, p) => RefreshStreamer());
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.SelectedVideo, (i, c, p) => RefreshStreamer());
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.SelectedAudio, (i, c, p) => RefreshStreamer());
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.Bitrate, (i, c, p) =>
            {
                RefreshStreamer();
                RefreshPromo();
            });
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.EncoderType, (i, c, p) => RefreshStreamer());
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.EncoderQuality, (i, c, p) => RefreshStreamer());
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.StreamingToCloudStarted, (i, c, p) => RefreshStreamer());
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.NoStreamWithoutVpn, (i, c, p) => RefreshStreamer());
            CoreData.Subscriptions.SubscribeForProperties<ISettings>(s => s.IsRecordingRequested, (i, c, p) =>
            {
                RefreshControls();
                RefreshStreamer();
            });

            CoreData.Subscriptions.SubscribeForProperties<IVideoInput>(s => s.Filters, (i, c, p) => RefreshStreamer());
            CoreData.Subscriptions.SubscribeForProperties<IIngest>(s => s.Data, (i, c, p) => RefreshStreamer());
            CoreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.AssignedOutgest, (i, c, p) => RefreshStreamer());
            CoreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.VpnState, (i, c, p) => RefreshStreamer());
            CoreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.VpnServerIpAddress, (i, c, p) => RefreshStreamer());
            CoreData.Subscriptions.SubscribeForProperties<IChannel>(s => s.IsOn, (i, c, p) =>
            {
                RefreshControls();
                if (CoreData.Settings.StreamingToCloud == StreamingToCloudBehavior.FirstChannel && i.IsOn && CoreData.Root.Channels.Values.Count(e => e.IsOn) == 1)
                    CoreData.Settings.StreamingToCloudStarted = true;
            });

            VideoSource.Start();
            ScreenRenderer.SetStreamer(_dynamicStreamer, true);
            ScreenRenderer.Start();

            await Vpn.StartAsync();

            RefreshPromo();
            RefreshStreamer();
        }

        private void RefreshControls()
        {
            ChangeStreamParamsDisabled.Value = CoreData.Settings.IsRecordingRequested || CoreData.Root.Channels.Values.Any(s => s.IsOn);
        }

        private void RefreshPromo()
        {
            if (!_promoIsShown && CoreData.Settings.Bitrate == MaxBitrate && MaxBitrate < 16000)
            {
                _promoIsShown = true;
                Promo.Value = MaxBitrate == 4000 ? "Get more bitrate after registration" : "Upgate you plan to get more bitrate";
                PromoUrl.Value = MaxBitrate == 4000 ? _rootModel.AppData.RegisterUrl : _rootModel.AppData.PricingUrl;
                TaskHelper.RunUnawaited(async () =>
                {
                    await Task.Delay(8000);
                    Promo.Value = null;
                }, "Show promo");
            }
        }

        public void RefreshStreamer()
        {
            if (!VideoSource.IsReady())
            {
                Log.Warning("Video source is not yet ready");
                return;
            }

            var settings = CoreData.Root.Settings;

            if (settings.SelectedVideo == null || !CoreData.Root.VideoInputs.TryGetValue(settings.SelectedVideo, out var videoInput))
            {
                Log.Warning($"Bad video source '{settings.SelectedVideo}'");
                return;
            }

            if (videoInput.Capabilities?.Caps == null || videoInput.Capabilities.Caps.Length == 0)
            {
                Log.Warning($"Bad video capabilities '{settings.SelectedVideo}'");
                return;
            }

            if (settings.SelectedAudio == null || !CoreData.Root.AudioInputs.TryGetValue(settings.SelectedAudio, out var audioInput))
            {
                Log.Warning($"Bad audio source '{settings.SelectedAudio}'");
                return;
            }

            if (!_statisticsStarted)
            {
                _statisticsStarted = true;
                TaskHelper.RunUnawaited(CollectStatisticsRoutine(), "CollectStatistics");
            }

            // shutdown
            if (videoInput.Owner == CoreData.ThisDeviceId)
            {
                if (CoreData.ThisDevice.RequireOutgest)
                {
                    Log.Information("Withdrawing outgest");
                    CoreData.ThisDevice.RequireOutgest = false;
                }

                if (_receiverStreamerState != null)
                {
                    Log.Information("Closing outgest streamer");
                    ScreenRenderer.SetStreamer(null, true);
                    _receiverStreamer.Shutdown();
                    _receiverStreamer = null;
                    _receiverStreamerState = null;
                }
            }
            else
            {
                
                if (_dynamicStreamerState != null)
                {
                    ScreenRenderer.SetStreamer(null, false);
                    _dynamicStreamer.Shutdown();
                    _dynamicStreamer = null;
                    _dynamicStreamerState = null;
                }
            }

            // start
            if (videoInput.Owner == CoreData.ThisDeviceId)
            {
                if (_dynamicStreamerState == null)
                    _dynamicStreamerState = new DynamicStreamerState();

                if (_dynamicStreamer == null)
                {
                    _dynamicStreamer = new Streamer(_streamerLogger);
                    ScreenRenderer.SetStreamer(_dynamicStreamer, true);
                }

                var filterSpec = Filters.GetFiltersSpec(videoInput.Filters);
                var resolution = settings.Resolution;
                var fps = settings.Fps;
                var capability = FindBestCapability(videoInput, fps, resolution, filterSpec);
                var inputFps = GetInputFps(capability, fps);

                //input
                var inputConfig = GetInputConfig(videoInput.Name, capability, inputFps, audioInput.Name);
                string inputHash = $"{inputConfig.input}, {inputConfig.options}, {fps}, {resolution}";
                if (inputHash != _dynamicStreamerState.Input)
                {
                    Log.Information($"Input Init('{inputHash}')");
                    _dynamicStreamer.SetInput("dshow", inputConfig.input, inputConfig.options, fps, resolution.Width, resolution.Height);
                    _dynamicStreamerState.Input = inputHash;
                    _dynamicStreamerState.InputFps = inputFps;
                }

                // filter
                string filterConfig = GetFilterConfig(fps, inputFps, capability, resolution, filterSpec);
                if (filterConfig != _dynamicStreamerState.Filter)
                {
                    Log.Information($"SetFilter('{filterConfig}')");
                    _dynamicStreamer.SetFilter(filterConfig);
                    _dynamicStreamerState.Filter = filterConfig;
                }

                // encoder
                GetEncoderConfig(settings.Bitrate, fps, out int videoMaxBitrate, out int audioMaxBitrate,
                                                 out var videoCodec, out var videoOptions, out var videoCodecFallback, out var videoOptionsFallback);

                string encoderHash = $"{videoCodec}, {videoOptions}, {videoMaxBitrate}, {audioMaxBitrate}";

                if (encoderHash != _dynamicStreamerState.Encoder)
                {
                    Log.Information($"Encoder Init({encoderHash})");
                    _dynamicStreamer.SetEncoder(videoCodec, videoOptions, videoCodecFallback, videoOptionsFallback, videoMaxBitrate, "aac", "", audioMaxBitrate);
                    _dynamicStreamerState.Encoder = encoderHash;
                }

                // streaming output
                var ingest = CoreData.Root.Ingests.Values.FirstOrDefault();

                bool startStream = ShouldStartStream();

                if (ingest == null || !startStream) // remove
                {
                    if (_dynamicStreamerState.StreamingOutputId != -1)
                        _dynamicStreamer.RemoveOutput(_dynamicStreamerState.StreamingOutputId);
                    _dynamicStreamerState.StreamingOutputId = -1;
                    _dynamicStreamerState.StreamingOutput = string.Empty;
                }
                else
                {
                    var ingestUrl = GetIngestOutgestUrl(ingest.Data.Output);

                    string ingestHash = $"{ingest.Data.Type}, {ingestUrl}, {ingest.Data.Options}";
                    if (_dynamicStreamerState.StreamingOutputId == -1) // add
                    {
                        Log.Information($"Start Streaming ({ingestHash})");
                        _dynamicStreamerState.StreamingOutputId = _dynamicStreamer.AddOutput(ingest.Data.Type, ingestUrl, ingest.Data.Options);
                        _dynamicStreamerState.StreamingOutput = ingestHash;
                    }
                    else if (ingestHash != _dynamicStreamerState.StreamingOutput) // update
                    {
                        Log.Information($"Update Streaming ({ingestHash})");
                        _dynamicStreamer.RemoveOutput(_dynamicStreamerState.StreamingOutputId);
                        _dynamicStreamerState.StreamingOutputId = _dynamicStreamer.AddOutput(ingest.Data.Type, ingestUrl, ingest.Data.Options);
                        _dynamicStreamerState.StreamingOutput = ingestHash;
                    }
                }

                bool recording = CoreData.Settings.IsRecordingRequested;

                if (!recording)
                {
                    if (_dynamicStreamerState.RecordingOutputId != -1)
                        _dynamicStreamer.RemoveOutput(_dynamicStreamerState.RecordingOutputId);
                    _dynamicStreamerState.RecordingOutputId = -1;
                }
                else
                {
                    if (_dynamicStreamerState.RecordingOutputId == -1)
                    {
                        if (MainSettingsModel.IsValidRecordingPath(CoreData.ThisDevice.DeviceSettings.RecordingsPath))
                        {
                            var now = DateTime.Now;
                            string output = Path.Combine(CoreData.ThisDevice.DeviceSettings.RecordingsPath, now.ToString("yyy_MM_dd__HH_mm_ss") + ".flv");
                            Log.Information($"Start recording({output})");
                            _dynamicStreamerState.RecordingOutputId = _dynamicStreamer.AddOutput("flv", output, "");
                        }
                    }
                }
            }
            else
            {
                if (_vpnMessage != -1)
                    _transientMessage.Clear(_vpnMessage);

                if (!CoreData.ThisDevice.RequireOutgest)
                {
                    Log.Information("Requesting outgest");
                    CoreData.ThisDevice.RequireOutgest = true;
                }

                var outgestId = CoreData.ThisDevice.AssignedOutgest;
                if (outgestId != null && CoreData.Root.Outgests.TryGetValue(outgestId, out var outgest))
                {
                    var outgetsUrl = GetIngestOutgestUrl(outgest.Data.Output);
                    var state = $"{outgest.Data.Type}, {outgetsUrl}, {outgest.Data.Options}";
                    if (_receiverStreamerState == null)
                    {
                        Log.Information($"Connecting to outgest '{state}'");
                        _receiverStreamer = new Streamer(_streamerLogger);
                        ScreenRenderer.SetStreamer(_receiverStreamer, false);
                        _receiverStreamer.SetInput(outgest.Data.Type, outgetsUrl, outgest.Data.Options, 0, 0, 0);
                        _receiverStreamerState = state;
                    }
                    else if (_receiverStreamerState != state)
                    {
                        Log.Information($"Updating outgest '{state}'");
                        _receiverStreamer.SetInput(outgest.Data.Type, outgetsUrl, outgest.Data.Options, 0, 0, 0);
                        _receiverStreamerState = state;
                    }
                }
            }

        }

        private string GetIngestOutgestUrl(string url)
        {
            var vpn = CoreData.ThisDevice.VpnServerIpAddress;
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
            bool requested = CoreData.Settings.StreamingToCloudStarted;
            if (requested)
            {
                if (CoreData.Settings.NoStreamWithoutVpn)
                {
                    if (CoreData.ThisDevice.VpnState != VpnState.Connected)
                    {
                        if (CoreData.ThisDevice.VpnState == VpnState.Idle)
                            _vpnMessage = _transientMessage.Show("VPN is OFF. Streaming is not possible.", TransientMessageType.Error, false);
                        else
                            _vpnMessage = _transientMessage.Show("VPN is ON but not yet connected. Streaming not possible.", TransientMessageType.Error, false);
                        return false;
                    }
                }
            }
            if (_vpnMessage != -1)
                _transientMessage.Clear(_vpnMessage);
            return requested;
        }

        internal void SetActualBitrate(int ave, IndicatorState state)
        {
            if (ave < MinBitrate + 30)
                ave = MinBitrate + 30;

            ActualBitrate.Value = ave;
            ActualBitrateState.Value = state;
        }

        private void GetEncoderConfig(int bitrate, int fps, out int video, out int audio, out string videoCodec, out string videoOptions, out string videoCodecFallback, out string videoOptionsFallback)
        {
            video = bitrate;
            audio = video / 14;
            if (audio > 224)
                audio = 224;
            else if (audio < 50)
                audio = 50;

            video = video - audio;

            var addition = $"{Sep}g{Eq}{fps * 2}{Sep}keyint_min{Eq}{fps + 1}";

            string encoder = null; 
            string settings = null; 
            string fallbackEncoder = null; 
            string fallbackSettings = null; 

            if (CoreData.Settings.EncoderType == EncoderType.Software)
            {
                switch (CoreData.Settings.EncoderQuality)
                {
                    case EncoderQuality.Speed:
                        encoder = "libx264";
                        settings = $"tune{Eq}zerolatency{Sep}preset{Eq}ultrafast";
                        break;
                    case EncoderQuality.Balanced:
                        encoder = "libx264";
                        settings = $"tune{Eq}zerolatency{Sep}preset{Eq}superfast";
                        break;
                    case EncoderQuality.BalancedQuality:
                        encoder = "libx264";
                        settings = $"tune{Eq}zerolatency{Sep}preset{Eq}veryfast";
                        break;
                    case EncoderQuality.Quality:
                        encoder = "libx264";
                        settings = $"tune{Eq}zerolatency{Sep}preset{Eq}faster";
                        break;
                }
            }
            else
            {
                switch (CoreData.Settings.EncoderQuality)
                {
                    case EncoderQuality.Speed:
                        encoder = "h264_qsv";
                        settings = $"preset{Eq}fast{Sep}bf{Eq}0{Sep}profile{Eq}main";
                        fallbackEncoder = "libx264";
                        fallbackSettings = $"tune{Eq}zerolatency{Sep}preset{Eq}ultrafast";
                        break;
                    case EncoderQuality.Balanced:
                    case EncoderQuality.BalancedQuality:
                        encoder = "h264_qsv";
                        settings = $"preset{Eq}medium{Sep}bf{Eq}0{Sep}profile{Eq}main";
                        fallbackEncoder = "libx264";
                        fallbackSettings = $"tune{Eq}zerolatency{Sep}preset{Eq}superfast";
                        break;
                    case EncoderQuality.Quality:
                        encoder = "h264_qsv";
                        settings = $"preset{Eq}slow{Sep}bf{Eq}0{Sep}profile{Eq}main";
                        fallbackEncoder = "libx264";
                        fallbackSettings = $"tune{Eq}zerolatency{Sep}preset{Eq}veryfast";
                        break;
                }
            }

            videoCodec = encoder;
            videoOptions = settings + addition + GetCodecSpecificOption(encoder);
            videoCodecFallback = fallbackEncoder;
            videoOptionsFallback = fallbackSettings + addition + GetCodecSpecificOption(fallbackEncoder);
        }

        private string GetCodecSpecificOption(string encoder)
        {
            if (encoder == "libx264")
                return $"{Sep}x264-params{Eq}filler=true:nal-hrd=cbr:force-cfr=1";

            return "";
        }

        private string GetFilterConfig(int fps, int inputFps, VideoInputCapability capability, Resolution resolution, FilterSpecs[] filterSpec)
        {
            IEnumerable<string> chain = (capability.Fmt == VideoInputCapabilityFormat.MJpeg) ?
                                            filterSpec.Reverse().Select(s => s.Spec) :
                                            filterSpec.Select(s => s.Spec);

            if (capability.W != resolution.Width || capability.H != resolution.Height)
            {
                var scaleFilter = $"scale={resolution.Width}:{resolution.Height}";

                if (resolution.Width > capability.W)
                    chain = chain.Concat(new[] { scaleFilter });
                else
                    chain = new[] { scaleFilter }.Concat(chain);
            }

            var fpsFilter = $"fps={fps}";
            if (inputFps >= fps)
                chain = new[] { fpsFilter }.Concat(chain);
            else
                chain = chain.Concat(new[] { fpsFilter});

            return string.Join(", ", chain);
        }

        private VideoInputCapability FindBestCapability(IVideoInput videoInput, int fps, Resolution resolution, FilterSpecs[] filterSpec)
        {
            bool hasJpegOnlyFilter = filterSpec.Any(s => s.InputFormats.Length == 1 && s.InputFormats[0] == VideoInputCapabilityFormat.MJpeg);
            bool hasRowOnlyFilter = filterSpec.Any(s => s.InputFormats.Length == 1 && s.InputFormats[0] == VideoInputCapabilityFormat.Raw);

            bool jpeg = hasJpegOnlyFilter;

            var caps = videoInput.Capabilities.Caps.Where(s => s.Fmt != VideoInputCapabilityFormat.H264).ToArray();

            var shortlist = caps.Where(s => s.H == resolution.Height && s.W == resolution.Width && fps >= s.MinF && fps <= s.MaxF).ToList();
            if (shortlist.Count > 0)
            {
                return SelectFirstCapability(shortlist, jpeg);
            }
            // no exectly good capability - need to select closest

            var n = videoInput.Name;

            // lets ignore fps, most likely fps is requested higher then supported
            var scored = caps.Select(c => new { cap = c, score = GetScore(c, jpeg, fps, resolution) }).ToList();
            var maxScore = scored.Max(s => s.score);
            var final = scored.First(s => s.score == maxScore);

            Log.Information($"Selecting '{final.cap}' capability by score '{final.score}' for Fps={fps}, Res={resolution}");

            return final.cap;
        }

        private int GetScore(VideoInputCapability c, bool jpeg, int fps, Resolution resolution)
        {
            int score = 0;

            if (fps < c.MinF)
                score -= (c.MinF - fps);
            else if (fps > c.MaxF)
                score -= (fps - c.MaxF) * 2;

            if ((double)resolution.Height / resolution.Width != (double)c.H / c.W)
                score -= 10;

            double diff = (double)c.W / resolution.Width;

            if (diff < 1.0) //cap lower
                score -= (int)((1.0 - diff) * 30); // half of width = -15 score
            else
                score -= (int)((diff - 1.0) * 15); // twice bigger = -15 score

            if ((c.Fmt == VideoInputCapabilityFormat.MJpeg) != jpeg)
                score -= 1;
            
            return score;
        }

        private VideoInputCapability SelectFirstCapability(IEnumerable<VideoInputCapability> shortlist, bool jpegInPreference)
        {
            if (jpegInPreference)
            {
                var cap = shortlist.FirstOrDefault(s => s.Fmt == VideoInputCapabilityFormat.MJpeg);
                if (cap != null)
                    return cap;
            }
            else
            {
                var cap = shortlist.FirstOrDefault(s => s.Fmt != VideoInputCapabilityFormat.MJpeg);
                if (cap != null)
                    return cap;
            }
            return shortlist.First();
        }

        public (string input, string options) GetInputConfig(string videoInputName,
                                        VideoInputCapability capability,
                                        int inputFps,
                                        string audioInputName)
        {
            string enforceMjpeg = "";
            if (capability.Fmt == VideoInputCapabilityFormat.MJpeg)
                enforceMjpeg = $"vcodec{Eq}mjpeg{Sep}";

            int rtbufsize = CaclulateRtBufferSize(capability);

            string fpsOption = $"framerate{Eq}{inputFps}{Sep}";

            return (input: 
                            $"video={videoInputName}:audio={audioInputName}",
                    options:
                            $"video_size{Eq}{capability.W}x{capability.H}{Sep}" +
                            fpsOption +
                            $"sample_rate{Eq}44100{Sep}" +
                            $"channels{Eq}2{Sep}" +
                            $"audio_buffer_size{Eq}50{Sep}" +
                            $"fflags{Eq}nobuffer{Sep}" +
                            enforceMjpeg +
                            $"rtbufsize{Eq}{rtbufsize}");
        }

        private int GetInputFps(VideoInputCapability capability, int fps)
        {
            if (capability.MinF > fps)
                return capability.MinF;
            else if (fps > capability.MaxF)
                return capability.MaxF;
            return fps;
        }

        private int CaclulateRtBufferSize(VideoInputCapability capability)
        {
            int width = capability.W < 1024 ? 1024 : capability.W;

            if (capability.Fmt == VideoInputCapabilityFormat.MJpeg) 
                return width * 4000; // about 70 frames
            else
                return width * 40000; // about 27 frames
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

                try
                {
                    _streamerLogger.Flush().ForEach(s =>
                    {
                        if (s.Pattern == null)
                            Log.Information(_streamerLogger.GetLogMessage(s));
                        else
                            Log.Warning(_streamerLogger.GetLogMessage(s));
                    });
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to process logs");
                }
                await Task.Delay(990);
            }
        }

        private void CollectStatistics()
        {
            if (CoreData.ThisDevice == null)
                return;

            var kpi = CoreData.ThisDevice.KPIs;
            if (_receiverStreamer == null)
            {
                kpi.CloudIn.Enabled = false;
            }
            else
            {
                var cloudIn = kpi.CloudIn;
                cloudIn.Enabled = true;
                var stat = _receiverStreamer.GetStreamerStatistics();
                var input = stat.FirstOrDefault(s => s.Id == -1)?.CurrentValues;
                if (input != null)
                {
                    cloudIn.Bitrate = Streamer.GetBitrateFromStatistics(input.Transferred);
                    cloudIn.Errors = input.Errors;
                }
            }

            if (_dynamicStreamerState == null || _dynamicStreamer == null)
            {
                kpi.CloudOut.Enabled = false;
                kpi.Encoder.Enabled = false;
            }
            else
            {
                var stat = _dynamicStreamer.GetStreamerStatistics();

                var encoder = kpi.Encoder;
                
                var input = stat.FirstOrDefault(s => s.Id == -1)?.CurrentValues;
                if (input != null)
                {
                    encoder.Enabled = true;
                    encoder.QueueSize = input.QueueSize;
                    encoder.InputFps = input.Frames;
                    encoder.InputErrors = input.Errors;
                    encoder.InputTargetFps = _dynamicStreamerState.InputFps;
                }
                else
                    encoder.Enabled = false;

                if (_dynamicStreamerState.StreamingOutputId < 0)
                    kpi.CloudOut.Enabled = false;
                else
                {
                    var cloudOut = kpi.CloudOut;
                    
                    var output = stat.FirstOrDefault(s => s.Id == _dynamicStreamerState.StreamingOutputId)?.CurrentValues;
                    if (output != null)
                    {
                        cloudOut.Enabled = true;
                        cloudOut.Bitrate = Streamer.GetBitrateFromStatistics(output.Transferred);
                        cloudOut.Drops = output.Drops;
                        cloudOut.Errors = output.Errors;
                    }
                    else
                        cloudOut.Enabled = false;
                }
            }
        }
    }

    class DynamicStreamerState
    {
        public string Input { get; set; }

        public string Encoder { get; set; }

        public string Filter { get; set; }

        public int StreamingOutputId { get; set; } = -1;

        public string StreamingOutput { get; set; }

        public int RecordingOutputId { get; set; } = -1;

        public int InputFps { get; set; } = -1;
    }
}
