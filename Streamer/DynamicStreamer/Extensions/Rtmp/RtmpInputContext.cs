using DynamicStreamer.Contexts;
using DynamicStreamer.Nodes;
using DynamicStreamer.Rtmp;
using Harmonic.Networking.Amf.Common;
using Harmonic.Networking.Rtmp.Messages;
using Harmonic.Networking.Rtmp.Messages.Commands;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

namespace DynamicStreamer.Extensions.Rtmp
{
    public class RtmpInputContext : IInputContext, IRtmpExtensions, IStatProvider
    {
        public static string Name = "rtmp";

        private IStreamerBase _streamer;
        private RtmpConnection _rtmpConnection;
        private int _bitrateLimit;
        private ManualResetEvent _analysed = new ManualResetEvent(false);
        private object _sync = new object();
        private bool _continueProcessing = true;
        private CodecProperties _audioProps;
        private CodecProperties _videoProps;
        private bool _commonPropsFound = false;
        private bool _videoExtradataFound = false;
        private bool _audioExtradataFound = false;
        private Queue<Packet> _queue = new Queue<Packet>();
        private IInputTimeAdjuster _timeAdjuster = new InputNetworkTimeAdjuster();
        private LinkedList<TrafficRecord> _traffic = new LinkedList<TrafficRecord>();
        private bool _unexpectedAudioLogged = false;
        private StatisticKeeper<StatisticDataOfInputOutput> _statisticKeeper;
        private bool _gracefullyClosed;

        public RtmpInputContext(IStreamerBase streamer)
        {
            _streamer = streamer;
            _statisticKeeper = new StatisticKeeper<StatisticDataOfInputOutput>(new NodeName(""));
        }

        public InputConfig Config { get; set;}

        NodeName IRuntimeItem.Name => throw new NotImplementedException();

        public void Analyze(int duration, int streamsCount)
        {
            if (!_analysed.WaitOne(duration + 1500))
                throw new DynamicStreamerException("DataMessage is not yet received");
        }

        public void Dispose()
        {
            _analysed?.Dispose();
            _analysed = null;
            CloseConnection();
        }

        private void CloseConnection()
        {
            _rtmpConnection?.Kill();
            _rtmpConnection = null;
            _commonPropsFound = false;
            _videoExtradataFound = false;
            _audioExtradataFound = false;
        }

        public void Interrupt()
        {
            lock(_sync)
            {
                _continueProcessing = false;
                Monitor.PulseAll(_sync);
                _analysed.Set();
            }
        }

        public void Open(InputSetup setup)
        {
            CloseConnection();

            var data = setup.RtmpTransferData;
            if (data == null)
                throw new DynamicStreamerException("RtmpTransferData is not initialized");
            var socket = new Socket(data.SocketInformation);
            _rtmpConnection = new RtmpConnection(data.State, socket, this, $"{RtmpConnection.GetRemoteEndPoint(socket)}/rtmp", "Device");
            _bitrateLimit = setup.BitrateLimit;
            _rtmpConnection.Start();
        }

        public void Read(Packet packet, InputSetup inputSetup)
        {
            lock (_sync)
            {
                while (_continueProcessing && _queue.Count == 0)
                    Monitor.Wait(_sync);

                if (_queue.Count > 0)
                {
                    var dq = _queue.Dequeue();
                    if (dq == null)
                    {
                        _gracefullyClosed = true;
                        throw new GracefulCloseException();
                    }

                    packet.CopyContentFrom(dq);
                    _streamer.PacketPool.Back(dq);
                }
                else
                    throw new OperationCanceledException();
            }

            _statisticKeeper.UpdateData(d => d.AddPacket(packet.Properties.Size, packet.Properties.StreamIndex == 0));
        }

        void IRtmpExtensions.HandleDataMessage(RtmpConnection connection, DataMessage message)
        {
            try
            {
                if (_commonPropsFound)
                    throw new InvalidOperationException("Second data message unexpected");

                var props = message.Data.Select(s => s as Dictionary<string, object>).FirstOrDefault(s => s != null);

                if (props == null)
                {
                    Log.Information("Properties not found, trying Amf object");
                    props = message.Data.Select(s => s as AmfObject).FirstOrDefault(s => s != null)?.FieldsDictionary;
                }

                if (props == null)
                    throw new InvalidOperationException("Properties not found");

                _videoProps = GetVideoProps(props);
                _audioProps = GetAudioProps(props);
                _commonPropsFound = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to process data message");
            }
        }

