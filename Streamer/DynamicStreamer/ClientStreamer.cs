using DynamicStreamer.Contexts;
using DynamicStreamer.DirectXHelpers;
using DynamicStreamer.Extensions;
using DynamicStreamer.Extensions.ScreenCapture;
using DynamicStreamer.Extensions.WebBrowser;
using DynamicStreamer.Helpers;
using DynamicStreamer.Nodes;
using DynamicStreamer.Queues;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicStreamer
{
    public class ClientStreamer : StreamerBase<ClientStreamerConfig>
    {
        private readonly BitrateController<ClientStreamerConfig> _bitrateController;
        private readonly OverloadController _overloadController;
        private readonly HardwareEncoderCheck _hardwareEncoderCheck;
        private readonly TimerSubscription _timer;
        private DirectXContext _dx;
        private int _dxFailureCounter = 0;
        private int _dxFailureCounter5Minutes = 0;
        private bool _qsvNvSwitchedOff = false;
        private bool _dxSwitchedOff = false;

        public List<Trunk> VideoInputTrunks { get; } = new List<Trunk>();

        public List<AudioInputTrunk> AudioInputTrunks { get; } = new List<AudioInputTrunk>();

        public VideoEncoderTrunk VideoEncoderTrunk { get; }

        public AudioEncoderTrunk AudioEncoderTrunk { get; } = new AudioEncoderTrunk();

        public OutputStreamQueue<Packet> OutputQueue { get; }

        public List<OutputTrunk> OutputTrunks { get; } = new List<OutputTrunk>();

        public VideoRenderOptions VideoRenderOptions { get; set; }




        public ClientStreamer(string name, HardwareEncoderCheck hardwareEncoderCheck) : base(name)
        {
            _overloadController = new OverloadController(this);
            VideoEncoderTrunk = new VideoEncoderTrunk(this, _overloadController);

            var sources = Enumerable.Range(0, 2).ToArray();
            OutputQueue = new OutputStreamQueue<Packet>(PacketPool, sources);

            _bitrateController = new BitrateController<ClientStreamerConfig>(this, (c, r) => c with { BitrateDrcRatio = r });
            _hardwareEncoderCheck = hardwareEncoderCheck;

            _timer = Subscribe(5*60*1000, On5Minutes);
        }

        private void On5Minutes()
        {
            _dxFailureCounter5Minutes = 0;
        }

        public override void UpdateCore(UpdateVersionContext update, ClientStreamerConfig c)
        {
            if (!c.Disposing)
            {
                var videoEncoderCtx = UpdateVideo(update, c);
                var audioEncoderCtx = UpdateAudio(update, c);
                bool outputChanged = UpdateOutputs(update, c, videoEncoderCtx, audioEncoderCtx);

                if (outputChanged)
                    VideoEncoderTrunk.EncoderNode.MakeIFrameNextPacket(update.Version);
            }
            else
                this.DisposeInternals();
        }

        private void DisposeInternals()
        {
            _stopped = true;
            _timer.Unsubscribe();

            VideoInputTrunks.ForEach(s => s.Dispose());
            AudioInputTrunks.ForEach(s => s.Dispose());
            OutputTrunks.ForEach(s => s.Dispose());
            VideoEncoderTrunk?.Dispose();
            AudioEncoderTrunk?.Dispose();

            _overloadController?.Dispose();
        }

        private bool UpdateOutputs(UpdateVersionContext version, ClientStreamerConfig c, IEncoderContext videoEncoderCtx, IEncoderContext audioEncoderCtx)
        {
            bool changed = false;
            foreach (var trunkConfig in c.OutputTrunks)
            {
                var trunk = OutputTrunks.FirstOrDefault(s => s.Id == trunkConfig.Id);
                if (trunk == null)
                {
                    trunk = new OutputTrunk { Id = trunkConfig.Id };
                    trunk.OutputNode = new OutputNode(new NodeName("X", trunkConfig.Id), this, OutputQueue);
                    OutputTrunks.Add(trunk);
                    changed = true;
                }

                List<OutputStreamProperties> outputStreams = new List<OutputStreamProperties> {
                    new OutputStreamProperties
                        {
                            CodecProps = videoEncoderCtx.Config.CodecProps,
                            input_time_base = _time_base
                        },
                    new OutputStreamProperties
                    {
                        CodecProps = audioEncoderCtx.Config.CodecProps,
                        input_time_base = _time_base
                    }
                };

                if (trunk.OutputNode.PrepareVersion(version, new OutputSetup
                                                                {
                                                                    Type = trunkConfig.OutputSetup.Type,
                                                                    Output = trunkConfig.OutputSetup.Output,
                                                                    Options = trunkConfig.OutputSetup.Options,
                                                                    TimeoutMs = trunkConfig.OutputSetup.TimeoutMs,
                                                                    OutputStreamProps = outputStreams.ToArray()
                                                                }, trunkConfig.RequireBitrateControl ? _bitrateController : null, trunk.Id))
                    changed = true;
            }

            OutputTrunks
                .Where(s => c.OutputTrunks.FirstOrDefault(vit => vit.Id == s.Id) == null)
                .ToList()
                .ForEach(s =>
                {
                    Core.LogInfo($"Disposing output {s.Id}");
                    OutputTrunks.Remove(s);
                    s.Dispose();
                });

            var trunkBitrateControl = c.OutputTrunks.FirstOrDefault(s => s.RequireBitrateControl);
            if (trunkBitrateControl == null)
                _bitrateController.ShutDown();
            else
                _bitrateController.SetId(trunkBitrateControl.Id, c.VideoEncoderTrunk.Bitrate);

            return changed;
        }

        private IEncoderContext UpdateAudio(UpdateVersionContext version, ClientStreamerConfig c)
        {
            if (c.AudioEncoderTrunk.sample_rate == 0) // receiver mode
                return null;

            if (AudioEncoderTrunk.EncoderNode == null)
            {
                AudioEncoderTrunk.EncoderNode = new EncoderNode(new NodeName("AE", null, "E", 3), this);
                AudioEncoderTrunk.UiFilterQueue = new FrameOutput(this, c.AudioEncoderTrunk.OnAudioFrame);
                AudioEncoderTrunk.EncoderAndUiFilterQueue = new DuplicateQueue<Frame>(FramePool);                
                AudioEncoderTrunk.MixingFilterQueue = new AudioMixingQueue(new NodeName("AE", null, "FMix", 1), FramePool, this,
                    new AudioMixingQueueSetup(CheckAgainstLastPacketEndPts: false, UseCurrentTimeForDelta: true, GenerateSilenceToRuntime:10, PushSilenceDelay: 44100 / 4));
                AudioEncoderTrunk.EncoderQueue = new UnorderedStreamQueue<Frame>(new NodeName("AE", null, "Eq", 3), FramePool);
                AudioEncoderTrunk.EncoderAndUiFilterQueue.SetQueues(AudioEncoderTrunk.UiFilterQueue, AudioEncoderTrunk.EncoderQueue);
            }
            version.RuntimeConfig.Add(AudioEncoderTrunk.MixingFilterQueue, null);

            var br = new EncoderBitrate { bit_rate = (int)(c.BitrateDrcRatio * c.AudioEncoderTrunk.Bitrate) };

            var outputQueue = new SetSourceIdQueue<Packet>(OutputQueue, 1);
            var encoderCtx = AudioEncoderTrunk.EncoderNode.PrepareVersion(version, AudioEncoderTrunk.EncoderQueue,
                new ChangeTimeBaseQueue<Packet>(outputQueue, new AVRational { num = 1, den = c.AudioEncoderTrunk.sample_rate }, _time_base), 
                new EncoderSetup
                {
                    Name = "aac",
                    EncoderBitrate = br,
                    EncoderSpec = new EncoderSpec
                    {
                        sample_rate = c.AudioEncoderTrunk.sample_rate,
                        channel_layout = _default_channel_layout,
                        time_base = new AVRational { num = 1, den = c.AudioEncoderTrunk.sample_rate }
                    }
                }, 
                (c, same) =>
                {
                    if (same)
                        c.UpdateBitrate(br);
                });

            int counter = 0;
            List<MixingFilterAudioSource> audioSources = new List<MixingFilterAudioSource>();
            foreach (var trunkConfig in c.AudioInputTrunks)
            {
                var trunk = AudioInputTrunks.FirstOrDefault(s => s.Id == trunkConfig.Id);
                if (trunk == null)
                {
                    trunk = new AudioInputTrunk { Id = trunkConfig.Id };
                    trunk.Input = new InputNode(new NodeName("A", trunkConfig.Id, "I", 0), () => InputChanged(), this);
                    trunk.DecoderQueue = new UnorderedStreamQueue<Packet>(new NodeName("A", trunkConfig.Id, "Dq", 0), PacketPool);
                    trunk.FilterQueue = new UnorderedStreamQueue<Frame>(new NodeName("A", trunkConfig.Id, "Fq", 0), FramePool);
                    AudioInputTrunks.Add(trunk);
                }

                var inputCtx = trunk.Input.PrepareVersion(version, trunk.DecoderQueue, trunkConfig.Setup with { AdjustInputType = AdjustInputType.CurrentTime });

                if (inputCtx != null)
                {
                    var streamProps = inputCtx.Config.InputStreamProps[0];
                    if (trunk.Decoder == null)
                        trunk.Decoder = new DecoderNode(new NodeName("A", trunk.Id, "D", 1), this);
                    IDecoderContext decoderCtx = trunk.Decoder.PrepareVersion(version, trunk.DecoderQueue, trunk.FilterQueue, new DecoderSetup(DecoderContextFFMpeg.Type, streamProps.CodecProps, null));
                    var sourceId = counter++;
                    IFilterContext filterCtx = UpdateAudioFilter(version, trunk, trunkConfig, streamProps, decoderCtx, sourceId, c.AudioEncoderTrunk);

                    audioSources.Add(new MixingFilterAudioSource(decoderCtx, filterCtx, sourceId));
                }
            }

            AudioInputTrunks
                .Where(s => c.AudioInputTrunks.FirstOrDefault(vit => vit.Id == s.Id) == null)
                .ToList()
                .ForEach(s =>
                {
                    Core.LogInfo($"Disposing audio {s.Id}");
                    AudioInputTrunks.Remove(s);
                    s.Dispose();
                });

            if (audioSources.Count > 0) 
            {
                UpdateAudioMixingFilter(version, audioSources, encoderCtx, c.AudioEncoderTrunk);
            }
            else
                AudioEncoderTrunk.MixingFilterQueue.Reset(version.Version, audioSources.Count, false);

            return encoderCtx;
        }

        private void UpdateAudioMixingFilter(UpdateVersionContext version, List<MixingFilterAudioSource> audioSources, IEncoderContext encoderCtx, AudioEncoderTrunkConfig audioEncoderTrunk)
        {
            if (AudioEncoderTrunk.MixingFilter == null)
                AudioEncoderTrunk.MixingFilter = new FilterNode(new NodeName("AE", null, "FMix", 2), this);

            var inputSpecs = audioSources.Select(s => new FilterInputSetup(new FilterInputSpec
            {
                time_base = new AVRational { num = 1, den = audioEncoderTrunk.sample_rate },
                sample_fmt = _default_sample_fmt,
                sample_rate = audioEncoderTrunk.sample_rate,
                channel_layout = _default_channel_layout
            })).ToArray();

            var outputSpecs = new FilterOutputSpec
            {
                sample_fmt = _default_sample_fmt,
                sample_rate = audioEncoderTrunk.sample_rate,
                channel_layout = _default_channel_layout,
                required_frame_size = encoderCtx.Config.EncoderProps.required_frame_size
            };


            string finalChain = "anull";
            if (audioEncoderTrunk.VolumeDb != 0.0)
            {
                finalChain = $"volume={Core.FormatDouble(audioEncoderTrunk.VolumeDb, 4)}dB";
            }

            string final = null;
            if (audioSources.Count == 1)
            {
                final = $"[in0]{finalChain}[out]";
            }
            else
            {
                var inputs = string.Join("", audioSources.Select(s => $"[in{s.SourceId}]"));
                final = $"{inputs}amix=inputs={audioSources.Count}[mid];[mid]{finalChain}[out]";
            }

            AudioEncoderTrunk.MixingFilter.PrepareVersion(version, AudioEncoderTrunk.MixingFilterQueue, AudioEncoderTrunk.EncoderAndUiFilterQueue, new FilterSetup
            {
                Type = FilterContextFFMpeg.Type,
                FilterSpec = final,
                InputSetups = inputSpecs,
                OutputSpec = outputSpecs
            }, (ctx, same) => AudioEncoderTrunk.MixingFilterQueue.Reset(version.Version, audioSources.Count, same));
        }

        private IFilterContext UpdateAudioFilter(UpdateVersionContext version, AudioInputTrunk trunk, AudioInputTrunkConfig trunkConfig, InputStreamProperties streamProps, IDecoderContext decoderCtx, int sourceId, AudioEncoderTrunkConfig audioEncoderTrunkConfig)
        {
            if (trunk.Filter == null)
            {
                trunk.UiOutput = new FrameOutput(this, trunkConfig.OnAudioFrame);
                trunk.MixerAndUiFilterQueue = new DuplicateQueue<Frame>(FramePool);
                trunk.MixerAndUiFilterQueue.SetQueues(trunk.UiOutput, AudioEncoderTrunk.MixingFilterQueue);
                trunk.Filter = new FilterNode(new NodeName("A", trunkConfig.Id, "F", 2), this);
            }

            string final = "anull";
            if (trunkConfig.VolumeDb != 0.0)
                final = $"volume={Core.FormatDouble(trunkConfig.VolumeDb, 4)}dB";

            return trunk.Filter.PrepareVersion(version, trunk.FilterQueue, new SetSourceIdQueue<Frame>(trunk.MixerAndUiFilterQueue, sourceId), new FilterSetup
            {
                Type = FilterContextFFMpeg.Type,
                FilterSpec = $"[in0]{final}[out]",
                InputSetups = new[]
                {
                        new FilterInputSetup( new FilterInputSpec
                            {
                                sample_rate = streamProps.CodecProps.sample_rate,
                                channel_layout = decoderCtx.Config.DecoderProperties.channel_layout,
                                sample_fmt = decoderCtx.Config.DecoderProperties.sample_fmt,
                                time_base = _time_base,
                            }) 
                },
                OutputSpec = new FilterOutputSpec
                {
                    sample_fmt = _default_sample_fmt,
                    sample_rate = audioEncoderTrunkConfig.sample_rate,
                    channel_layout = _default_channel_layout
                },
            });
        }

        private PixelFormatGroup DecideOnPixelFormat(ClientStreamerConfig c)
        {
            int yuv = 1; // yuv works a bit faster
            int rgb = 0;

            foreach (var vt in VideoInputTrunks)
            {
                var config = c.VideoInputTrunks.FirstOrDefault(s => s.Id == vt.Id);
                if (config != null)
                {
                    var filterPixel = FFMpegFilters.GetFilterPixelFormat(config.FilterChain);
                    if (filterPixel == FilterPixelFormat.Rgb)
                        rgb++;
                    else if (filterPixel == FilterPixelFormat.Yuv)
                        yuv++;
                    else if (vt.Detail is VideoInputTrunkFull full)
                    {
                        var cfg = full.Input.CurrentContext?.Config;
                        if (cfg != null)
                        {
                            var inputFormat = cfg.InputStreamProps[0].CodecProps.format;

                            if (Core.Const2.Rgb.ConcatFormats.Any(s => s == inputFormat))
                                rgb++;
                            else if (Core.Const2.Yuv420.ConcatFormats.Any(s => s == inputFormat))
                                yuv++;
                        }
                    }
                }
            }

            var filterPixelEnc = FFMpegFilters.GetFilterPixelFormat(c.VideoEncoderTrunk.FilterChain);
            if (filterPixelEnc == FilterPixelFormat.Rgb)
                rgb++;
            else if (filterPixelEnc == FilterPixelFormat.Yuv)
                yuv++;
            else
                yuv++; // encoder preference


            if (_dx != null)
                return Core.Const2.Rgb;
            else if (yuv >= rgb)
                return Core.Const2.Yuv420;
            else
                return Core.Const2.Rgb;
        }

        private IEncoderContext UpdateVideo(UpdateVersionContext update, ClientStreamerConfig c)
        {
            UpdateDirectXContext(update, c);

            int fps = c.VideoEncoderTrunk.FPS;
            var pixelFormatGroup = DecideOnPixelFormat(c);
            IEncoderContext encoderCtx = null;
            if (!c.VideoEncoderTrunk.ReceiverMode)
                encoderCtx = UpdateVideoEncoder(update, c.VideoEncoderTrunk, c.BitrateDrcRatio);
            
            List<VideoBlenderInputDescription> videoSources = new List<VideoBlenderInputDescription>();

            var orderedTrunks = c.VideoInputTrunks.OrderBy(s => s.ZOrder).ToList();
            bool portaitMode = false;

            foreach (var trunkConfig in c.VideoInputTrunks)
            {
                var trunkId = trunkConfig.Id;
                var trunkRoot = VideoInputTrunks.FirstOrDefault(s => s.Id == trunkId);
                if (trunkRoot == null)
                {
                    trunkRoot = new Trunk { Id = trunkId };
                    VideoInputTrunks.Add(trunkRoot);
                }

                var sourceId = videoSources.Count;
                bool firstBackground = trunkConfig == orderedTrunks[0] && trunkConfig.PositionRect.IsFullScreen() && trunkConfig.Visible;
                int[] outputPixelFormats = firstBackground ? pixelFormatGroup.MainFormats : pixelFormatGroup.OverlayFormats;

                int scaledWidth = (int)(c.VideoEncoderTrunk.EncoderSpec.width * trunkConfig.PositionRect.Width);
                int scaledHeight = (int)(c.VideoEncoderTrunk.EncoderSpec.height * trunkConfig.PositionRect.Height);

                VideoBlenderInputDescription blenderDesc = null;
                if (trunkConfig.Detail is VideoInputConfigFull videoInputConfigFull)
                {
                    var trunk = GetOrCreateTrunkDetail(trunkRoot, () => new VideoInputTrunkFull(trunkId, this, () => InputChanged()));
                    var fpsQueue = UpdateInputFpsQueue(update, trunk, videoInputConfigFull, trunkId, fps);

                    var inputCtx = trunk.Input.PrepareVersion(update, fpsQueue, PrepareVideoInputSetup(videoInputConfigFull.Setup, fps, trunk.Input.CurrentContext, c));
                    if (inputCtx != null)
                    {
                        var blenderQueue = new SetSourceIdQueue<Frame>(VideoEncoderTrunk.BlenderQueue, sourceId);
                        var streamProps = inputCtx.Config.InputStreamProps[0];
                        int decoders = (streamProps.CodecProps.codec_id == Core.Const.CODEC_ID_MJPEG) ? 3 : 1;

                        if (c.VideoEncoderTrunk.ReceiverMode)
                        {
                            portaitMode = streamProps.CodecProps.width < streamProps.CodecProps.height; // support of preview from mobile
                            if (portaitMode)
                            {
                                var tmp = scaledWidth;
                                scaledWidth = scaledHeight;
                                scaledHeight = tmp;
                            }
                        }

                        int outputPixelFormat = -1;
                        bool inputIsVFlipped = IsInputVFlipped(streamProps, videoInputConfigFull.Setup);
                        var filterChain = PrepareFilterChain(trunkConfig.FilterChain, inputIsVFlipped);

                        if (_dx != null)
                        {
                            if (streamProps.CodecProps.codec_id == Core.Const.CODEC_ID_RAWVIDEO && DirectXUploader.IsFormatSupportedForDecoderUpload(streamProps.CodecProps.format))
                            {
                                // [decoder-uploader]->[blender]
                                // we know how to process "raw video" format
                                trunk.DecoderPool.PrepareVersion(update, 1, trunk.DecoderQueue, 
                                    (v, s) => s.PrepareVersion(v, null, blenderQueue, new DecoderSetup(DecoderContextDirectXUpload.Type, streamProps.CodecProps, _dx)));
                            }
                            else if (streamProps.CodecProps.codec_id == Core.Const.CODEC_ID_RAWVIDEO && 
                                        (streamProps.CodecProps.format == Core.PIX_FMT_INTERNAL_DIRECTX ||
                                         streamProps.CodecProps.format == Core.Const.PIX_FMT_BGRA))
                            {
                                // [decoder-pass]->[blender]
                                // we know how to process "raw video" format
                                trunk.DecoderPool.PrepareVersion(update, 1, trunk.DecoderQueue,
                                    (v, s) => s.PrepareVersion(v, null, blenderQueue, new DecoderSetup(DecoderContextDirectXPassThru.Type, streamProps.CodecProps, _dx)));
                            }
                            else
                            {
                                // need to decode/resample?
                                var decoder = trunk.DecoderPool.PrepareVersion(update, decoders, trunk.DecoderQueue,
                                    (v, s) => s.PrepareVersion(v, null, trunk.FilterQueue, new DecoderSetup(DecoderContextFFMpeg.Type, streamProps.CodecProps, null)));
                                var dc = decoder.Config;


                                //[decoder]->[to rgb]->[upload passthru]->[blender]
                                //UpdateVideoFilterForDirectXUpload(update, trunk, dc, Core.Const.PIX_FMT_BGRA);
                                //trunk.Filter2.PrepareVersion(update, trunk.Filter2Queue, blenderQueue, new FilterSetup
                                //{
                                //    Type = FilterContextDirectXPassThru.Type,
                                //    DirectXContext = _dx,
                                //    InputSetups = new[] { new FilterInputSetup(new FilterInputSpec { width = dc.CodecProperties.width, height = dc.CodecProperties.height }) }
                                //});


                                int requiredPixelFormat = decoder.Config.DecoderProperties.pix_fmt;
                                if (DirectXUploader.IsFormatSupportedForFilterUpload(requiredPixelFormat)) 
                                {
                                    //[decoder]->[null]->[upload]->[blender]
                                    trunk.FilterPool.PrepareVersion(update, 1, trunk.FilterQueue, (v, s) => s.PrepareVersion(v, null, trunk.Filter2Queue, new FilterSetup { Type = FilterContextNull.Type}));
                                }
                                else
                                {
                                    //[decoder]->[change_pix_fmt]->[upload]->[blender]
                                    requiredPixelFormat = Core.Const.PIX_FMT_YUYV422;
                                    UpdateVideoFilterForDirectXUpload(update, trunk, dc, requiredPixelFormat);
                                }

                                trunk.Filter2.PrepareVersion(update, trunk.Filter2Queue, blenderQueue, new FilterSetup { Type = FilterContextDirectXUpload.Type, DirectXContext = _dx,
                                    InputSetups = new[] { new FilterInputSetup(new FilterInputSpec { pix_fmt = requiredPixelFormat, width = dc.CodecProperties.width, height = dc.CodecProperties.height }) }});
                            }
                        }
                        else
                        {
                            //[decoder]->[change_pix_fmt+scale+filter]->[null]->[blender]
                            // full software
                            var decoder = trunk.DecoderPool.PrepareVersion(update, decoders, trunk.DecoderQueue,
                                    (v, s) => s.PrepareVersion(v, null, trunk.FilterQueue, new DecoderSetup(DecoderContextFFMpeg.Type, streamProps.CodecProps, null)));

                            var inputPixelFormat = decoder.Config.DecoderProperties.pix_fmt;
                            outputPixelFormat = outputPixelFormats.Any(s => s == inputPixelFormat) ? inputPixelFormat : outputPixelFormats[0];
                            UpdateVideoFilterForFFMpeg(update, trunk, trunkConfig, decoder.Config, scaledWidth, scaledHeight, outputPixelFormat, filterChain);

                            trunk.Filter2.PrepareVersion(update, trunk.Filter2Queue, blenderQueue, new FilterSetup { Type = FilterContextNull.Type });
                        }

                        blenderDesc = new VideoBlenderInputDescription
                        {
                            PixelFormat = outputPixelFormat,
                            Behavior = CreateMergerBehavior(videoInputConfigFull.Setup),
                            FilterChain = filterChain
                        };
                    }
                }
                else if (trunkConfig.Detail is VideoInputConfigSingleFrame videoInputConfigSingleFrame)
                {
                    var trunkImpl = GetOrCreateTrunkDetail(trunkRoot, () => new VideoInputTrunkSingleFrame());

                    trunkImpl.FixedFrame.Update(videoInputConfigSingleFrame.Data, new FixedFrameConfig(scaledWidth, scaledHeight, outputPixelFormats[0], _dx), this);

                    if (trunkImpl.FixedFrame.Frame != null)
                    {
                        blenderDesc = new VideoBlenderInputDescription
                        {
                            PixelFormat = outputPixelFormats[0],
                            FixedFrame = trunkImpl.FixedFrame.Frame.AddRef(),
                            Behavior = new VideoBlenderInputBehavior(100, -1),
                            FilterChain = trunkConfig.FilterChain
                        };
                    }
                }

                if (blenderDesc != null)
                {
                    blenderDesc.Id = trunkId;
                    blenderDesc.SourceId = videoSources.Count;
                    blenderDesc.Rect = trunkConfig.PositionRect;
                    blenderDesc.Ptz = trunkConfig.PtzRect;
                    blenderDesc.Visible = trunkConfig.Visible;
                    blenderDesc.ZOrder = trunkConfig.ZOrder;
                    videoSources.Add(blenderDesc);
                }
            }


            VideoInputTrunks
                .Where(s => c.VideoInputTrunks.FirstOrDefault(vit => vit.Id == s.Id) == null)
                .ToList()
                .ForEach(s =>
                {
                    Core.LogInfo($"Disposing video {s.Id}");
                    VideoInputTrunks.Remove(s);
                    s.Dispose();
                });

            var outputFormat = UpdateVideoMixingFilter(update, videoSources, encoderCtx, c.VideoEncoderTrunk, pixelFormatGroup, portaitMode);

            UpdateUiFrameOutput(update, c, encoderCtx, outputFormat, portaitMode);
            return encoderCtx;
        }

        private VideoFilterChainDescriptor PrepareFilterChain(VideoFilterChainDescriptor filterChain, bool inputIsVFlipped)
        {
            if (!inputIsVFlipped)
                return filterChain;
            else
            {
                if (filterChain == null || filterChain.Filters == null)
                    return new VideoFilterChainDescriptor(new[] { new VideoFilterDescriptor(VideoFilterType.VFlip, 1.0, null)});

                if (filterChain.HasVFlip())
                    return new VideoFilterChainDescriptor(filterChain.Filters.Where(s => s.Type != VideoFilterType.VFlip).ToArray());
                else
                    return new VideoFilterChainDescriptor(filterChain.Filters.Concat(new[] { new VideoFilterDescriptor(VideoFilterType.VFlip, 1.0, null) }).ToArray());
            }
        }

        private bool IsInputVFlipped(InputStreamProperties streamProps, InputSetup setup)
        {
            return setup.Input?.Contains("EOS Webcam Utility") == true;
            //return streamProps.CodecProps.extradata_size == 9 &&
            //     streamProps.CodecProps.extradata[0] == 66 && // "BottomUp" string is encoded here
            //     streamProps.CodecProps.extradata[1] == 111 && streamProps.CodecProps.extradata[2] == 116 && streamProps.CodecProps.extradata[3] == 116 && streamProps.CodecProps.extradata[4] == 111 && streamProps.CodecProps.extradata[5] == 109 &&
            //     streamProps.CodecProps.extradata[6] == 85 && streamProps.CodecProps.extradata[7] == 112 && streamProps.CodecProps.extradata[8] == 0;
        }

        private void UpdateVideoFilterForDirectXUpload(UpdateVersionContext update, VideoInputTrunkFull trunk, DecoderConfig dc, int outputPixelFormat)
        {
            trunk.FilterPool.PrepareVersion(update, 2, trunk.FilterQueue, (v, s) => s.PrepareVersion(v, null, trunk.Filter2Queue, new FilterSetup
            {
                Type = FilterContextFFMpeg.Type,
                FilterSpec = $"[in0]null[out]",
                InputSetups = new[]
                {
                    new FilterInputSetup(new FilterInputSpec
                            {
                                time_base = _time_base,
                                pix_fmt =  dc.DecoderProperties.pix_fmt,
                                width = dc.CodecProperties.width,
                                height = dc.CodecProperties.height,
                                sample_aspect_ratio = _sample_aspect_ratio,
                                color_range = dc.CodecProperties.color_range
                            })
                },
                OutputSpec = new FilterOutputSpec
                {
                    pix_fmt = outputPixelFormat
                }
            }));
        }

        private ITargetQueue<Packet> UpdateInputFpsQueue(UpdateVersionContext update, VideoInputTrunkFull trunkImpl, VideoInputConfigFull videoInputConfigFull, string trunkId, int fps)
        {
            if (trunkImpl.InputFpsLimitQueue == null || trunkImpl.InputFpsLimitQueue.Fps != fps)
                trunkImpl.InputFpsLimitQueue = new FpsQueue<Packet>(new NodeName("V", trunkId, "Ifps", 1), trunkImpl.DecoderQueue, this, PacketPool, fps, 3, _time_base, _overloadController, -1);

            if (!videoInputConfigFull.Setup.UseFpsQueue)
            {
                return trunkImpl.DecoderQueue;
            }
            else
            {
                update.RuntimeConfig.Add(trunkImpl.InputFpsLimitQueue, null);
                return trunkImpl.InputFpsLimitQueue;
            }
        }

        private void UpdateDirectXContext(UpdateVersionContext update, ClientStreamerConfig c)
        {
            if (!c.VideoRenderOptions.Equals(VideoRenderOptions))
            {
                VideoRenderOptions = c.VideoRenderOptions;

                _dx?.Pool.Deactivate();
                _dx?.RemoveRef();
                if (!_dxSwitchedOff)
                    _dx = DirectXContextFactory.Create(c.VideoRenderOptions, this);
                else
                    _dx = null;
            }
            update.RuntimeConfig.Dx = _dx;
            update.RuntimeConfig.EnableObjectTracking = c.VideoRenderOptions.EnableObjectTracking;
        }

        public override void ReinitDirectX()
        {
            Log.Information($"ReinitDirectX requested ({_dxFailureCounter5Minutes + 1} in 5 min)");
            int counter = 0;
            lock (this)
            {
                _dxFailureCounter++;
                _dxFailureCounter5Minutes++;
                counter = _dxFailureCounter;

                if (_dxFailureCounter5Minutes > 10)
                    _qsvNvSwitchedOff = true;
                if (_dxFailureCounter5Minutes > 20)
                    _dxSwitchedOff = true;
            }

            TuneConfig(c =>
            {
                return c with
                {
                    VideoRenderOptions = new VideoRenderOptions(c.VideoRenderOptions.Type, c.VideoRenderOptions.Adapter, c.VideoRenderOptions.MainWindowHandle, c.VideoRenderOptions.EnableObjectTracking, counter)
                };
            });

        }

        public override int GetDxFailureCounter()
        {
            lock (this)
            {
                return _dxFailureCounter;
            }
        }

        private InputSetup PrepareVideoInputSetup(InputSetup modelSetup, int fps, IInputContext currentContext, ClientStreamerConfig c)
        {
            var result = new InputSetupNoneResetingOptions
            {
                Fps = fps,
            };

            var objectBaseInput = modelSetup.ObjectInput;

            var needdx = modelSetup.Type == ScreenCaptureContext.Name || modelSetup.Type == WebBrowserContext.Name || modelSetup.Type == PluginContext.PluginName;
            var dx = needdx ? _dx : null;

            if (modelSetup.Type == ScreenCaptureContext.Name )
            {
                var config = currentContext?.Config;
                if (config != null)
                {
                    var str = config.InputStreamProps[0];
                    result.LoopbackOptions = new LoopbackOptions { Width = str.CodecProps.width, Height = str.CodecProps.height };
                }
            }
            else if (modelSetup.Type == PluginContext.PluginName)
            {
                var wb = (WebBrowserContextSetup)objectBaseInput;
                if (c.VideoEncoderTrunk.EncoderSpec.height <= 720)
                    wb = wb with { PageHeight = 720, PageWidth = 1280 };
                else
                    wb = wb with { PageHeight = 1080, PageWidth = 1920 };
                objectBaseInput = wb;
            }
            return modelSetup with { NoneResetingOptions = result, Dx = dx, AdjustInputType = AdjustInputType.Adaptive, ObjectInput = objectBaseInput };
        }


        private VideoBlenderInputBehavior CreateMergerBehavior(InputSetup setup)
        {
            if (setup.Type == ScreenCaptureContext.Name) 
                return new VideoBlenderInputBehavior(150, 1000);
            else if (setup.Type == WebBrowserContext.Name)
                return new VideoBlenderInputBehavior(150, -1);
            else if (setup.Type == PluginContext.PluginName)
                return new VideoBlenderInputBehavior(150, -1);
            else
                return new VideoBlenderInputBehavior(150, 1000);
        }

        private IEncoderContext UpdateVideoEncoder(UpdateVersionContext version, VideoEncoderTrunkConfig c, double bitrateDrcRatio)
        {
            var hwCheck = _hardwareEncoderCheck.GetResult();

            var settings = GetEncoderSettings(c.EncoderType, c.EncoderQuality, c.EnableQsvNv12Optimization, hwCheck);
            bool soft = settings.name == "libx264";
            var options = settings.options + $"{Core.Sep}g{Core.Eq}{c.FPS * 2}{Core.Sep}keyint_min{Core.Eq}{c.FPS + 1}";

            var baseBitrate = bitrateDrcRatio * c.Bitrate;
            var encoderBitrate = new EncoderBitrate { bit_rate = (int)baseBitrate, max_rate = (int)baseBitrate, buffer_size = (int)(1.2 * baseBitrate) };
            bool dynamicBitrate = false;

            if (soft)
            {
                if (c.EncoderPreferNalHdr)
                {
                    options += Core.Sep +
                                $"x264-params{Core.Eq}filler=true:nal-hrd=cbr:force-cfr=1" +
                                Core.Sep +
                                GetVideoEncoderBitrateOptions(encoderBitrate);

                    encoderBitrate = new EncoderBitrate(); // reset
                }
                else
                {
                    options += Core.Sep + $"x264-params{Core.Eq}filler=true:force-cfr=1";
                    dynamicBitrate = true;
                }
            }
            else
            {
                if (settings.type != EncoderContextQsvDx.TypeName)
                {
                    options += Core.Sep + GetVideoEncoderBitrateOptions(encoderBitrate);
                    encoderBitrate = new EncoderBitrate(); // reset
                }
                else
                    dynamicBitrate = true;
            }

            bool supportsEnforcingIFrame = settings.name != "h264_qsv"; // qsv gets crazy if we enforcing Iframes.

            return VideoEncoderTrunk.EncoderNode.PrepareVersion(
                version, 
                VideoEncoderTrunk.EncoderQueue,
                new ChangeTimeBaseQueue<Packet>(OutputQueue, new AVRational { num = 1, den = c.FPS }, _time_base),
                new EncoderSetup
                {
                    Type = settings.type,
                    Name = settings.name,
                    Options = options,
                    DirectXContext = _dx,
                    EncoderBitrate = encoderBitrate,
                    SupportsEnforcingIFrame = supportsEnforcingIFrame,
                    EncoderSpec = new EncoderSpec
                    {
                        sample_aspect_ratio = _sample_aspect_ratio,
                        width = c.EncoderSpec.width,
                        height = c.EncoderSpec.height,
                        Quality = c.EncoderSpec.Quality,
                        time_base = new AVRational { num = 1, den = c.FPS }
                    }
                }, 
                (ec, same) =>
                {
                    if (same && dynamicBitrate)
                        ec.UpdateBitrate(encoderBitrate);
                }, soft ? int.MaxValue : 2);
        }

        private (string type, string name, string options) GetEncoderSettings(VideoEncoderType type, VideoEncoderQuality quality, bool enableQsvNv12Optimization, HardwareEncoderCheckResult hwCheck)
        {
            string lt = null;

            if (type != VideoEncoderType.Software)
            {
                var vendor = _dx?.AdapterInfo?.Vendor ?? AdapterVendor.NVidia;

                if (vendor == AdapterVendor.Intel)
                {
                    if (hwCheck.Qsv_nv12 && enableQsvNv12Optimization && !_qsvNvSwitchedOff)
                        return (EncoderContextQsvDx.TypeName, "", "");
                    if (hwCheck.Qsv)
                        lt = "qsv";
                }
                else if (vendor == AdapterVendor.NVidia)
                {
                    if (hwCheck.Nv)
                        lt = "nv";
                }
                else if (vendor == AdapterVendor.Other)
                {
                    if (hwCheck.Amd)
                        lt = "amd";
                }

                if (lt == null)
                {
                    if (hwCheck.Qsv) lt = "qsv";
                    else if (hwCheck.Nv) lt = "nv";
                    else if (hwCheck.Amd) lt = "amd";
                }
            }

            var settings = GetEncoderSettings(lt, quality);
            return ("", settings.name, settings.options);
        }

        private (string name, string options) GetEncoderSettings(string lt, VideoEncoderQuality quality) => lt switch
            {
                "qsv" => quality switch
                {
                    VideoEncoderQuality.Speed =>            ("h264_qsv", $"bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}preset{Core.Eq}fast"),
                    VideoEncoderQuality.Balanced =>         ("h264_qsv", $"bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}preset{Core.Eq}medium"),
                    VideoEncoderQuality.BalancedQuality =>  ("h264_qsv", $"bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}preset{Core.Eq}medium"),
                    VideoEncoderQuality.Quality =>          ("h264_qsv", $"bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}preset{Core.Eq}slow"),
                    _ => throw new NotSupportedException()
                },
                "nv" => quality switch
                {
                    VideoEncoderQuality.Speed =>            ("h264_nvenc", $"bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}rc{Core.Eq}cbr_ld_hq{Core.Sep}zerolatency{Core.Eq}1{Core.Sep}preset{Core.Eq}llhp"),
                    VideoEncoderQuality.Balanced =>         ("h264_nvenc", $"bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}rc{Core.Eq}cbr_ld_hq{Core.Sep}zerolatency{Core.Eq}1{Core.Sep}preset{Core.Eq}ll"),
                    VideoEncoderQuality.BalancedQuality =>  ("h264_nvenc", $"bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}rc{Core.Eq}cbr_ld_hq{Core.Sep}zerolatency{Core.Eq}1{Core.Sep}preset{Core.Eq}ll"),
                    VideoEncoderQuality.Quality =>          ("h264_nvenc", $"bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}rc{Core.Eq}cbr_ld_hq{Core.Sep}zerolatency{Core.Eq}1{Core.Sep}preset{Core.Eq}llhq"),
                    _ => throw new NotSupportedException()
                },

                "amd" => quality switch
                {
                    VideoEncoderQuality.Speed => ("h264_amf", $"usage{Core.Eq}webcam{Core.Sep}bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}rc{Core.Eq}cbr{Core.Sep}quality{Core.Eq}speed"),
                    VideoEncoderQuality.Balanced => ("h264_amf", $"usage{Core.Eq}webcam{Core.Sep}bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}rc{Core.Eq}cbr{Core.Sep}quality{Core.Eq}balanced"),
                    VideoEncoderQuality.BalancedQuality => ("h264_amf", $"usage{Core.Eq}webcam{Core.Sep}bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}rc{Core.Eq}cbr{Core.Sep}quality{Core.Eq}balanced"),
                    VideoEncoderQuality.Quality => ("h264_amf", $"usage{Core.Eq}webcam{Core.Sep}bf{Core.Eq}0{Core.Sep}profile{Core.Eq}main{Core.Sep}rc{Core.Eq}cbr{Core.Sep}quality{Core.Eq}quality"),
                    _ => throw new NotSupportedException()
                },
                _ => quality switch
                {
                    VideoEncoderQuality.Speed => ("libx264", $"tune{Core.Eq}zerolatency{Core.Sep}preset{Core.Eq}ultrafast"),
                    VideoEncoderQuality.Balanced => ("libx264", $"tune{Core.Eq}zerolatency{Core.Sep}preset{Core.Eq}superfast"),
                    VideoEncoderQuality.BalancedQuality => ("libx264", $"tune{Core.Eq}zerolatency{Core.Sep}preset{Core.Eq}veryfast"),
                    VideoEncoderQuality.Quality => ("libx264", $"tune{Core.Eq}zerolatency{Core.Sep}preset{Core.Eq}faster"),
                    _ => throw new NotSupportedException()
                }
            };



        private string GetVideoEncoderBitrateOptions(EncoderBitrate c)
        {
            return $"bufsize{Core.Eq}{c.buffer_size}k{Core.Sep}" +
                   $"maxrate{Core.Eq}{c.max_rate}k{Core.Sep}" +
                   $"b{Core.Eq}{c.bit_rate}k";
        }
       

        private T GetOrCreateTrunkDetail<T>(Trunk trunk, Func<T> creator) where T: class, IDisposable
        {
            if (trunk.Detail is not T trunkImpl)
            {
                trunkImpl = creator();
                trunk.Detail?.Dispose();
                trunk.Detail = trunkImpl;
            }
            return trunkImpl;
        }

        private int UpdateVideoMixingFilter(UpdateVersionContext update, List<VideoBlenderInputDescription> videoSources, IEncoderContext encoderCtx, VideoEncoderTrunkConfig config, PixelFormatGroup pixelFormatGroup, bool portaitMode)
        {
            int h = portaitMode ? config.EncoderSpec.width : config.EncoderSpec.height;
            int w = portaitMode ? config.EncoderSpec.height : config.EncoderSpec.width;
            int encoderRequiredPixFmt = encoderCtx?.Config.EncoderProps.pix_fmt ?? Core.Const.PIX_FMT_YUV420P;

            var ordered = videoSources.OrderBy(s => s.ZOrder).ToList();
            bool addBackround = ordered.Count > 0 ? !(ordered[0].Rect.IsFullScreen() && ordered[0].Visible) : true;
            var blenderOutputPixelFormat = _dx != null ? -1 : (addBackround ? pixelFormatGroup.MainFormats[0] : ordered[0].PixelFormat);

            VideoEncoderTrunk.BackgroundFrame.Update(config.Background, new FixedFrameConfig(w, h, pixelFormatGroup.MainFormats[0], _dx), this);

            var inputs = videoSources.ToList();

            if (addBackround)
                inputs.Insert(0, new VideoBlenderInputDescription
                {
                    Id = "background",
                    SourceId = -1,
                    Rect = PositionRect.Full,
                    Ptz = PositionRect.Full,
                    Visible = true,
                    ZOrder = -1,
                    PixelFormat = pixelFormatGroup.MainFormats[0],
                    FixedFrame = VideoEncoderTrunk.BackgroundFrame.Frame?.AddRef(),
                    Behavior = new VideoBlenderInputBehavior(100, -1)
                });
            
            VideoEncoderTrunk.Blender.PrepareVersion(
                update,
                VideoEncoderTrunk.BlenderQueue,
                VideoEncoderTrunk.EncoderAndUiFilterDuplicateQueue,
                new VideoBlenderSetup(w, h, config.FPS, 3, // 3 frames
                                                        3, // 3 frames
                                                        20_000_000, // 2 sec
                                                        3_000_000, //300 ms -> should more than 3 frames and less 2 sec
                                                        blenderOutputPixelFormat, config.BlendingType, _dx, 
                                                        new VideoBlenderSetupWeakOptions
                                                        {
                                                            PixelFormatGroup = pixelFormatGroup,
                                                            Inputs = inputs.ToArray(),
                                                            NoSignalData = config.NoSignal,
                                                            FilterChain = config.FilterChain
                                                        },
                                                        -60));

            if (_dx != null )
            {
                if (encoderCtx == null)
                {
                    VideoEncoderTrunk.PreEncoderFilterPool.PrepareVersion(update, 1, VideoEncoderTrunk.PreEncoderFilterQueue, (v, s) => s.PrepareVersion(v, null, VideoEncoderTrunk.EncoderQueue, new FilterSetup { Type = FilterContextNull.Type }));
                }
                else if (encoderCtx.Config.EncoderProps.pix_fmt == Core.PIX_FMT_INTERNAL_DIRECTX)
                {
                    VideoEncoderTrunk.PreEncoderFilterPool.PrepareVersion(update, 1, VideoEncoderTrunk.PreEncoderFilterQueue,
                    (v, s) => s.PrepareVersion(v, null, VideoEncoderTrunk.EncoderQueue,
                        new FilterSetup
                        {
                            Type = FilterContextDirectXTransform.Type,
                            DirectXContext = _dx,
                            InputSetups = new[] { new FilterInputSetup(new FilterInputSpec { width = w, height = h }) },
                            OutputSpec = new FilterOutputSpec { pix_fmt = Core.Const.PIX_FMT_NV12 }
                        }));
                }
                else
                {
                    VideoEncoderTrunk.PreEncoderFilterPool.PrepareVersion(update, 2, VideoEncoderTrunk.PreEncoderFilterQueue,
                         (v, s) => s.PrepareVersion(v, null, VideoEncoderTrunk.EncoderQueue,
                             new FilterSetup
                             {
                                 Type = FilterContextDirectXDownload.Type,
                                 DirectXContext = _dx,
                                 InputSetups = new[] { new FilterInputSetup(new FilterInputSpec { width = w, height = h }) },
                                 OutputSpec = new FilterOutputSpec { pix_fmt = encoderRequiredPixFmt }
                             }));
                }
                return Core.PIX_FMT_INTERNAL_DIRECTX;
            }
            else
            {
                if (config.FilterChain != null)
                    throw new InvalidOperationException("Not supported now");

                if (encoderRequiredPixFmt == blenderOutputPixelFormat)
                {
                    VideoEncoderTrunk.PreEncoderFilterPool.PrepareVersion(update, 1, VideoEncoderTrunk.PreEncoderFilterQueue, (v, s) => s.PrepareVersion(v, null, VideoEncoderTrunk.EncoderQueue, new FilterSetup { Type = FilterContextNull.Type }));
                }
                else
                {
                    VideoEncoderTrunk.PreEncoderFilterPool.PrepareVersion(update, 2, VideoEncoderTrunk.PreEncoderFilterQueue, (v, s) => s.PrepareVersion(v, null, VideoEncoderTrunk.EncoderQueue,
                        new FilterSetup
                    {
                        Type = FilterContextFFMpeg.Type,
                        FilterSpec = $"[in0]null[out]",
                        InputSetups = new[]{ new FilterInputSetup(new FilterInputSpec
                            {
                                time_base = _time_base,
                                pix_fmt = blenderOutputPixelFormat,
                                width = w,
                                height = h,
                                sample_aspect_ratio = _sample_aspect_ratio,
                                color_range = 0,
                            }) },
                        OutputSpec = new FilterOutputSpec
                        {
                            pix_fmt = encoderRequiredPixFmt
                        }
                    }));
                }

                return blenderOutputPixelFormat;
            }
        }

        

        private void UpdateUiFrameOutput(UpdateVersionContext version, ClientStreamerConfig c, IEncoderContext encoderContext, int afterMixPixelFormat, bool portaitMode)
        {
            //var changetimequeue = new ChangeTimeBaseQueue<Frame>(VideoEncoderTrunk.EncoderQueue, _time_base, new AVRational { num = 1, den = c.VideoEncoderTrunk.FPS });

            if (c.VideoEncoderTrunk.OnUiFrame != null)
            {
                int fpsLimit = 100;

                if (_dx == null || !_dx.AdapterIsEqualToWindowAdapter) // if soft rendering or display is different from adapter - do some limit
                    fpsLimit = 24;

                VideoEncoderTrunk.UiFpsFilterQueue = new FpsQueue<Frame>(new NodeName("VE", null, "FUIFps", 8), VideoEncoderTrunk.UiFilterQueue, this, this.FramePool, fpsLimit, 3, _time_base, null, version.Version);
                version.RuntimeConfig.Add(VideoEncoderTrunk.UiFpsFilterQueue, null);

                var changeTime = new ChangeTimeBaseQueue<Frame>(VideoEncoderTrunk.UiFpsFilterQueue, new AVRational { num = 1, den = c.VideoEncoderTrunk.FPS }, _time_base);

                if (c.VideoEncoderTrunk.ReceiverMode)
                    VideoEncoderTrunk.EncoderAndUiFilterDuplicateQueue.SetQueues(changeTime);
                else 
                    VideoEncoderTrunk.EncoderAndUiFilterDuplicateQueue.SetQueues(VideoEncoderTrunk.PreEncoderFilterQueue, changeTime);

                if (VideoEncoderTrunk.UiFilter == null)
                {
                    VideoEncoderTrunk.UiOut = new FrameOutput(this, c.VideoEncoderTrunk.OnUiFrame);
                    VideoEncoderTrunk.UiFilter = new FilterNode(new NodeName("VE", null, "FUI", 9), this);
                }

                if (_dx != null)
                {
                    VideoEncoderTrunk.UiFilter.PrepareVersion(version, VideoEncoderTrunk.UiFilterQueue, VideoEncoderTrunk.UiOut, 
                        new FilterSetup { Type = FilterContextNull.Type });
                }
                else
                {
                    int maxUiHeight = 1080;
                    int targetHeight = portaitMode ? c.VideoEncoderTrunk.EncoderSpec.width : c.VideoEncoderTrunk.EncoderSpec.height;
                    int targetWidth = portaitMode ? c.VideoEncoderTrunk.EncoderSpec.height : c.VideoEncoderTrunk.EncoderSpec.width;
                    string uiSpec = "null";
                    if (targetHeight > maxUiHeight)
                    {
                        // downscale & alignment
                        int width = (targetWidth * maxUiHeight) / targetHeight;
                        int widthAligned = (width / 32) * 32;
                        uiSpec = $"scale=w={widthAligned}:h={maxUiHeight}";
                    }
                    else if (targetWidth % 32 != 0)
                    {
                        // make alignment
                        uiSpec = $"scale=w={(targetWidth / 32) * 32}:h={targetHeight}";
                    }

                    VideoEncoderTrunk.UiFilter.PrepareVersion(version, VideoEncoderTrunk.UiFilterQueue, VideoEncoderTrunk.UiOut, new FilterSetup
                    {
                        Type = FilterContextFFMpeg.Type,
                        FilterSpec = $"[in0]{uiSpec}[out]",
                        InputSetups = new[]
                        {
                            new FilterInputSetup(new FilterInputSpec
                            {
                                time_base = _time_base,
                                pix_fmt = afterMixPixelFormat,
                                width = targetWidth,
                                height = targetHeight,
                                sample_aspect_ratio = _sample_aspect_ratio,
                                color_range = 1,
                            })
                            },
                        OutputSpec = new FilterOutputSpec
                        {
                            pix_fmt = Core.Const.PIX_FMT_BGR24 
                        }
                    });
                }
            }
            else //c.VideoEncoderTrunk.OnUiFrame != null
            {
                if (c.VideoEncoderTrunk.ReceiverMode)
                    VideoEncoderTrunk.EncoderAndUiFilterDuplicateQueue.SetQueues();
                else 
                    VideoEncoderTrunk.EncoderAndUiFilterDuplicateQueue.SetQueues(VideoEncoderTrunk.PreEncoderFilterQueue);

                if (VideoEncoderTrunk.UiFilter != null)
                {
                    VideoEncoderTrunk.UiFilter.Dispose();

                    VideoEncoderTrunk.UiFilter = null;
                    VideoEncoderTrunk.UiOut = null;
                }
            }
        }

        private IFilterContext UpdateVideoFilterForFFMpeg(UpdateVersionContext update, VideoInputTrunkFull trunk, VideoInputTrunkConfig trunkConfig, DecoderConfig decoderConfig, int scaledWidth, int scaledHeight, int outputPixelFormat,
            VideoFilterChainDescriptor filterChain)
        {
            var inputWidth = decoderConfig.CodecProperties.width;
            var inputHeight = decoderConfig.CodecProperties.height;
            int bestQuality = 0;

            string scale = "";
            if (!trunkConfig.PtzRect.IsFullScreen())
            {
                int x = (int)(inputWidth * trunkConfig.PtzRect.Left);
                int y = (int)(inputHeight * trunkConfig.PtzRect.Top);
                inputWidth = (int)(inputWidth * trunkConfig.PtzRect.Width);
                inputHeight = (int)(inputHeight * trunkConfig.PtzRect.Height);

                scale += $"crop=w={inputWidth}:h={inputHeight}:x={x}:y={y}";
            }
            if (scaledHeight != inputHeight || scaledWidth != inputWidth) 
                scale += (scale.Length == 0 ? "" : ", ") +  $"scale=w={scaledWidth}:h={scaledHeight}";
            string userFilter = FFMpegFilters.GetFFMpegFilterString(filterChain);
            string final;
            if (scale != "")
            {
                if (userFilter != "null")
                {
                    if (decoderConfig.CodecProperties.width * decoderConfig.CodecProperties.height > scaledWidth * scaledHeight)
                        final = $"{scale},{userFilter}";
                    else
                        final = $"{userFilter},{scale}";
                }
                else
                    final = scale;
            }
            else 
                final = userFilter;

            if (trunkConfig.Detail is VideoInputConfigFull dyn)
            {
                bool needQuality = dyn.Setup.Type == WebBrowserContext.Name || dyn.Setup.Type == PluginContext.PluginName || dyn.Setup.Type == ScreenCaptureContext.Name;
                bestQuality = needQuality ? 1 : 0;
            }

            return trunk.FilterPool.PrepareVersion(update, 2, trunk.FilterQueue, (v, s) => s.PrepareVersion(update, null, trunk.Filter2Queue, new FilterSetup
            {
                Type = FilterContextFFMpeg.Type,
                FilterSpec = $"[in0]{final}[out]",
                InputSetups = new[]
                {
                    new FilterInputSetup(new FilterInputSpec
                            {
                                time_base = _time_base,
                                pix_fmt =  decoderConfig.DecoderProperties.pix_fmt,
                                width = decoderConfig.CodecProperties.width,
                                height = decoderConfig.CodecProperties.height,
                                sample_aspect_ratio = _sample_aspect_ratio,
                                color_range = decoderConfig.CodecProperties.color_range,
                                BestQuality = bestQuality
                            })
                },
                OutputSpec = new FilterOutputSpec
                {
                    pix_fmt = outputPixelFormat
                }
            }));
        }

        private void InputChanged()
        {
            BlockingUpdate();
        }
    }
}
