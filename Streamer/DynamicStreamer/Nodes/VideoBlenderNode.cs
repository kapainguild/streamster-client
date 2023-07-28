using DynamicStreamer.Contexts;
using DynamicStreamer.Helpers;

namespace DynamicStreamer.Nodes
{
    public class VideoBlenderNode : Node<VideoBlenderContext, VideoBlenderSetup, Frame, Frame>
    {
        protected StatisticKeeper<StatisticDataOfBlenderNode> _statisticKeeper2;
        private readonly OverloadController _overloadController;
        private int _latestVersion = -1;

        public VideoBlenderNode(NodeName name, IStreamerBase streamer, OverloadController overloadController) : base(name, streamer)
        {
            _statisticKeeper2 = new StatisticKeeper<StatisticDataOfBlenderNode>(name);
            _overloadController = overloadController;
        }

        protected override VideoBlenderContext CreateAndOpenContext(VideoBlenderSetup setup)
        {
            return new VideoBlenderContext(setup, Streamer.FramePool, Streamer, _overloadController, () => ActivateNoData());
        }

        public override StatisticItem[] GetStat()
        {
            base.GetStat();
            return new[] { _statisticKeeper2.Get() };
        }

        protected override void ProcessData(Data<Frame> data, ContextVersion<VideoBlenderContext, VideoBlenderSetup, Frame> currentVersion)
        {
            if (currentVersion == null)
                return;

            if (data != null)
            {
                _statisticKeeper.Data.InFrames++; //unsafe, but ok
                if (currentVersion == null || 
                    currentVersion.Version < _latestVersion) //TODO:NextRelease: we need some how handle older version packets to not lose frames
                {
                    Streamer.FramePool.Back(data.Payload);
                    return;
                }
                _latestVersion = currentVersion.Version;
                data.Trace?.Received(Name);

                int writeRes = currentVersion.Context.Instance.Write(data, currentVersion.ContextSetup, _statisticKeeper2.Data);

                if (Core.IsFailed(writeRes))
                {
                    _statisticKeeper.Data.Errors++;
                    Core.LogError($"Write to {Name} (sid:{data.SourceId}): {Core.GetErrorMessage(writeRes)}", "write to node failed");
                    return;
                }
            }
            else
            {
                currentVersion.Context.Instance.Write(null, currentVersion.ContextSetup, null);
            }

            while (!currentVersion.IsInterrupted)
            {
                var resultPayload = Streamer.FramePool.Rent();
                var readRes = currentVersion.Context.Instance.Read(resultPayload, out var resultTrace);
                if (readRes == ErrorCodes.TryAgainLater)
                {
                    Streamer.FramePool.Back(resultPayload);
                    break;
                }
                else if (Core.IsFailed(readRes))
                {
                    _statisticKeeper2.Data.Errors++;
                    Streamer.FramePool.Back(resultPayload);
                    Core.LogError($"Read from {Name}: {Core.GetErrorMessage((int)readRes)}", "read from node failed");
                    break;
                }
                else // success
                {
                    //Core.LogInfo($"-------------------------------------------------------------Out {Name}");

                    _statisticKeeper2.Data.OutFrames++; //unsafe, but ok
                    currentVersion.OutputQueue.Enqueue(new Data<Frame>(resultPayload, currentVersion.Version, data?.SequenceNumber ?? 0, resultTrace));
                }
            }
        }
    }
}
