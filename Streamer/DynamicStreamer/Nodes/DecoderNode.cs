using DynamicStreamer.Contexts;
using DynamicStreamer.Queues;
using System;

namespace DynamicStreamer.Nodes
{
    public class DecoderNode : Node<IDecoderContext, DecoderSetup, Packet, Frame>
    {
        public DecoderNode(NodeName name, IStreamerBase controller) : base(name, controller)
        {
        }

        protected override IDecoderContext CreateAndOpenContext(DecoderSetup config)
        {
            IDecoderContext result;
            if (config.Type == DecoderContextFFMpeg.Type)
                result = new DecoderContextFFMpeg();
            else if (config.Type == DecoderContextDirectXUpload.Type)
                result = new DecoderContextDirectXUpload();
            else if (config.Type == DecoderContextDirectXPassThru.Type)
                result = new DecoderContextDirectXPassThru();
            else
                throw new InvalidOperationException($"Decoder type {config.Type} not found");

            int res = result.Open(config);
            if (res < 0)
                Core.LogError($"FATAL: failed to open Decoder {Name} with {config}");
            return result;
        }

        protected override void ProcessData(Data<Packet> data, ContextVersion<IDecoderContext, DecoderSetup, Frame> currentVersion)
        {
            _statisticKeeper.Data.InFrames++; //unsafe, but ok
            if (currentVersion == null)
            {
                Streamer.PacketPool.Back(data.Payload);
                return;
            }
            data.Trace?.Received(Name);

            int writeRes = currentVersion.Context.Instance.Write(data.Payload);
            Streamer.PacketPool.Back(data.Payload);

            if (Core.IsFailed(writeRes))
            {
                _statisticKeeper.Data.Errors++;
                Core.LogError($"Write to {Name}: {Core.GetErrorMessage(writeRes)}", "write to node failed");
                return;
            }

            while (!currentVersion.IsInterrupted)
            {
                var resultPayload = Streamer.FramePool.Rent();
                var readRes = currentVersion.Context.Instance.Read(resultPayload);
                if (readRes == ErrorCodes.TryAgainLater)
                {
                    Streamer.FramePool.Back(resultPayload);
                    break;
                }
                else if (Core.IsFailed(readRes))
                {
                    _statisticKeeper.Data.Errors++;
                    Streamer.FramePool.Back(resultPayload);
                    Core.LogError($"Read from {Name}: {Core.GetErrorMessage(writeRes)}", "read from node failed");
                    break;
                }
                else // success
                {
                    _statisticKeeper.Data.OutFrames++; //unsafe, but ok
                    currentVersion.OutputQueue.Enqueue(new Data<Frame>(resultPayload, currentVersion.Version, data.SequenceNumber, PayloadTrace.Create(Name, data.Trace)));
                }
            }
        }
    }
}
