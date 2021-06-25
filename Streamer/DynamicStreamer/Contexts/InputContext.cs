using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace DynamicStreamer.Contexts
{
    

    public class InputContext : IInputContext
    {
        delegate int ReadInterruptCallbackFunction();

        private IntPtr _handle;
        private ReadInterruptCallbackFunction _readInterruptCallbackFunction;
        private bool _interrupted = false;
        private DateTime _startOperationTime = DateTime.MaxValue;
        private AdjustInputType _adjustInputType;
        private long _overloadCounter = 0;

        private InputTimeAdjuster _timeAdjuster = new InputTimeAdjuster();

        [DllImport(Core.DllName)] private static extern IntPtr InputContext_Create();
        [DllImport(Core.DllName)] private static extern void InputContext_Delete(IntPtr handle);
        [DllImport(Core.DllName)] private static extern int InputContext_Open(IntPtr handle, byte[] type, byte[] input, byte[] options, ref AVRational translate_to_time_base, IntPtr readInterruptCallbackFunction);
        [DllImport(Core.DllName)] private static extern int InputContext_Read(IntPtr handle, IntPtr hPacket, ref PacketProperties props);
        [DllImport(Core.DllName)] private static extern int InputContext_Analyze(IntPtr handle, int durationMs);
        [DllImport(Core.DllName)] private static extern int InputContext_GetStreamInfo(IntPtr handle, int stream, ref InputStreamProperties codecProperties);

        public InputConfig Config { get; private set; }

        public InputContext()
        {
            _handle = InputContext_Create();
        }

        public void Open(InputSetup setup)
        {
            _interrupted = false;
            _startOperationTime = DateTime.UtcNow;
            _adjustInputType = setup.AdjustInputType;
            _readInterruptCallbackFunction = new ReadInterruptCallbackFunction(OnReadInterruptCallback);
            var translate_to_time_base = new AVRational { num = 1, den = 10_000_000 };
            int res = InputContext_Open(
                _handle,
                Core.StringToBytes(setup.Type),
                Core.StringToBytes(setup.Input),
                Core.StringToBytes(setup.Options),
                ref translate_to_time_base,
                Marshal.GetFunctionPointerForDelegate(_readInterruptCallbackFunction));
            _startOperationTime = DateTime.MaxValue;
            CheckedCall(res);
        }

        private int OnReadInterruptCallback()
        {
            //note.  it is called from Open and from Read
            if (_interrupted)
                return 1;
            if ((DateTime.UtcNow - _startOperationTime).TotalSeconds > 10)
                return 1;
            return 0; 
        }

        public void Read(Packet packet, InputSetup setup)
        {
            while (true)
            {
                _startOperationTime = DateTime.UtcNow;
                int res = InputContext_Read(_handle, packet.Handle, ref packet.Properties);
                var currentTime = Core.GetCurrentTime();
                _startOperationTime = DateTime.MaxValue;

                CheckedCall(res);

                if (packet.Properties.StreamIndex == 0) // ignore sound for receiver mode
                {
                    if ((_overloadCounter & 7) == 0)
                        Thread.Sleep(10);

                    _overloadCounter++;

                    if (_adjustInputType == AdjustInputType.Adaptive)
                    {
                        packet.SetPts(_timeAdjuster.Add(packet.Properties.Pts, currentTime));
                    }
                    else if (_adjustInputType == AdjustInputType.CurrentTime)
                    {
                        packet.SetPts(currentTime);
                    }
                    break;
                }

                if (_interrupted)
                    throw new OperationCanceledException();
            }
        }

        public void Analyze(int duration, int streamsCount)
        {
            int streams = InputContext_Analyze(_handle, duration);
            CheckedCall(streams);
            if (streams != streamsCount)
                throw new DynamicStreamerException($"Unexpected number of streams {streams} vs {streamsCount}");

            if (streams > 0)
            {
                var result = new InputStreamProperties[streams];
                for (int q = 0; q < streams; q++)
                {
                    CheckedCall(InputContext_GetStreamInfo(_handle, q, ref result[q]));

                    var codecProps = result[q].CodecProps;
                    if (codecProps.codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                    {
                        if (codecProps.height == 0)
                            throw new DynamicStreamerException($"Height/weight is not obtained for stream {q}");

                    }
                    else if (codecProps.codec_type == AVMediaType.AVMEDIA_TYPE_AUDIO)
                    {
                        if (codecProps.sample_rate <= 0)
                            throw new DynamicStreamerException($"sample_rate is not obtained for stream {q}");
                    }
                    else throw new DynamicStreamerException($"Unexpected codec_type {codecProps.codec_type} at stream {q}");
                }

                Config = new InputConfig(result);
            }
        }

        private void CheckedCall(int errorCode, [CallerMemberName]string caller = null)
        {
            if (errorCode < 0)
            {
                InputContext_Delete(_handle);
                _handle = InputContext_Create();
            }

            Core.Checked(errorCode, "Input failed");
        }

        public void Interrupt()
        {
            _interrupted = true;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                InputContext_Delete(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