        private CodecProperties GetAudioProps(Dictionary<string, object> props)
        {
            if (TryGetVal(props, "audiodatarate", out var audiodatarate) &&
                CheckAudioCodec(props))
            {
                if (!TryGetVal(props, "audiosamplerate", out var audiosamplerate))
                {
                    Log.Warning("audiosamplerate is not set, taking 44100");
                    audiosamplerate = 44100;
                }
                else
                {
                    if (audiosamplerate == 48) // go pro uses this
                    {
                        Log.Warning("audiosamplerate is set to 48, taking 48000. Possible stream is from GoPro");
                        audiosamplerate = 48000;
                    }
                    else if (audiosamplerate == 44) // go pro uses this
                    {
                        Log.Warning("audiosamplerate is set to 44, taking 44100. Possible stream is from GoPro");
                        audiosamplerate = 44100;
                    }

                }

                if (!TryGetVal(props, "audiosamplesize", out var audiosamplesize))
                {
                    Log.Warning("audiosamplesize is not set, taking 32");
                    audiosamplesize = 32;
                }

                if (!TryGetVal(props, "audiochannels", out var audiochannels))
                {
                    if (props.TryGetValue("stereo", out var valueBool) && valueBool is bool resBool)
                    {
                        audiochannels = resBool ? 2 : 1;
                    }
                    else if (props.TryGetValue("stereo", out var valueStr) && valueStr is string resString)
                    {
                        audiochannels = resString.ToLower() == "true" ? 2 : 1;
                    }
                    else
                    {
                        Log.Warning("audiochannels is not set, taking 2");
                        audiochannels = 2;
                    }
                }

                return new CodecProperties
                {
                    codec_type = AVMediaType.AVMEDIA_TYPE_AUDIO,
                    codec_id = Core.Const.CODEC_ID_AAC,
                    sample_rate = audiosamplerate,
                    bits_per_coded_sample = audiosamplesize,
                    bit_rate = audiodatarate * 1000,
                    channels = audiochannels,
                    channel_layout = audiochannels == 1 ? 1ul : 3ul,
                    format = 8,
                    frame_size = 1024,
                };
            }
            else
            {
                Log.Warning("Failed to find audio properties");
                return new CodecProperties { codec_id = Core.NoStream };
            }
        }

        private CodecProperties GetVideoProps(Dictionary<string, object> props)
        {
            if (TryGetVal(props, "width", out var width) &&
                TryGetVal(props, "height", out var height) &&
                TryGetVal(props, "videodatarate", out var videodatarate) &&
                CheckVideoCodec(props))
                return new CodecProperties
                    {
                        codec_type = AVMediaType.AVMEDIA_TYPE_VIDEO,
                        codec_id = Core.Const.CODEC_ID_H264,
                        width = width,
                        height = height,
                        bit_rate = videodatarate * 1000,
                        bits_per_raw_sample = 8,
                        format = Core.Const.PIX_FMT_YUV420P,

                        chroma_location = 1,
                        color_primaries = 2,
                        color_space = 2,
                        color_trc = 2,
                        field_order = 1,
                        level = 40,
                        profile = 100,
                    };

            throw new InvalidOperationException("Failed to find video properties");
        }

        private bool CheckVideoCodec(Dictionary<string, object> props)
        {
            if (TryGetVal(props, "videocodecid", out var codec) && codec == 7)
                return true;

            if (props.TryGetValue("videocodecid", out var value) && value is string res && res == "avc1")
                return true;
            return false;
        }


        private bool CheckAudioCodec(Dictionary<string, object> props)
        {
            if (TryGetVal(props, "audiocodecid", out var codec) && codec == 10)
                return true;

            if (props.TryGetValue("audiocodecid", out var value) && value is string res && res == "mp4a")
                return true;
            return false;
        }

        private bool TryGetVal(Dictionary<string, object> props, string key, out int r)
        {
            if (props.TryGetValue(key, out var value) && value is double res)
            {
                r = (int)res;
                return true;
            }
            r = 0;
            return false;
        }


        void IRtmpExtensions.HandlePublish(RtmpConnection connection) { }

        void IRtmpExtensions.HandleStop(RtmpConnection connection, RtmpConnectionState state)
        {
        }

        void IRtmpExtensions.HandleDeleteStream(RtmpConnection connection, DeleteStreamCommandMessage message)
        {
            lock (_sync)
            {
                _queue.Enqueue(null); // special case: pass graceful close to reading thread
                Monitor.PulseAll(_sync);
            }
        }

