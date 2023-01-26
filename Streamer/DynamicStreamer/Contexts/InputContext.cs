using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace DynamicStreamer.Contexts
{
    public class InputContext : IInputContext
    {
        public static AVRational s_translate_to_time_base = new AVRational { num = 1, den = 10_000_000 };

        private IntPtr _handle;
        
        private bool _interrupted = false;
        private DateTime _startOperationTime = DateTime.MaxValue;
        private bool _firstStreamOnly;
        private long _overloadCounter = 0;
        private InputContextCallbackItem _inputContextCallbackItem;

        private IInputTimeAdjuster _timeAdjuster = new InputTimeAdjusterNone();

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
            _timeAdjuster = CreateAdjuster(setup.AdjustInputType);
            _firstStreamOnly = setup.FirstStreamOnly;
            _inputContextCallbackItem?.Reset();
            _inputContextCallbackItem = new InputContextCallbackItem(this);
            int res = InputContext_Open(
                _handle,
                Core.StringToBytes(setup.Type),
                Core.StringToBytes(setup.Input),
                Core.StringToBytes(setup.Options),
                ref s_translate_to_time_base,
                _inputContextCallbackItem.GetFunction());
            _startOperationTime = DateTime.MaxValue;
            CheckedCall(res);
        }

        private IInputTimeAdjuster CreateAdjuster(AdjustInputType adjustInputType) => adjustInputType switch
        {
            AdjustInputType.Adaptive => new InputTimeAdjuster(),
            AdjustInputType.CurrentTime => new InputTimeAdjusterCurrentTime(),
            AdjustInputType.AdaptiveNetwork => new InputNetworkTimeAdjuster(),
            _ => new InputTimeAdjusterNone()
        };

        public int OnReadInterruptCallback()
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

                // overload prevention
                if (packet.Properties.StreamIndex == 0)
                {
                    if ((_overloadCounter & 7) == 0)
                        Thread.Sleep(10);

                    _overloadCounter++;
                }

                if (packet.Properties.StreamIndex == 0 || !_firstStreamOnly) // ignore sound for receiver mode
                {
                    packet.SetPts(_timeAdjuster.Add(packet.Properties.Pts, currentTime));
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
            if (streams < streamsCount)
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
                _inputContextCallbackItem?.Reset();
                _inputContextCallbackItem = null;

                InputContext_Delete(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }


    public class InputContextCallbackItem
    {
        private static List<InputContextCallbackItem> _disposed = new List<InputContextCallbackItem>();

        delegate int ReadInterruptCallbackFunction();

        private ReadInterruptCallbackFunction _readInterruptCallbackFunction;

        private InputContext _parent;

        public InputContextCallbackItem(InputContext parent)
        {
            _parent = parent;
            _readInterruptCallbackFunction = new ReadInterruptCallbackFunction(CallBack); 
        }

        private int CallBack()
        {
            return _parent?.OnReadInterruptCallback() ?? 1;
        }

        public void Reset()
        {
            lock (_disposed)
            {
                _disposed.Add(this);

                if (_disposed.Count > 100_000)
                    _disposed = new List<InputContextCallbackItem>(_disposed.Skip(10_000));
            }
            _parent = null;
        }

        internal IntPtr GetFunction()
        {
            return Marshal.GetFunctionPointerForDelegate(_readInterruptCallbackFunction);
        }
    }
}
