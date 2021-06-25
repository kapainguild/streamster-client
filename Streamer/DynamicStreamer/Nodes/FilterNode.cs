using DynamicStreamer.Contexts;
using DynamicStreamer.Queues;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.Nodes
{
    public class FilterNode : Node<IFilterContext, FilterSetup, Frame, Frame>
    {
        public FilterNode(NodeName name, IStreamerBase streamer) : base(name, streamer)
        {
        }

        private IFilterContext CreateContext(FilterSetup config)
        {
            if (config.Type == FilterContextDirectXUpload.Type)
                return new FilterContextDirectXUpload();

            if (config.Type == FilterContextDirectXDownload.Type)
                return new FilterContextDirectXDownload(Streamer);

            if (config.Type == FilterContextDirectXTransform.Type)
                return new FilterContextDirectXTransform(Streamer);

            if (config.Type == FilterContextNull.Type)
                return new FilterContextNull();

            return new FilterContextFFMpeg();
        }

        protected override IFilterContext CreateAndOpenContext(FilterSetup config)
        {
            var result = CreateContext(config);
            int res = result.Open(config);
            if (res < 0)
                Core.LogError($"FATAL: failed to open Filter {Name} with {config}");
            return result;
        }

        protected override void ProcessData(Data<Frame> data, ContextVersion<IFilterContext, FilterSetup, Frame> currentVersion)
        {
            //if (Name.Name == "FMix" && data.Payload != null)
            //{
            //    Core.LogInfo($"--------------------------------In {Name} {data.SourceId}  {data.Payload.Properties.Pts}");
            //}

            _statisticKeeper.Data.InFrames++; //unsafe, but ok
            if (currentVersion == null)
            {
                Streamer.FramePool.Back(data.Payload);
                return;
            }
            data.Trace?.Received(Name);

            var instance = currentVersion.Context.Instance;
            //using var mem = new TimeMeasurer($"Filter '{Name}'");
            
            int writeRes = instance.Write(data.Payload, data.SourceId);
            if (writeRes == (int)ErrorCodes.NullFilter)
            {
                _statisticKeeper.Data.OutFrames++; //unsafe, but ok
                currentVersion.OutputQueue.Enqueue(data);
                return;
            }

            Streamer.FramePool.Back(data.Payload);

            if (Core.IsFailed(writeRes))
            {
                _statisticKeeper.Data.Errors++;
                Core.LogError($"Write to {Name} (sid:{data.SourceId}): {Core.GetErrorMessage(writeRes)}", "write to node failed");
                return;
            }

            while (!currentVersion.IsInterrupted)
            {
                var resultPayload = Streamer.FramePool.Rent();
                var readRes = instance.Read(resultPayload);
                if (readRes == ErrorCodes.TryAgainLater)
                {
                    Streamer.FramePool.Back(resultPayload);
                    break;
                }
                else if (Core.IsFailed(readRes))
                {
                    _statisticKeeper.Data.Errors++;
                    Streamer.FramePool.Back(resultPayload);
                    Core.LogError($"Read from {Name}: {Core.GetErrorMessage((int)readRes)}", "read from node failed");
                    break;
                }
                else // success
                {
                    //if (Name.Name == "FMix" && data.Payload != null)
                    //{
                    //    Core.LogInfo($"----------------------------------Out {Name} -  {resultPayload.Properties.Pts}");
                    //}

                    _statisticKeeper.Data.OutFrames++; //unsafe, but ok
                    currentVersion.OutputQueue.Enqueue(new Data<Frame>(resultPayload, currentVersion.Version, data.SequenceNumber, PayloadTrace.Create(Name, data.Trace)));
                }
            }
        }
    }
}
