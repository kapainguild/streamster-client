using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace DynamicStreamer.Contexts
{
    public class OutputSetup
    {
        public string Type { get; set; }

        public string Output { get; set; }

        public string Options { get; set; }

        public OutputStreamProperties[] OutputStreamProps { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is OutputSetup setup)
            {
                return Type == setup.Type &&
                   Output == setup.Output &&
                   Options == setup.Options &&
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

            Core.Checked(res, "Open output failed");
        }

        private int OnReadInterruptCallback()
        {
            //note.  it is called from Open and from Read and from Write_trailer!
            if (_interrupted && _inOpenOrRead)
                return 1;
            return 0;
        }

        public ErrorCodes Write(Packet packet, int stream)
        {
            _inOpenOrRead = true;
            var res = OutputContext_Write(_handle, packet.Handle, stream);
            _inOpenOrRead = false;
            return res;
        }

        public void CloseOutput()
        {
            IsOpened = false;
            OutputContext_Delete(_handle);
            _handle = OutputContext_Create();
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
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
