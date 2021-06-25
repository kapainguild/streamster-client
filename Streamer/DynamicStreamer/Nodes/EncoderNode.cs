
using DynamicStreamer.Contexts;

namespace DynamicStreamer.Nodes
{
    public class EncoderNode : Node<IEncoderContext, EncoderSetup, Frame, Packet>
    {
        private int _makeIFrameNextPacket;

        public EncoderNode(NodeName name, IStreamerBase controller) : base(name, controller)
        {
        }

        protected override IEncoderContext CreateAndOpenContext(EncoderSetup config)
        {
            IEncoderContext result = config.Type == EncoderContextQsvDx.TypeName ? new EncoderContextQsvDx() : new EncoderContext();
            int res = result.Open(config);
            if (res < 0)
                Core.LogError($"FATAL: failed to open Encoder {Name} with {config}");
            return result;
        }

        protected override void ProcessData(Data<Frame> data, ContextVersion<IEncoderContext, EncoderSetup, Packet> currentVersion)
        {
            _statisticKeeper.Data.InFrames++; //unsafe, but ok
            if (currentVersion == null)
            {
                Streamer.FramePool.Back(data.Payload);
                return;
            }
            data.Trace?.Received(Name);
            bool enforceIFrame = false;

            if (_makeIFrameNextPacket == data.Version && currentVersion.ContextSetup.SupportsEnforcingIFrame)
            {
                Core.LogInfo("Trying to enforce IFrame on encoder");
                _makeIFrameNextPacket = 0;
                enforceIFrame = true;
            }

            int writeRes = currentVersion.Context.Instance.Write(data.Payload, enforceIFrame);
            Streamer.FramePool.Back(data.Payload);

            if (Core.IsFailed(writeRes))
            {
                _statisticKeeper.Data.Errors++;
                Core.LogError($"Write to {Name}: {Core.GetErrorMessage(writeRes)}", "write to node failed");
                return;
            }

            while (!currentVersion.IsInterrupted)
            {
                var resultPayload = Streamer.PacketPool.Rent();
                var readRes = currentVersion.Context.Instance.Read(resultPayload);
                if (readRes == ErrorCodes.TryAgainLater)
                {
                    Streamer.PacketPool.Back(resultPayload);
                    break;
                }
                else if (Core.IsFailed(readRes))
                {
                    _statisticKeeper.Data.Errors++;
                    Streamer.PacketPool.Back(resultPayload);
                    Core.LogError($"Read from {Name}: {Core.GetErrorMessage(writeRes)}", "read from node failed");
                    break;
                }
                else // success
                {
                    _statisticKeeper.Data.OutFrames++; //unsafe, but ok
                    currentVersion.OutputQueue.Enqueue(new Data<Packet>(resultPayload, currentVersion.Version, data.SequenceNumber, PayloadTrace.Create(Name, data.Trace)));
                }
            }
        }

        public void MakeIFrameNextPacket(int version)
        {
            _makeIFrameNextPacket = version;
        }
    }
}
