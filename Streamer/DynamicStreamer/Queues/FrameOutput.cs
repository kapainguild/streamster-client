using System;

namespace DynamicStreamer.Queues
{
    public record FrameOutputData(FromPool<Frame> Frame, PayloadTrace Trace);

    public class FrameOutput : ITargetQueue<Frame>
    {
        private readonly IStreamerBase _streamer;
        private readonly Action<FrameOutputData> _onUiFrame;

        public FrameOutput(IStreamerBase streamer, Action<FrameOutputData> onUiFrame)
        {
            _streamer = streamer;
            _onUiFrame = onUiFrame;
        }

        public void Enqueue(Data<Frame> data)
        {
            _onUiFrame(new FrameOutputData(new FromPool<Frame>(data.Payload, _streamer.FramePool), data.Trace));
        }
    }
}
