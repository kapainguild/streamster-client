using DynamicStreamer;
using DynamicStreamer.Nodes;
using Serilog;
using Streamster.ClientCore.Support;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Streamster.ClientCore.Models
{
    class StreamerHealthCheck
    {
        private CoreData _coreData;
        private readonly StreamingSourcesModel _streamingSourcesModel;
        private AverageIntValue _cloudInAverage;
        private AverageIntValue _cloudOutAverage;
        private Dictionary<string, IssueCache> _prevVideos = new Dictionary<string, IssueCache>();
        private Dictionary<string, IssueCache> _prevAudios = new Dictionary<string, IssueCache>();

        public StreamerHealthCheck(CoreData coreData, StreamingSourcesModel streamingSourcesModel)
        {
            _coreData = coreData;
            _streamingSourcesModel = streamingSourcesModel;
        }

        internal void ProcessReceivier(ClientStreamer receiverStreamer, IDeviceIndicators kpi)
        {
            if (receiverStreamer == null)
            {
                kpi.CloudIn.State = IndicatorState.Disabled;
                _cloudInAverage = null;
            }
            else
            {
                if (_cloudInAverage == null)
                    _cloudInAverage = new AverageIntValue(3, true);

                var stat = receiverStreamer.ResourceManager.GetStatistics(out var period);
                var input = stat.FirstOrDefault(s => s.Name.Name == "I" && s.Name.Trunk == "0" && s.Name.TrunkPrefix == "V");

                if (input == null)
                    kpi.CloudIn.State = IndicatorState.Disabled; // no outgest data yet
                else
                {
                    var data = (StatisticDataOfInputOutput)input.Data;
                    var bitrate = (int)(data.Bytes * 8 / period.TotalMilliseconds);
                    var ave = _cloudInAverage.AddValue(bitrate);

                    IndicatorState state = IndicatorState.Ok;
                    if (data.Errors > 0 || bitrate == 0)
                    {
                        state = IndicatorState.Error;
                    }
                    else if (!_streamingSourcesModel.IsExternalEncoderStreaming()) // it does not respect _coreData.Settings.Bitrate
                    {
                        var bitratePercent = (ave * 100) / _coreData.Settings.Bitrate;

                        if (bitratePercent < 60)
                            state = IndicatorState.Warning;
                    }

                    kpi.CloudIn.Bitrate = ave;
                    kpi.CloudIn.State = state;
                }
            }
        }

        internal void ProcessMain(ClientStreamer mainStreamer, IDeviceIndicators kpi, StreamerRebuildContext lastRebuildContext)
        {
            if(mainStreamer == null)
            {
                kpi.Encoder.State = IndicatorState.Disabled;
                _cloudOutAverage = null;
            }
            else
            {
                if (_cloudOutAverage == null)
                    _cloudOutAverage = new AverageIntValue(3, true);

                var stat = mainStreamer.ResourceManager.GetStatistics(out var period);
                var overload = mainStreamer.ResourceManager.GetOverload();

                int failedInputs = 0;

                if (_streamingSourcesModel.TryGetCurrentScene(out var scene) &&
                    scene.Owner == _coreData.ThisDeviceId)
                {
                    ProcessInputs(stat, lastRebuildContext.Videos, "V", _prevVideos, scene.VideoIssues, s => scene.VideoIssues = s);
                    ProcessInputs(stat, lastRebuildContext.Audios, "A", _prevAudios, scene.AudioIssues, s => scene.AudioIssues = s);

                    if (scene.VideoIssues != null)
                    {
                        foreach (var vi in scene.VideoIssues)
                            if (scene.Items.TryGetValue(vi.Id, out var item) && item.Visible)
                               failedInputs++;
                    }
                    if (scene.AudioIssues != null)
                    {
                        foreach (var vi in scene.AudioIssues)
                            if (scene.Audios.TryGetValue(vi.Id, out var item) && !item.Muted)
                                failedInputs++;
                    }
                }

                ProcessMainKpi(stat, kpi, period, overload, failedInputs);
            }
        }

        private void ProcessMainKpi(List<StatisticItem> stat, IDeviceIndicators kpi, TimeSpan period, int overload, int failedInputs)
        {

            // encoder & inputs
            var encoder = kpi.Encoder;

            var encoderStat = stat.FirstOrDefault(s => s.Name.Name == "E" && s.Name.TrunkPrefix == "VE");

            if (encoderStat == null)
                Log.Warning("Encoder statistics not found");

            if (encoderStat != null)
            {
                var encData = (StatisticDataOfProcessingNode)encoderStat.Data;
                var fps = (int)(encData.OutFrames / period.TotalSeconds);
                var configuredFps = _coreData.Settings.Fps;

                var fpsPercent = (fps * 100) / configuredFps;

                var state = IndicatorState.Ok;
                if (failedInputs > 0)
                    state = IndicatorState.Error2;
                else if (overload > 6)
                    state = IndicatorState.Error;
                else if (fpsPercent < 87)
                    state = IndicatorState.Warning;

                encoder.State = state;
                encoder.Data = new EncoderData
                {
                    Q = overload,
                    O = fps,
                    F = failedInputs
                };
            }
            else
                encoder.State = IndicatorState.Disabled;
        }

        private void ProcessInputs(List<StatisticItem> stat, 
            Dictionary<string, RebuildInfo> rebuild, 
            string trunkPrefix, 
            Dictionary<string, IssueCache> cache,
            InputIssue[] model, 
            Action<InputIssue[]> setter)
        {
            List<InputIssue> vi = new List<InputIssue>();
            var now = DateTime.UtcNow;
            foreach (var item in rebuild)
            {
                InputIssueDesc result = InputIssueDesc.None;
                string id = item.Key;
                if (item.Value.Issue != InputIssueDesc.None)
                    result =  item.Value.Issue;
                else if (item.Value.Video?.Image == null)
                {
                    // runtime issue
                    var st = stat.FirstOrDefault(s => s.Name.Trunk == id && s.Name.Name == "I" && s.Name.TrunkPrefix == trunkPrefix);
                    if (st?.Data is StatisticDataOfInputOutput iostat)
                    {
                        if (!cache.ContainsKey(id))
                            cache[id] = new IssueCache { Created = now };

                        if (iostat.Errors > 0)
                        {
                            if (iostat.ErrorType == InputErrorType.InUse)
                                result = InputIssueDesc.InUse;
                            else
                                result = InputIssueDesc.Failed;

                            cache[id].FailedTime = DateTime.UtcNow;
                            cache[id].Failure = result;
                        }
                        else if (iostat.Frames == 0)
                        {
                            var cacheEntry = cache[id];
                            if (now - cacheEntry.FailedTime < TimeSpan.FromSeconds(10))
                                result = cacheEntry.Failure;
                            else if (now - cacheEntry.Created > TimeSpan.FromSeconds(3))
                            {
                                cacheEntry.NoFramesCount++;
                                if (cacheEntry.NoFramesCount >= 2) // two consequtive seconds
                                    result = InputIssueDesc.NoFrames;
                            }
                        }
                        else if (iostat.Frames > 250)
                        {
                            result = InputIssueDesc.TooManyFrames;
                        }
                        else
                        {
                            cache[id].NoFramesCount = 0;
                        }
                    }
                    //else
                    //    result = InputIssueDesc.UnknownState;
                }

                if (result != InputIssueDesc.None)
                {
                    vi.Add(new InputIssue { Id = id, Desc = result });
                }
            }

            if (vi.Count == 0)
            {
                if (model != null)
                    setter(null);
            }
            else
            {
                var sorted = vi.OrderBy(s => s.Id).ToArray();

                if (model == null || !model.SequenceEqual(sorted))
                    setter(sorted);
            }
        }
    }


    class IssueCache
    {
        public DateTime FailedTime { get; set; }
        public InputIssueDesc Failure { get; set; }

        public DateTime Created { get; set; }

        public int NoFramesCount { get; set; }
    }
}
