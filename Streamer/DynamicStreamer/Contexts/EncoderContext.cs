using System;
using System.Runtime.InteropServices;

namespace DynamicStreamer
{

    public class EncoderContext : IEncoderContext
    {
        private IntPtr _handle;
        private bool _opened;
        private EncoderSetup _setup;

        [DllImport(Core.DllName)] private static extern IntPtr EncoderContext_Create();
        [DllImport(Core.DllName)] private static extern void EncoderContext_Delete(IntPtr handle);
        [DllImport(Core.DllName)] private static extern int EncoderContext_Open(IntPtr handle, byte[] name, byte[] options, ref EncoderSpec codecProperties, ref EncoderBitrate encoderBitrate, ref EncoderProperties decoderProperties, ref CodecProperties outCodecProperties);
        [DllImport(Core.DllName)] private static extern int EncoderContext_Write(IntPtr handle, IntPtr packet, int iFrame);
        [DllImport(Core.DllName)] private static extern ErrorCodes EncoderContext_Read(IntPtr handle, IntPtr packet, ref PacketProperties packetProperties);
        [DllImport(Core.DllName)] private static extern void EncoderContext_UpdateBitrate(IntPtr handle, ref EncoderBitrate encoderBitrate);

        public EncoderContext()
        {
            _handle = EncoderContext_Create();
        }

        public EncoderConfig Config { get; set; }

        public int Open(EncoderSetup setup)
        {
            EncoderConfig config = new EncoderConfig();
            var res = EncoderContext_Open(_handle, Core.StringToBytes(setup.Name), Core.StringToBytes(setup.Options), ref setup.EncoderSpec, ref setup.EncoderBitrate, ref config.EncoderProps, ref config.CodecProps);
            _opened = res >= 0;
            _setup = setup;
            Config = config;
            return res;
        }

        public int Write(Frame frame, bool enforceIFrame)
        {
            if (!_opened)
                Open(_setup);

            if (_opened)
                return EncoderContext_Write(_handle, frame.Handle, enforceIFrame ? 1 : 0);
            else
                return (int)ErrorCodes.ContextIsNotOpened;
        }

        public ErrorCodes Read(Packet packet)
        {
            if (_opened)
                return EncoderContext_Read(_handle, packet.Handle, ref packet.Properties);
            else
                return ErrorCodes.ContextIsNotOpened;
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                EncoderContext_Delete(_handle);
                _handle = IntPtr.Zero;
            }
        }

        public void UpdateBitrate(EncoderBitrate encoderBitrate)
        {
            EncoderContext_UpdateBitrate(_handle, ref encoderBitrate);
        }
    }

}
