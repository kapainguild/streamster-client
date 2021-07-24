using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicStreamer.Contexts
{
    class EncoderContextQsvDx : IEncoderContext
    {
        public const string TypeName = "QsvDx";

        private IntPtr _handle;
        private bool _opened;

        public EncoderBitrate _currentBitrate;
        private DirectXContext _dx;
        private static object s_lock = new object();
        private static EncoderContextQsvDx s_prevVersion = null;

        [DllImport(Core.DllName)] private static extern IntPtr EncoderContextQsvDx_Create();
        [DllImport(Core.DllName)] private static extern void EncoderContextQsvDx_Delete(IntPtr handle);
        [DllImport(Core.DllName)] private static extern int EncoderContextQsvDx_Open(IntPtr handle, byte[] options, ref EncoderSpec codecProperties, ref EncoderBitrate encoderBitrate, IntPtr device, IntPtr deviceContext, ref CodecProperties outCodecProperties);
        [DllImport(Core.DllName)] private static extern int EncoderContextQsvDx_Write(IntPtr handle, IntPtr textureSharedHandle, long Pts, int iFrame);
        [DllImport(Core.DllName)] private static extern ErrorCodes EncoderContextQsvDx_Read(IntPtr handle, IntPtr packet, ref PacketProperties packetProperties);
        [DllImport(Core.DllName)] private static extern void EncoderContextQsvDx_UpdateBitrate(IntPtr handle, ref EncoderBitrate encoderBitrate);

        public EncoderContextQsvDx()
        {
            lock (s_lock)
            {
                if (s_prevVersion != null)
                    s_prevVersion.Dispose();
                _handle = EncoderContextQsvDx_Create();
                s_prevVersion = this;
            }
        }

        public EncoderConfig Config { get; set; }

        public int Open(EncoderSetup setup)
        {
            lock (s_lock)
            {
                if (_handle != IntPtr.Zero)
                {
                    EncoderConfig config = new EncoderConfig();
                    var res = EncoderContextQsvDx_Open(_handle,
                        Core.StringToBytes(setup.Options),
                        ref setup.EncoderSpec,
                        ref setup.EncoderBitrate, setup.DirectXContext.Device.NativePointer, setup.DirectXContext.CtxNativePointer, ref config.CodecProps);
                    _opened = res >= 0;
                    _currentBitrate = setup.EncoderBitrate;
                    _dx = setup.DirectXContext;
                    config.EncoderProps.pix_fmt = Core.PIX_FMT_INTERNAL_DIRECTX;
                    Config = config;
                    return res;
                }
                else return (int)ErrorCodes.ContextIsNotOpened;
            }
        }

        public int Write(Frame frame, bool enforceIFrame)
        {
            lock (s_lock)
            {
                if (_opened && _handle != IntPtr.Zero)
                {
                    if (frame.DirectXResourceRef.Instance.Texture2D == null)
                    {
                        Core.LogWarning("Ignore empty Texture2d");
                        return 0;
                    }

                    var res = EncoderContextQsvDx_Write(_handle, frame.DirectXResourceRef.Instance.GetSharedHandle(), frame.GetPts(), enforceIFrame ? 1 : 0);
                    if (res != 0)
                    {
                        _dx.Broken(new Exception($"EncoderContextQsvDx_Write failed with {res}"));
                        return (int)ErrorCodes.InternalErrorUnknown3;
                    }
                    return 0;
                }
                else
                    return (int)ErrorCodes.ContextIsNotOpened;
            }
        }

        public ErrorCodes Read(Packet packet)
        {
            lock (s_lock)
            {
                if (_opened && _handle != IntPtr.Zero)
                    return EncoderContextQsvDx_Read(_handle, packet.Handle, ref packet.Properties);
                else
                    return ErrorCodes.ContextIsNotOpened;
            }
        }

        public void Dispose()
        {
            lock (s_lock)
            {
                if (_handle != IntPtr.Zero)
                {
                    EncoderContextQsvDx_Delete(_handle);
                    _handle = IntPtr.Zero;
                }
            }
        }

        public void UpdateBitrate(EncoderBitrate encoderBitrate)
        {
            lock (s_lock)
            {
                if (!_currentBitrate.Equals(encoderBitrate) && _handle != IntPtr.Zero)
                {
                    _currentBitrate = encoderBitrate;
                    EncoderContextQsvDx_UpdateBitrate(_handle, ref encoderBitrate);
                }
            }
        }
    }
}