        void IRtmpExtensions.HandleVideoMessage(RtmpConnection connection, VideoMessage message)
        {
            bool sleep = false;
            lock (_sync)
            {
                if (!_videoExtradataFound && _commonPropsFound)
                {
                    if (message.Data.Length < 1024)
                    {
                        Log.Information("Retrieving video extra data");
                        CopyExtraData(ref _videoProps, message.Data, 5);
                        _videoExtradataFound = true;
                        RefreshConfig();
                    }
                    else
                        Log.Warning($"Unexpectedly long first video packet with length {message.Data.Length}");
                }

                sleep = SendPacket(message.Data, 0, 5, message.MessageHeader.Timestamp);
            }
            if (sleep)
                Thread.Sleep(100);
        }

        void IRtmpExtensions.HandleAudioMessage(RtmpConnection connection, AudioMessage message)
        {
            bool sleep = false;
            lock (_sync)
            {
                if (_audioProps.codec_id == Core.NoStream)
                {
                    if (!_unexpectedAudioLogged)
                    {
                        Log.Error("Receiving audio message while audio is not expected");
                        _unexpectedAudioLogged = true;
                    }
                    return;
                }

                if (!_audioExtradataFound && _commonPropsFound)
                {
                    Log.Information("Retrieving audio extra data");
                    CopyExtraData(ref _audioProps, message.Data, 2);
                    _audioExtradataFound = true;
                    RefreshConfig();
                }
                sleep = SendPacket(message.Data, 1, 2, message.MessageHeader.Timestamp);
            }
            if (sleep)
            {
                _statisticKeeper.UpdateData(d => d.AddError(InputErrorType.ExceedingLimit));
                Thread.Sleep(100);
            }
        }

        private void RefreshConfig()
        {
            if (_videoExtradataFound && _audioExtradataFound)
            {
                Config = new InputConfig(new InputStreamProperties[] { new InputStreamProperties { CodecProps = _videoProps }, new InputStreamProperties { CodecProps = _audioProps } });
                _analysed.Set();
            }
            else if (_videoExtradataFound && _audioProps.codec_id == Core.NoStream)
            {
                Config = new InputConfig(new InputStreamProperties[] { new InputStreamProperties { CodecProps = _videoProps } });
                _analysed.Set();
            }
        }

        private void CopyExtraData(ref CodecProperties props, ReadOnlyMemory<byte> data, int prefix)
        {
            var buf = new byte[1024];
            int length = data.Length - prefix;
            Array.Copy(data.Slice(prefix).ToArray(), buf, length);
            
            props = props with { extradata_size = length, extradata = buf };
        }

        private bool SendPacket(ReadOnlyMemory<byte> data, int streamIndex, int prefix, uint timestamp)
        {
            var head = data.Span[0];
            var frameType = (FrameType)(head >> 4);
            var packet = _streamer.PacketPool.Rent();

            long currentTime = Core.GetCurrentTime();
            long time = _timeAdjuster.Add(((long)timestamp) * 10000, currentTime);

            packet.InitFromBuffer(data.Slice(prefix), prefix, data.Length - prefix, time, streamIndex, frameType == FrameType.KeyFrame);//TODO: time?
            _queue.Enqueue(packet);
            Monitor.PulseAll(_sync);

            return AddStat(data.Length, currentTime);
        }

        private bool AddStat(int length, long currentTime)
        {
            if (_bitrateLimit == 0)
                return false;

            _traffic.AddLast(new TrafficRecord(length, currentTime));
            while (_traffic.Count > 0 && currentTime - _traffic.First.Value.Time > 3000_0000) // 3 sec
                _traffic.RemoveFirst();

            var bitrate = _traffic.Sum(s => s.Bytes) * 8 / 3 / 1000;
            return bitrate > _bitrateLimit;
        }

        public StatisticItem[] GetStat()
        {
            if (_gracefullyClosed)
                _statisticKeeper.UpdateData(d => d.AddError(InputErrorType.GracefulClose));

            var cfg = Config;
            if (cfg != null && cfg.InputStreamProps != null && cfg.InputStreamProps.Length > 0 && cfg.InputStreamProps[0].CodecProps.height > 0
                && cfg.InputStreamProps[0].CodecProps.width > 0)
            {
                var item = _statisticKeeper.Get();
                if (item.Data is StatisticDataOfInputOutput data)
                {
                    data.Other = Math.Min(cfg.InputStreamProps[0].CodecProps.height, cfg.InputStreamProps[0].CodecProps.width).ToString();
                }
                return new[] { item };
            }
                

            return new[] { _statisticKeeper.Get() };
        }
    }

    public record TrafficRecord(int Bytes, long Time);

    public enum FrameType
    {
        KeyFrame = 1,
        InterFrame,
        DisposableInterFrame,
        GeneratedKeyFrame,
        VideoInfoOrCommandFrame
    }
}
