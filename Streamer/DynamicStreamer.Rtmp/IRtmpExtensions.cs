using Harmonic.Networking.Rtmp.Messages;
using Harmonic.Networking.Rtmp.Messages.Commands;

namespace DynamicStreamer.Rtmp
{
    public interface IRtmpExtensions
    {
        void HandlePublish(RtmpConnection connection);

        void HandleStop(RtmpConnection connection, RtmpConnectionState state);

        void HandleDataMessage(RtmpConnection connection, DataMessage message);

        void HandleVideoMessage(RtmpConnection connection, VideoMessage message);

        void HandleAudioMessage(RtmpConnection connection, AudioMessage message);

        void HandleDeleteStream(RtmpConnection connection, DeleteStreamCommandMessage message);
    }
}
