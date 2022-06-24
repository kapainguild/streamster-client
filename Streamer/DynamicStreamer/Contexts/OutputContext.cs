using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace DynamicStreamer.Contexts
{
    public class OutputSetup
    {
        public string Type { get; set; }

        public string Output { get; set; }

        public string Options { get; set; }

        public int TimeoutMs { get; set; } // 0 - no timeout check

        public OutputStreamProperties[] OutputStreamProps { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is OutputSetup setup)
            {
                return Type == setup.Type &&
                   Output == setup.Output &&
                   Options == setup.Options &&
                   TimeoutMs == setup.TimeoutMs &&
                   OutputStreamProps.SequenceEqual(setup.OutputStreamProps);
            }
            return false;
        }

        public override int GetHashCode() => HashCode.Combine(Type, Output, Options, OutputStreamProps);

        public override string ToString() => $"{Type}-{Output}";
    }

    public class OutputContext : IDisposable
    {
        delegate int ReadInterruptCallbackFunction();

        private IntPtr _handle;
        private ReadInterruptCallbackFunction _readInterruptCallbackFunction;
        private bool _interrupted;
        private DateTime _startOperationTime = DateTime.MaxValue;
        private int _timeoutMs = 0;
        private string _title = "Unknown";

        private bool _inOpenOrRead = false;

        [DllImport(Core.DllName)] private static extern IntPtr OutputContext_Create();
        [DllImport(Core.DllName)] private static extern void OutputContext_Delete(IntPtr handle);
        [DllImport(Core.DllName)] private static extern int OutputContext_Open(IntPtr handle, byte[] type, byte[] output, byte[] options, int streamCount, OutputStreamProperties[] codecProps, IntPtr readInterruptCallbackFunction);
        [DllImport(Core.DllName)] private static extern ErrorCodes OutputContext_Write(IntPtr handle, IntPtr hPacket, int stream);

        public bool IsOpened { get; private set; }

        public OutputContext()
        {
            _handle = OutputContext_Create();
        }

        public void Open(OutputSetup setup)
        {
            _readInterruptCallbackFunction = new ReadInterruptCallbackFunction(OnReadInterruptCallback);
            _inOpenOrRead = true;
            _timeoutMs = setup.TimeoutMs;
            int res = OutputContext_Open(
                _handle,
                Core.StringToBytes(setup.Type),
                Core.StringToBytes(setup.Output),
                Core.StringToBytes(setup.Options),
                setup.OutputStreamProps.Length,
                setup.OutputStreamProps,
                Marshal.GetFunctionPointerForDelegate(_readInterruptCallbackFunction));
            IsOpened = res >= 0;
            _inOpenOrRead = false;
            _title = $"({setup.Type} {setup.Output})";

            Core.Checked(res, $"Open output failed {_title}");
        }

        private int OnReadInterruptCallback()
        {
            if (_inOpenOrRead) //Note.  it is called from Open and from Read and from Write_trailer!
            {
                if (_interrupted)
                    return 1;

                if (_timeoutMs > 0 && (DateTime.UtcNow - _startOperationTime).TotalMilliseconds > _timeoutMs)
                {
                    Core.LogError($"Long write (> {_timeoutMs}ms) detected for {_title}");
                    return 1;
                }
            }
            return 0;
        }

        public ErrorCodes Write(Packet packet, int stream)
        {
            _inOpenOrRead = true;
            _startOperationTime = DateTime.UtcNow;
            var res = OutputContext_Write(_handle, packet.Handle, stream);
            _startOperationTime = DateTime.MaxValue;
            _inOpenOrRead = false;

            if (res == ErrorCodes.TimeoutOrInterrupted && _interrupted)
                throw new OperationCanceledException();
            return res;
        }

        public void CloseOutput()
        {
            IsOpened = false;
            Core.LogInfo($"Closing output {_title}");
            OutputContext_Delete(_handle);
            _handle = OutputContext_Create();
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                Core.LogInfo($"Disposing output {_title}");
                OutputContext_Delete(_handle);
                _handle = IntPtr.Zero;
            }
        }

        internal void Interrupt()
        {
            _interrupted = true;
        }
    }
}
