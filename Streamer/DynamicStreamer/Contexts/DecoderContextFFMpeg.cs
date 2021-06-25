using System;
using System.Runtime.InteropServices;

namespace DynamicStreamer
{
    public class DecoderContextFFMpeg : IDecoderContext
    {
        public const string Type = nameof(DecoderContextFFMpeg);

        private IntPtr _handle;
        private bool _opened;

        [DllImport(Core.DllName)] private static extern IntPtr DecoderContext_Create();
        [DllImport(Core.DllName)] private static extern void DecoderContext_Delete(IntPtr handle);
        [DllImport(Core.DllName)] private static extern int DecoderContext_Open(IntPtr handle, ref CodecProperties codecProperties, ref DecoderProperties decoderProperties, ref CodecProperties outCodecProperties);
        [DllImport(Core.DllName)] private static extern int DecoderContext_Write(IntPtr handle, IntPtr packet);
        [DllImport(Core.DllName)] private static extern ErrorCodes DecoderContext_Read(IntPtr handle, IntPtr frame, ref FrameProperties frameProperties);

        public DecoderContextFFMpeg()
        {
            _handle = DecoderContext_Create();
        }

        public DecoderConfig Config { get; set; }

        public int Open(DecoderSetup setup)
        {
            DecoderConfig result = new DecoderConfig();
            var codecProps = setup.CodecProps;
            var res = DecoderContext_Open(_handle, ref codecProps, ref result.DecoderProperties, ref result.CodecProperties); 
            _opened = res >= 0;
            Config = result;
            return res;
        }

        public int Write(Packet packet)
        {
            if (_opened)
                return DecoderContext_Write(_handle, packet.Handle);
            else
                return (int)ErrorCodes.ContextIsNotOpened;
        }

        public ErrorCodes Read(Frame frame)
        {
            if (_opened)
                return DecoderContext_Read(_handle, frame.Handle, ref frame.Properties);
            else
                return ErrorCodes.ContextIsNotOpened;

            //if (frame.Properties.Height > 0 && frame.Properties.Format == 1)
            //{
            //    byte[] d = new byte[4147200];
            //    Marshal.Copy(frame.Properties.DataPtr0, d, 0 , d.Length);

            //    File.WriteAllBytes("c:\\-\\frame.yuyv", d);
            //}
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                DecoderContext_Delete(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
