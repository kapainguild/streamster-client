using Harmonic.Buffers;
using Harmonic.Networking;
using Harmonic.Networking.Amf.Common;
using Harmonic.Networking.Rtmp;
using Harmonic.Networking.Rtmp.Data;
using Harmonic.Networking.Rtmp.Messages;
using Harmonic.Networking.Rtmp.Messages.Commands;
using Harmonic.Networking.Rtmp.Serialization;
using Harmonic.Networking.Utils;
using Serilog;
using Serilog.Context;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace DynamicStreamer.Rtmp
{
    public enum ProcessState
    {
        HandshakeC0C1,
        HandshakeC2,
        FirstByteBasicHeader,
        ChunkMessageHeader,
        ExtendedTimestamp,
        CompleteMessage
    }

    public delegate (bool continueProcessing, int consumed) BufferProcessor(ReadOnlySequence<byte> buffer, int consumed);

    public class RtmpConnection 
    {
        public const int ExtendedTimestampLength = 4;
        public const int Type0Size = 11;
        public const int Type1Size = 7;
        public const int Type2Size = 3;
        public const uint ControlMessageStreamId = 0;

        public const uint ControlChunkStreamId = 2;
        

        private Socket _socket;
        private readonly IRtmpExtensions _extension;
        private readonly string _logPrefix;
        private readonly string _logPrefixName;

        private Dictionary<ProcessState, BufferProcessor> _bufferProcessors = new Dictionary<ProcessState, BufferProcessor>();

        private ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
        private Queue<SendItem> _writerQueue = new Queue<SendItem>();
        private object _writerQueueLock = new object();
        private volatile bool _continueProcessing = true;

        private uint _readerTimestampEpoch = 0;
        private uint _writerTimestampEpoch = 0;
        private Random _random = new Random();
        private byte[] _s1Data;

        private Thread _threadReceive;
        private Thread _threadSend;

        private ProcessState _nextProcessState = ProcessState.HandshakeC0C1;

        private AmfSerializationContext _amfSerializationContext = new AmfSerializationContext();

        private ChunkHeader _processingChunk = null;
        private int ReadMinimumBufferSize { get => (_bufferState.ReadChunkSize + Type0Size) * 4; }
        public ConnectionInformation ConnectionInformation { get; private set; }

        private Dictionary<uint, MessageHeader> _previousWriteMessageHeader = new Dictionary<uint, MessageHeader>();
        private Dictionary<uint, MessageHeader> _previousReadMessageHeader = new Dictionary<uint, MessageHeader>();
        private Dictionary<uint, MessageReadingState> _incompleteMessageState = new Dictionary<uint, MessageReadingState>();

        private BufferState _bufferState = new BufferState();

        private LimitType? _previousLimitType = null;

        private IdProvider _messageIdProvider = new IdProvider(ControlMessageStreamId + 1, 32000);
        private IdProvider _chunkIdProvider = new IdProvider(ControlChunkStreamId + 1, 32000);

        private uint _connectionChunkId;

        private Dictionary<uint, NetStream> _netStreams = new Dictionary<uint, NetStream>();

        public Socket Socket => _socket;

        public RtmpConnection(Socket socket, IRtmpExtensions extension, int receiveTimeout, string logPrefix, string logPrefixName)
        {
            socket.ReceiveTimeout = receiveTimeout;
            _socket = socket;
            _extension = extension;
            _logPrefix = logPrefix;
            _logPrefixName = logPrefixName;

            _bufferProcessors.Add(ProcessState.HandshakeC0C1, ProcessHandshakeC0C1);
            _bufferProcessors.Add(ProcessState.HandshakeC2, ProcessHandshakeC2);
        }

        public RtmpConnection(RtmpConnectionState state, Socket socket, IRtmpExtensions extension, string logPrefix, string logPrefixName) : this(socket, extension, 10_000, logPrefix, logPrefixName)
        {
            using var _ = LogContext.PushProperty(_logPrefixName, _logPrefix);

            HandshakePerformed();

            _netStreams = state.NetStreams.ToDictionary(s => s.MessageStreamId, s => new NetStream(s));
            _connectionChunkId = state.ConnectionChunkId;
            _bufferState = state.BufferState;

            _messageIdProvider.InitRange(state.NetStreams.Select(s => s.MessageStreamId));
            _chunkIdProvider.InitRange(state.NetStreams.Select(s => s.ChunkStreamId).Concat(new[] { state.ConnectionChunkId}));
            _previousReadMessageHeader = state.PrevReadMessageStates.ToDictionary(s => s.ChunkId, s => new MessageHeader(s.MessageHeader));

            SendPublishOk(_netStreams.Values.First());
        }

        public void Kill()
        {
            CloseSocketSafely(_socket);
            _socket = null;
        }

        public static void CloseSocketSafely(Socket s)
        {
            try
            {
                s?.Close();
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to Kill connection");
            }
        }


        public static string GetRemoteEndPoint(Socket socket)
        {
            if (socket.RemoteEndPoint is IPEndPoint ip && ip.Address != null)
                return ip.Address.ToString();
            else
                Log.Error($"Unknown endpoint {socket.RemoteEndPoint} of type {socket.RemoteEndPoint?.GetType()}");

            return "Unknown";
        }


        public void Start()
        {
            _threadSend = new Thread(() => SendRoutine());
            _threadReceive = new Thread(() => ReceiveRoutine());

            _threadSend.Start();
            _threadReceive.Start();
        }

        private void SendRoutine()
        {
            using var _ = LogContext.PushProperty(_logPrefixName, _logPrefix);
            try
            {
                while (true)
                {
                    SendItem item;
                    lock (_writerQueueLock)
                    {
                        while (_continueProcessing && _writerQueue.Count == 0)
                            Monitor.Wait(_writerQueueLock);

                        if (!_continueProcessing)
                            break;

                        item = _writerQueue.Dequeue();
                    }

                    _socket.Send(item.Buffer, item.Length, SocketFlags.None);
                    _arrayPool.Return(item.Buffer);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Socket failed for sending");
            }
        }

        private void ReceiveRoutine()
        {
            using var _ = LogContext.PushProperty(_logPrefixName, _logPrefix);
            
            var readBuffer = _arrayPool.Rent(32_000);
            byte[] prevData = null;
            int prevDataLength = 0;
            byte[] concatBuffer = null;
            try
            {
                while (true)
                {
                    var read = _socket.Receive(readBuffer);

                    if (read == 0)
                    {
                        Log.Information("Graceful close of socket");
                        break;
                    }
                    
                    byte[] toSendBuffer = readBuffer;
                    int toSendLength = read;

                    if (prevData != null)
                    {
                        toSendLength = read + prevDataLength;
                        concatBuffer = _arrayPool.Rent(toSendLength);
                        Array.Copy(prevData, 0, concatBuffer, 0, prevDataLength);
                        Array.Copy(readBuffer, 0, concatBuffer, prevDataLength, read);

                        toSendBuffer = concatBuffer;

                        _arrayPool.Return(prevData);
                        prevData = null;
                        prevDataLength = 0;
                    }

                    int consumed = 0;
                    ReadOnlySequence<byte> toSend = new ReadOnlySequence<byte>(toSendBuffer, 0, toSendLength);

                    while (true)
                    {
                        var stageProcessor = _bufferProcessors[_nextProcessState];
                        var stageResult =  stageProcessor(toSend, consumed);
                        consumed = stageResult.consumed;
                        if (!stageResult.continueProcessing)
                            break;
                    }
                    if (consumed != toSendLength)
                    {
                        prevDataLength = toSendLength - consumed;
                        prevData = _arrayPool.Rent(prevDataLength);
                        Array.Copy(toSendBuffer, consumed, prevData, 0, prevDataLength);
                    }
                    if (concatBuffer != null)
                    {
                        _arrayPool.Return(concatBuffer);
                        concatBuffer = null;
                    }

                    if (IsHandshaked() && _bufferState.ReadWindowAcknowledgementSize.HasValue)
                    {
                        _bufferState.ReadUnacknowledgedSize += consumed;
                        if (_bufferState.ReadUnacknowledgedSize >= _bufferState.ReadWindowAcknowledgementSize)
                        {
                            SendMessage(new AcknowledgementMessage() { BytesReceived = (uint)_bufferState.ReadUnacknowledgedSize });
                            _bufferState.ReadUnacknowledgedSize -= _bufferState.ReadWindowAcknowledgementSize.Value;
                        }
                    }
                }

            }
            catch (OperationCanceledException)
            {
                Log.Information("Processing cancelled");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to read socket");
            }
            finally
            {
                _arrayPool.Return(readBuffer);
                if (prevData != null)
                    _arrayPool.Return(prevData);

                if (concatBuffer != null)
                    _arrayPool.Return(concatBuffer);
            }

            lock (_writerQueueLock)
            {
                _continueProcessing = false;
                Monitor.PulseAll(_writerQueueLock);
            }

            if (!_threadSend.Join(2000))
            {
                Log.Warning("Failed to stop send thread");
                _threadSend.Abort();
            }

            Log.Information("Stopping receive thread");
            _extension.HandleStop(this, GetState());

            CleanUp();
        }

        private RtmpConnectionState GetState()
        {
            return new RtmpConnectionState
            {
                NetStreams = _netStreams.Values.Select(s => s.State).ToArray(),
                ConnectionChunkId = _connectionChunkId,
                BufferState = _bufferState,
                PrevReadMessageStates = _previousReadMessageHeader.Select(s => new PrevReadMessageState { ChunkId = s.Key, MessageHeader = s.Value.ToMessageHeader() }).ToArray(),
            };
        }

        private void CleanUp()
        {
            if (_s1Data != null)
                _arrayPool.Return(_s1Data);
            _s1Data = null;
        }

        private bool IsHandshaked() => _nextProcessState > ProcessState.HandshakeC2;

        internal void SendRawData(ReadOnlyMemory<byte> data)
        {
            var buffer = _arrayPool.Rent(data.Length);
            data.CopyTo(buffer);

            lock (_writerQueueLock)
            {
                _writerQueue.Enqueue(new SendItem()
                {
                    Buffer = buffer,
                    Length = data.Length
                });
                Monitor.PulseAll(_writerQueueLock);
            }
        }

        private (bool continueProcessing, int consumed) ProcessHandshakeC0C1(ReadOnlySequence<byte> buffer, int consumed)
        {
            if (buffer.Length - consumed < 1537)
                return (false, consumed);

            var arr = _arrayPool.Rent(1537);
            try
            {
                buffer.Slice(consumed, 9).CopyTo(arr);
                consumed += 9;
                var version = arr[0];

                if (version < 3)
                    throw new ProtocolViolationException("To low version");

                if (version > 31)
                    throw new ProtocolViolationException("To high version");

                if (_s1Data == null)
                    _s1Data = _arrayPool.Rent(1528);

                _readerTimestampEpoch = NetworkBitConverter.ToUInt32(arr.AsSpan(1, 4));
                _writerTimestampEpoch = 0;
                _random.NextBytes(_s1Data.AsSpan(0, 1528));

                // s0s1
                arr.AsSpan().Clear();
                arr[0] = 3;
                NetworkBitConverter.TryGetBytes(_writerTimestampEpoch, arr.AsSpan(1, 4));
                _s1Data.AsSpan(0, 1528).CopyTo(arr.AsSpan(9));
                SendRawData(arr.AsMemory(0, 1537));

                // s2
                NetworkBitConverter.TryGetBytes(_readerTimestampEpoch, arr.AsSpan(0, 4));
                NetworkBitConverter.TryGetBytes((uint)0, arr.AsSpan(4, 4));

                SendRawData(arr.AsMemory(0, 1536));

                buffer.Slice(consumed, 1528).CopyTo(arr.AsSpan(8));
                consumed += 1528;

                _nextProcessState = ProcessState.HandshakeC2;
                return (true, consumed);
            }
            finally
            {
                _arrayPool.Return(arr);
            }
        }

        private (bool continueProcessing, int consumed) ProcessHandshakeC2(ReadOnlySequence<byte> buffer, int consumed)
        {
            if (buffer.Length - consumed < 1536)
                return (false, consumed);

            var arr = _arrayPool.Rent(1536);
            try
            {
                buffer.Slice(consumed, 1536).CopyTo(arr);
                consumed += 1536;
                var s1Timestamp = NetworkBitConverter.ToUInt32(arr.AsSpan(0, 4));
                if (s1Timestamp != _writerTimestampEpoch)
                    throw new ProtocolViolationException("s1Timestamp != _writerTimestampEpoch");

                if (!arr.AsSpan(8, 1528).SequenceEqual(_s1Data.AsSpan(0, 1528)))
                    throw new ProtocolViolationException("!arr.AsSpan(8, 1528).SequenceEqual(_s1Data.AsSpan(0, 1528))");

                HandshakePerformed();
                return (true, consumed);
            }
            finally
            {
                _arrayPool.Return(arr);
            }
        }

        private void HandshakePerformed()
        {
            _bufferProcessors.Clear();
            _nextProcessState = ProcessState.FirstByteBasicHeader;
            _bufferProcessors.Add(ProcessState.ChunkMessageHeader, ProcessChunkMessageHeader);
            _bufferProcessors.Add(ProcessState.CompleteMessage, ProcessCompleteMessage);
            _bufferProcessors.Add(ProcessState.ExtendedTimestamp, ProcessExtendedTimestamp);
            _bufferProcessors.Add(ProcessState.FirstByteBasicHeader, ProcessFirstByteBasicHeader);
        }


        internal void MultiplexMessageAsync(uint chunkStreamId, Message message)
        {
            if (!message.MessageHeader.MessageStreamId.HasValue)
            {
                throw new InvalidOperationException("cannot send message that has not attached to a message stream");
            }
            byte[] buffer = null;
            uint length = 0;
            using (var writeBuffer = new ByteBuffer())
            {
                var context = new SerializationContext()
                {
                    AmfSerializationContext = _amfSerializationContext,
                    WriteBuffer = writeBuffer
                };
                message.Serialize(context);
                length = (uint)writeBuffer.Length;
                buffer = _arrayPool.Rent((int)length);
                writeBuffer.TakeOutMemory(buffer);
            }

            try
            {
                message.MessageHeader.MessageLength = length;
                // chunking
                bool isFirstChunk = true;
                for (int i = 0; i < message.MessageHeader.MessageLength;)
                {
                    _previousWriteMessageHeader.TryGetValue(chunkStreamId, out var prevHeader);
                    var chunkHeaderType = SelectChunkType(message.MessageHeader, prevHeader, isFirstChunk);
                    isFirstChunk = false;
                    GenerateBasicHeader(chunkHeaderType, chunkStreamId, out var basicHeader, out var basicHeaderLength);
                    GenerateMesesageHeader(chunkHeaderType, message.MessageHeader, prevHeader, out var messageHeader, out var messageHeaderLength);
                    _previousWriteMessageHeader[chunkStreamId] = (MessageHeader)message.MessageHeader.Clone();
                    var headerLength = basicHeaderLength + messageHeaderLength;
                    var bodySize = (int)(length - i >= _bufferState.WriteChunkSize ? _bufferState.WriteChunkSize : length - i);

                    var chunkBuffer = _arrayPool.Rent(headerLength + bodySize);
                    try
                    {
                        basicHeader.AsSpan(0, basicHeaderLength).CopyTo(chunkBuffer);
                        messageHeader.AsSpan(0, messageHeaderLength).CopyTo(chunkBuffer.AsSpan(basicHeaderLength));
                        _arrayPool.Return(basicHeader);
                        _arrayPool.Return(messageHeader);
                        buffer.AsSpan(i, bodySize).CopyTo(chunkBuffer.AsSpan(headerLength));
                        i += bodySize;
                        var isLastChunk = message.MessageHeader.MessageLength - i == 0;

                        long offset = 0;
                        long totalLength = headerLength + bodySize;
                        long currentSendSize = totalLength;

                        while (offset != (headerLength + bodySize))
                        {
                            if (_bufferState.WriteWindowAcknowledgementSize.HasValue && _bufferState.WriteUnacknowledgedSize + currentSendSize > _bufferState.WriteWindowAcknowledgementSize.Value)
                            {
                                Log.Warning($"Sending unacknoledged data {_bufferState.WriteUnacknowledgedSize} + {currentSendSize} while window is {_bufferState.WriteWindowAcknowledgementSize.Value}");
                            }
                            SendRawData(chunkBuffer.AsMemory((int)offset, (int)currentSendSize));
                            offset += currentSendSize;
                            totalLength -= currentSendSize;

                            if (_bufferState.WriteWindowAcknowledgementSize.HasValue)
                            {
                                _bufferState.WriteUnacknowledgedSize += currentSendSize;
                            }
                        }
                        if (isLastChunk)
                        {
                            if (message.MessageHeader.MessageType == MessageType.SetChunkSize)
                            {
                                var setChunkSize = message as SetChunkSizeMessage;
                                _bufferState.WriteChunkSize = setChunkSize.ChunkSize;
                            }
                            else if (message.MessageHeader.MessageType == MessageType.SetPeerBandwidth)
                            {
                                var m = message as SetPeerBandwidthMessage;
                                _bufferState.ReadWindowAcknowledgementSize = m.WindowSize;
                            }
                            else if (message.MessageHeader.MessageType == MessageType.WindowAcknowledgementSize)
                            {
                                var m = message as WindowAcknowledgementSizeMessage;
                                _bufferState.WriteWindowAcknowledgementSize = m.WindowSize;
                            }
                        }
                    }
                    finally
                    {
                        _arrayPool.Return(chunkBuffer);
                    }
                }
            }
            finally
            {
                _arrayPool.Return(buffer);
            }
        }

        private void GenerateMesesageHeader(ChunkHeaderType chunkHeaderType, MessageHeader header, MessageHeader prevHeader, out byte[] buffer, out int length)
        {
            var timestamp = header.Timestamp;
            switch (chunkHeaderType)
            {
                case ChunkHeaderType.Type0:
                    buffer = _arrayPool.Rent(Type0Size + ExtendedTimestampLength);
                    NetworkBitConverter.TryGetUInt24Bytes(timestamp >= 0xFFFFFF ? 0xFFFFFF : timestamp, buffer.AsSpan(0, 3));
                    NetworkBitConverter.TryGetUInt24Bytes(header.MessageLength, buffer.AsSpan(3, 3));
                    NetworkBitConverter.TryGetBytes((byte)header.MessageType, buffer.AsSpan(6, 1));
                    NetworkBitConverter.TryGetBytes(header.MessageStreamId.Value, buffer.AsSpan(7, 4), true);
                    length = Type0Size;
                    break;
                case ChunkHeaderType.Type1:
                    buffer = _arrayPool.Rent(Type1Size + ExtendedTimestampLength);
                    timestamp = timestamp - prevHeader.Timestamp;
                    NetworkBitConverter.TryGetUInt24Bytes(timestamp >= 0xFFFFFF ? 0xFFFFFF : timestamp, buffer.AsSpan(0, 3));
                    NetworkBitConverter.TryGetUInt24Bytes(header.MessageLength, buffer.AsSpan(3, 3));
                    NetworkBitConverter.TryGetBytes((byte)header.MessageType, buffer.AsSpan(6, 1));
                    length = Type1Size;
                    break;
                case ChunkHeaderType.Type2:
                    buffer = _arrayPool.Rent(Type2Size + ExtendedTimestampLength);
                    timestamp = timestamp - prevHeader.Timestamp;
                    NetworkBitConverter.TryGetUInt24Bytes(timestamp >= 0xFFFFFF ? 0xFFFFFF : timestamp, buffer.AsSpan(0, 3));
                    length = Type2Size;
                    break;
                case ChunkHeaderType.Type3:
                    buffer = _arrayPool.Rent(ExtendedTimestampLength);
                    length = 0;
                    break;
                default:
                    throw new ArgumentException();
            }
            if (timestamp >= 0xFFFFFF)
            {
                NetworkBitConverter.TryGetBytes(timestamp, buffer.AsSpan(length, ExtendedTimestampLength));
                length += ExtendedTimestampLength;
            }
        }

        private void GenerateBasicHeader(ChunkHeaderType chunkHeaderType, uint chunkStreamId, out byte[] buffer, out int length)
        {
            byte fmt = (byte)chunkHeaderType;
            if (chunkStreamId >= 2 && chunkStreamId <= 63)
            {
                buffer = _arrayPool.Rent(1);
                buffer[0] = (byte)((byte)(fmt << 6) | chunkStreamId);
                length = 1;
            }
            else if (chunkStreamId >= 64 && chunkStreamId <= 319)
            {
                buffer = _arrayPool.Rent(2);
                buffer[0] = (byte)(fmt << 6);
                buffer[1] = (byte)(chunkStreamId - 64);
                length = 2;
            }
            else if (chunkStreamId >= 320 && chunkStreamId <= 65599)
            {
                buffer = _arrayPool.Rent(3);
                buffer[0] = (byte)((fmt << 6) | 1);
                buffer[1] = (byte)((chunkStreamId - 64) & 0xff);
                buffer[2] = (byte)((chunkStreamId - 64) >> 8);
                length = 3;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private ChunkHeaderType SelectChunkType(MessageHeader messageHeader, MessageHeader prevHeader, bool isFirstChunk)
        {
            if (prevHeader == null)
            {
                return ChunkHeaderType.Type0;
            }

            if (!isFirstChunk)
            {
                return ChunkHeaderType.Type3;
            }

            long currentTimestamp = messageHeader.Timestamp;
            long prevTimesatmp = prevHeader.Timestamp;

            if (currentTimestamp - prevTimesatmp < 0)
            {
                return ChunkHeaderType.Type0;
            }

            if (messageHeader.MessageType == prevHeader.MessageType &&
                messageHeader.MessageLength == prevHeader.MessageLength &&
                messageHeader.MessageStreamId == prevHeader.MessageStreamId &&
                messageHeader.Timestamp != prevHeader.Timestamp)
            {
                return ChunkHeaderType.Type2;
            }
            else if (messageHeader.MessageStreamId == prevHeader.MessageStreamId)
            {
                return ChunkHeaderType.Type1;
            }
            else
            {
                return ChunkHeaderType.Type0;
            }
        }
        private void FillHeader(ChunkHeader header)
        {
            if (!_previousReadMessageHeader.TryGetValue(header.ChunkBasicHeader.ChunkStreamId, out var prevHeader) &&
                header.ChunkBasicHeader.RtmpChunkHeaderType != ChunkHeaderType.Type0)
            {
                throw new InvalidOperationException();
            }

            switch (header.ChunkBasicHeader.RtmpChunkHeaderType)
            {
                case ChunkHeaderType.Type1:
                    header.MessageHeader.Timestamp += prevHeader.Timestamp;
                    header.MessageHeader.MessageStreamId = prevHeader.MessageStreamId;
                    break;
                case ChunkHeaderType.Type2:
                    header.MessageHeader.Timestamp += prevHeader.Timestamp;
                    header.MessageHeader.MessageLength = prevHeader.MessageLength;
                    header.MessageHeader.MessageType = prevHeader.MessageType;
                    header.MessageHeader.MessageStreamId = prevHeader.MessageStreamId;
                    break;
                case ChunkHeaderType.Type3:
                    header.MessageHeader.Timestamp = prevHeader.Timestamp;
                    header.MessageHeader.MessageLength = prevHeader.MessageLength;
                    header.MessageHeader.MessageType = prevHeader.MessageType;
                    header.MessageHeader.MessageStreamId = prevHeader.MessageStreamId;
                    break;
            }
        }


        public (bool continueProcessing, int consumed) ProcessFirstByteBasicHeader(ReadOnlySequence<byte> buffer, int consumed)
        {
            if (buffer.Length - consumed < 1)
                return (false, consumed);
            var header = new ChunkHeader()
            {
                ChunkBasicHeader = new ChunkBasicHeader(),
                MessageHeader = new MessageHeader()
            };
            _processingChunk = header;
            var arr = _arrayPool.Rent(1);
            buffer.Slice(consumed, 1).CopyTo(arr);
            consumed += 1;
            var basicHeader = arr[0];
            _arrayPool.Return(arr);
            header.ChunkBasicHeader.RtmpChunkHeaderType = (ChunkHeaderType)(basicHeader >> 6);
            header.ChunkBasicHeader.ChunkStreamId = (uint)basicHeader & 0x3F;
            if (header.ChunkBasicHeader.ChunkStreamId != 0 && header.ChunkBasicHeader.ChunkStreamId != 0x3F)
            {
                if (header.ChunkBasicHeader.RtmpChunkHeaderType == ChunkHeaderType.Type3)
                {
                    FillHeader(header);
                    _nextProcessState = ProcessState.CompleteMessage;
                    return (true, consumed);
                }
            }
            _nextProcessState = ProcessState.ChunkMessageHeader;
            return (true, consumed);
        }

        private (bool continueProcessing, int consumed) ProcessChunkMessageHeader(ReadOnlySequence<byte> buffer, int consumed)
        {
            int bytesNeed = 0;
            switch (_processingChunk.ChunkBasicHeader.ChunkStreamId)
            {
                case 0:
                    bytesNeed = 1;
                    break;
                case 0x3F:
                    bytesNeed = 2;
                    break;
            }
            switch (_processingChunk.ChunkBasicHeader.RtmpChunkHeaderType)
            {
                case ChunkHeaderType.Type0:
                    bytesNeed += Type0Size;
                    break;
                case ChunkHeaderType.Type1:
                    bytesNeed += Type1Size;
                    break;
                case ChunkHeaderType.Type2:
                    bytesNeed += Type2Size;
                    break;
            }

            if (buffer.Length - consumed <= bytesNeed)
            {
                return (false, consumed);
            }

            byte[] arr = null;
            if (_processingChunk.ChunkBasicHeader.ChunkStreamId == 0)
            {
                arr = _arrayPool.Rent(1);
                buffer.Slice(consumed, 1).CopyTo(arr);
                consumed += 1;
                _processingChunk.ChunkBasicHeader.ChunkStreamId = (uint)arr[0] + 64;
                _arrayPool.Return(arr);
            }
            else if (_processingChunk.ChunkBasicHeader.ChunkStreamId == 0x3F)
            {
                arr = _arrayPool.Rent(2);
                buffer.Slice(consumed, 2).CopyTo(arr);
                consumed += 2;
                _processingChunk.ChunkBasicHeader.ChunkStreamId = (uint)arr[1] * 256 + arr[0] + 64;
                _arrayPool.Return(arr);
            }
            var header = _processingChunk;
            switch (header.ChunkBasicHeader.RtmpChunkHeaderType)
            {
                case ChunkHeaderType.Type0:
                    arr = _arrayPool.Rent(Type0Size);
                    buffer.Slice(consumed, Type0Size).CopyTo(arr);
                    consumed += Type0Size;
                    header.MessageHeader.Timestamp = NetworkBitConverter.ToUInt24(arr.AsSpan(0, 3));
                    header.MessageHeader.MessageLength = NetworkBitConverter.ToUInt24(arr.AsSpan(3, 3));
                    header.MessageHeader.MessageType = (MessageType)arr[6];
                    header.MessageHeader.MessageStreamId = NetworkBitConverter.ToUInt32(arr.AsSpan(7, 4), true);
                    break;
                case ChunkHeaderType.Type1:
                    arr = _arrayPool.Rent(Type1Size);
                    buffer.Slice(consumed, Type1Size).CopyTo(arr);
                    consumed += Type1Size;
                    header.MessageHeader.Timestamp = NetworkBitConverter.ToUInt24(arr.AsSpan(0, 3));
                    header.MessageHeader.MessageLength = NetworkBitConverter.ToUInt24(arr.AsSpan(3, 3));
                    header.MessageHeader.MessageType = (MessageType)arr[6];
                    break;
                case ChunkHeaderType.Type2:
                    arr = _arrayPool.Rent(Type2Size);
                    buffer.Slice(consumed, Type2Size).CopyTo(arr);
                    consumed += Type2Size;
                    header.MessageHeader.Timestamp = NetworkBitConverter.ToUInt24(arr.AsSpan(0, 3));
                    break;
            }
            if (arr != null)
            {
                _arrayPool.Return(arr);
            }
            FillHeader(header);
            if (header.MessageHeader.Timestamp == 0x00FFFFFF)
            {
                _nextProcessState = ProcessState.ExtendedTimestamp;
            }
            else
            {
                _nextProcessState = ProcessState.CompleteMessage;
            }
            return (true, consumed);
        }

        private (bool continueProcessing, int consumed) ProcessExtendedTimestamp(ReadOnlySequence<byte> buffer, int consumed)
        {
            if (buffer.Length - consumed < 4)
                return (false, consumed);

            var arr = _arrayPool.Rent(4);
            buffer.Slice(consumed, 4).CopyTo(arr);
            consumed += 4;
            var extendedTimestamp = NetworkBitConverter.ToUInt32(arr.AsSpan(0, 4));
            _processingChunk.ExtendedTimestamp = extendedTimestamp;
            _processingChunk.MessageHeader.Timestamp = extendedTimestamp;
            _nextProcessState = ProcessState.CompleteMessage;
            return (true, consumed);
        }

        private (bool continueProcessing, int consumed) ProcessCompleteMessage(ReadOnlySequence<byte> buffer, int consumed)
        {
            var header = _processingChunk;
            if (!_incompleteMessageState.TryGetValue(header.ChunkBasicHeader.ChunkStreamId, out var state))
            {
                state = new MessageReadingState()
                {
                    CurrentIndex = 0,
                    MessageLength = header.MessageHeader.MessageLength,
                    Body = _arrayPool.Rent((int)header.MessageHeader.MessageLength)
                };
                _incompleteMessageState.Add(header.ChunkBasicHeader.ChunkStreamId, state);
            }

            var bytesNeed = (int)(state.RemainBytes >= _bufferState.ReadChunkSize ? _bufferState.ReadChunkSize : state.RemainBytes);

            if (buffer.Length - consumed < bytesNeed)
            {
                return (false, consumed);
            }

            if (_previousReadMessageHeader.TryGetValue(header.ChunkBasicHeader.ChunkStreamId, out var prevHeader))
            {
                if (prevHeader.MessageStreamId != header.MessageHeader.MessageStreamId)
                {
                    // inform user previous message will never be received
                    prevHeader = null;
                }
            }
            _previousReadMessageHeader[_processingChunk.ChunkBasicHeader.ChunkStreamId] = (MessageHeader)_processingChunk.MessageHeader.Clone();
            _processingChunk = null;

            buffer.Slice(consumed, bytesNeed).CopyTo(state.Body.AsSpan(state.CurrentIndex));
            consumed += bytesNeed;
            state.CurrentIndex += bytesNeed;

            if (state.IsCompleted)
            {
                _incompleteMessageState.Remove(header.ChunkBasicHeader.ChunkStreamId);
                try
                {
                    var context = new SerializationContext()
                    {
                        AmfSerializationContext = _amfSerializationContext,
                        ReadBuffer = state.Body.AsMemory(0, (int)state.MessageLength)
                    };
                    if (header.MessageHeader.MessageType == MessageType.AggregateMessage)
                    {
                        var agg = new AggregateMessage(header.MessageHeader);
                        agg.Deserialize(context);
                        foreach (var message in agg.Messages)
                        {
                            var msgContext = new SerializationContext()
                            {
                                AmfSerializationContext = _amfSerializationContext,
                                ReadBuffer = context.ReadBuffer.Slice(message.DataOffset, (int)message.DataLength)
                            };

                            var result = MessageFactory.Create(message.Header.MessageType, header.MessageHeader, msgContext);
                            if (result.msg != null)
                            {
                                // not sure this is correct! 
                                msgContext.ReadBuffer = context.ReadBuffer.Slice(result.factoryConsumed);
                                result.msg.MessageHeader = header.MessageHeader;
                                result.msg.Deserialize(msgContext);
                                _amfSerializationContext.Amf0Reader.ResetReference();
                                _amfSerializationContext.Amf3Reader.ResetReference();
                                MessageArrived(result.msg);
                            }
                        }
                    }
                    else
                    {
                        var result = MessageFactory.Create(header.MessageHeader.MessageType, header.MessageHeader, context);
                        if (result.msg != null)
                        {
                            result.msg.MessageHeader = header.MessageHeader;
                            context.ReadBuffer = context.ReadBuffer.Slice(result.factoryConsumed);
                            result.msg.Deserialize(context);
                            _amfSerializationContext.Amf0Reader.ResetReference();
                            _amfSerializationContext.Amf3Reader.ResetReference();
                            MessageArrived(result.msg);
                        }
                    }
                }
                finally
                {
                    _arrayPool.Return(state.Body);
                }
            }
            _nextProcessState = ProcessState.FirstByteBasicHeader;
            return (true, consumed);
        }

        private void MessageArrived(Message message)
        {
            if (!(message is AudioMessage || message is VideoMessage))
            {
                Log.Information($" [{message.MessageHeader.MessageStreamId}] ==>  {message}");
            }

            if (!message.MessageHeader.MessageStreamId.HasValue)
            {
                Log.Error("Null message stream id");
            }

            if (message.MessageHeader.MessageStreamId == ControlMessageStreamId)
            {
                switch (message)
                {
                    case SetChunkSizeMessage chunkSize:
                        _bufferState.ReadChunkSize = (int)chunkSize.ChunkSize;
                        break;

                    case WindowAcknowledgementSizeMessage acknowledgementSizeMessage:
                        _bufferState.ReadWindowAcknowledgementSize = acknowledgementSizeMessage.WindowSize;
                        break;

                    case SetPeerBandwidthMessage setPeerBandwidthMessage:
                        if (_bufferState.WriteWindowAcknowledgementSize.HasValue && 
                            setPeerBandwidthMessage.LimitType == LimitType.Soft && 
                            setPeerBandwidthMessage.WindowSize > _bufferState.WriteWindowAcknowledgementSize)
                            break;
                        if (_previousLimitType.HasValue &&
                            setPeerBandwidthMessage.LimitType == LimitType.Dynamic &&
                            _previousLimitType != LimitType.Hard)
                            break;
                        _previousLimitType = setPeerBandwidthMessage.LimitType;
                        _bufferState.WriteWindowAcknowledgementSize = setPeerBandwidthMessage.WindowSize;
                        SendMessage(new WindowAcknowledgementSizeMessage() { WindowSize = setPeerBandwidthMessage.WindowSize });
                        break;

                    case AcknowledgementMessage acknowledgement:
                        _bufferState.WriteUnacknowledgedSize -= acknowledgement.BytesReceived;
                        break;

                    case ConnectCommandMessage connect:
                        Connect(connect);
                        break;

                    case CloseCommandMessage _:
                        CloseConnection();
                        break;

                   case CreateStreamCommandMessage createStream:
                        CreateStream(createStream);
                        break;

                   case DeleteStreamCommandMessage deleteStream:
                        _netStreams.Remove((uint)deleteStream.StreamID); 
                        _extension.HandleDeleteStream(this, deleteStream);
                        break;
                }
            }
            else if (_netStreams.TryGetValue(message.MessageHeader.MessageStreamId.Value, out var stream))
            {
                switch (message)
                {
                    case PublishCommandMessage publish:
                        ProcessPublish(stream, publish);
                        break;

                    case DataMessage dataMessage:
                        _extension.HandleDataMessage(this, dataMessage);
                        break;

                    case AudioMessage audioMessage:
                        _extension.HandleAudioMessage(this, audioMessage);
                        break;

                    case VideoMessage videoMessage:
                        _extension.HandleVideoMessage(this, videoMessage);
                        break;
                }
            }
            else
            {
                Log.Error($"Bad message stream id {message.MessageHeader.MessageStreamId.Value}");
            }
        }

        private void ProcessPublish(NetStream stream, PublishCommandMessage publish)
        {
            //RtmpSession.SendControlMessageAsync(new StreamBeginMessage() { StreamID = MessageStream.MessageStreamId });

            stream.State.PublishingName = publish.PublishingName;
            stream.State.PublishingEncoding = publish.AmfEncodingVersion == AmfEncodingVersion.Amf0 ? 0 : 3;

            _extension.HandlePublish(this);

            SendPublishOk(stream);
        }

        private void SendPublishOk(NetStream stream)
        {
            var onStatus = new OnStatusCommandMessage(stream.State.PublishingEncoding == 0 ? AmfEncodingVersion.Amf0 : AmfEncodingVersion.Amf3 );
            onStatus.InfoObject = new AmfObject
            {
                {"level", "status" },
                {"code", "NetStream.Publish.Start" },
                {"description", "Stream is now published." },
                {"details", stream.State.PublishingName }
            };
            SendMessage(onStatus, stream.State.ChunkStreamId, stream.State.MessageStreamId);
        }

        private void CreateStream(CreateStreamCommandMessage createStream)
        {
            var stream = new NetStream(_chunkIdProvider.Get(), _messageIdProvider.Get());
            _netStreams[stream.State.MessageStreamId] = stream;

            var retCommand = new ReturnResultCommandMessage(createStream.AmfEncodingVersion);
            retCommand.IsSuccess = true;
            retCommand.TranscationID = createStream.TranscationID;
            retCommand.ReturnValue = stream.State.MessageStreamId;
            SendMessage(retCommand, _connectionChunkId);
        }

        private void CloseConnection()
        {
            if (_connectionChunkId != uint.MaxValue)
                _chunkIdProvider.Release(_connectionChunkId);
            _connectionChunkId = uint.MaxValue;
        }

        private void Connect(ConnectCommandMessage command)
        {
            ConnectionInformation = new ConnectionInformation(command.CommandObject);
            _connectionChunkId = _chunkIdProvider.Get();

            var msg = new ReturnResultCommandMessage(command.AmfEncodingVersion);
            msg.CommandObject = new AmfObject
            {
                { "capabilities", 255.00 },
                { "fmsVer", "FMS/4,5,1,484" },
                { "mode", 1.0 }
            };
            msg.ReturnValue = new AmfObject
            {
                { "code", "NetConnection.Connect.Success" },
                { "description", "Connection succeeded." },
                { "level", "status" },
            };
            msg.IsSuccess = true;
            msg.TranscationID = command.TranscationID;
            SendMessage(msg, _connectionChunkId);
        }

        public void SendMessage(Message message, uint chunkId = ControlChunkStreamId, uint messageId = ControlMessageStreamId)
        {
            message.MessageHeader.MessageStreamId = messageId;
            Log.Information($" [{messageId}]  <== {message}");
            MultiplexMessageAsync(chunkId, message);
        }
    }
}
