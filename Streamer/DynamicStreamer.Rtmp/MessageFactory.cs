using Harmonic.Networking.Rtmp.Data;
using Harmonic.Networking.Rtmp.Messages;
using Harmonic.Networking.Rtmp.Messages.Commands;
using Harmonic.Networking.Rtmp.Messages.UserControlMessages;
using Harmonic.Networking.Rtmp.Serialization;
using Harmonic.Networking.Utils;
using RtmpProtocol.Messages.Commands;
using Serilog;
using System;

namespace DynamicStreamer.Rtmp
{
    internal class MessageFactory
    {
        internal static (Message msg, int factoryConsumed) Create(MessageType messageType, MessageHeader messageHeader, SerializationContext msgContext)
        {
            try
            {
                switch (messageType)
                {
                    case MessageType.SetChunkSize:
                        return CreateSimpleMessage<SetChunkSizeMessage>();
                    case MessageType.AbortMessage:
                        return CreateSimpleMessage<AbortMessage>();
                    case MessageType.Acknowledgement:
                        return CreateSimpleMessage<AcknowledgementMessage>();
                    case MessageType.WindowAcknowledgementSize:
                        return CreateSimpleMessage<WindowAcknowledgementSizeMessage>();
                    case MessageType.SetPeerBandwidth:
                        return CreateSimpleMessage<SetPeerBandwidthMessage>();

                    case MessageType.AudioMessage:
                        return CreateSimpleMessage<AudioMessage>();
                    case MessageType.VideoMessage:
                        return CreateSimpleMessage<VideoMessage>();

                    case MessageType.Amf0Data:
                        return (new DataMessage(AmfEncodingVersion.Amf0), 0);
                    case MessageType.Amf3Data:
                        return (new DataMessage(AmfEncodingVersion.Amf3), 0);

                    case MessageType.UserControlMessages:
                        return (CreateUserControl((UserControlEventType)NetworkBitConverter.ToUInt16(msgContext.ReadBuffer.Span)), 0);

                    case MessageType.Amf0Command:
                        msgContext.AmfSerializationContext.Amf0Reader.TryGetString(msgContext.ReadBuffer.Span, out var name, out var consumed);
                        var command = CreateCommand(name, AmfEncodingVersion.Amf0);
                        command.ProcedureName = name;
                        return (command, consumed);

                    case MessageType.Amf3Command:
                        msgContext.AmfSerializationContext.Amf3Reader.TryGetString(msgContext.ReadBuffer.Span, out var name3, out var consumed3);
                        var command3 = CreateCommand(name3, AmfEncodingVersion.Amf3);
                        command3.ProcedureName = name3;
                        return (command3, consumed3);

                    case MessageType.AggregateMessage:
                        throw new NotSupportedException("not expected aggregate message");

                    default:
                        throw new NotSupportedException($"Not supported ({(int)messageType}) message");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return (null, 0);
            }
        }

        private static CommandMessage CreateCommand(string name, AmfEncodingVersion encoding)
        {
            return name switch
            {
                "connect" => new ConnectCommandMessage(encoding),
                "close" => new CloseCommandMessage(encoding),
                "createStream" => new CreateStreamCommandMessage(encoding),
                "deleteStream" => new DeleteStreamCommandMessage(encoding),
                "releaseStream" => new ReleaseStreamCommandMessage(encoding),
                "onStatus" => new OnStatusCommandMessage(encoding),
                "pause" => new PauseCommandMessage(encoding),
                "play2" => new Play2CommandMessage(encoding),
                "play" => new PlayCommandMessage(encoding),
                "publish" => new PublishCommandMessage(encoding),
                "receiveAudio" => new ReceiveAudioCommandMessage(encoding),
                "receiveVideo" => new ReceiveVideoCommandMessage(encoding),
                "seek" => new SeekCommandMessage(encoding),
                "FCPublish" => new FCPublishCommandMessage(encoding),
                "FCUnpublish" => new FCUnpublishCommandMessage(encoding),
                _ => throw new NotSupportedException($"Not supported ({name}) command message"),
            };
        }

        private static Message CreateUserControl(UserControlEventType userControlEventType)
        {
            switch (userControlEventType)
            {
                case UserControlEventType.StreamBegin: return new StreamBeginMessage();
                case UserControlEventType.StreamEof: return new StreamEofMessage();
                case UserControlEventType.StreamDry: return new StreamDryMessage();
                case UserControlEventType.SetBufferLength: return new SetBufferLengthMessage();
                case UserControlEventType.StreamIsRecorded: return new StreamIsRecordedMessage();
                case UserControlEventType.PingRequest: return new PingRequestMessage();
                case UserControlEventType.PingResponse: return new PingResponseMessage();
            }
            throw new NotSupportedException($"Not supported ({(int)userControlEventType}) user control message");
        }

        private static (Message msg, int factoryConsumed) CreateSimpleMessage<T>() where T : Message, new()
        {
            return (new T(), 0);
        }
    }
}