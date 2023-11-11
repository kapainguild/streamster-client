using DynamicStreamer.DirectXHelpers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicStreamer
{
    public class Packet : IPayload, IDisposable
    {
        public IntPtr Handle { get; set; }

        public PacketProperties Properties = new PacketProperties();

        public RefCounted<DirectXResource> DirectXResourceRef { get; set; }

        [DllImport(Core.DllName)] private static extern IntPtr Packet_Create();
        [DllImport(Core.DllName)] private static extern void Packet_Delete(IntPtr handle);
        [DllImport(Core.DllName)] private static extern void Packet_Unref(IntPtr handle);
        [DllImport(Core.DllName)] private static extern void Packet_CopyContentFrom(IntPtr handle, IntPtr from);
        [DllImport(Core.DllName)] private static extern void Packet_InitFromBuffer(IntPtr handle, ref byte buffer, int length);
        [DllImport(Core.DllName)] private static extern void Packet_InitFromBuffer2(IntPtr handle, IntPtr buffer, int length, long pts);
        [DllImport(Core.DllName)] private unsafe static extern void Packet_InitFromBuffer5(IntPtr handle, byte* buffer, int length, long pts, int streamIndex, int iFrame);
        [DllImport(Core.DllName)] private static extern void Packet_InitFromBuffer3(IntPtr handle, ref byte buffer, int length, long pts, ref PacketProperties packetProperties);
        [DllImport(Core.DllName)] private static extern int Packet_InitFromBuffer4(IntPtr handle, IntPtr buffer, int bitPerPixel, int width, int height, int sourceWidth, long pts, int checkForZero);
        [DllImport(Core.DllName)] private static extern void Packet_SetPts(IntPtr handle, long pts);
        [DllImport(Core.DllName)] private static extern void Packet_RescaleTimebase(IntPtr handle, ref AVRational from, ref AVRational to, ref PacketProperties packetProperties);
        [DllImport(Core.DllName)] private static extern void Packet_CopyToBuffer(IntPtr handle, ref byte buffer);


        public Packet()
        {
            Handle = Packet_Create();
        }

        public void CopyContentFrom(IPayload fromPacket)
        {
            Packet from = (Packet)fromPacket;
            Packet_CopyContentFrom(Handle, from.Handle);
            Properties = from.Properties;

            DirectXResourceRef?.RemoveRef();
            DirectXResourceRef = from.DirectXResourceRef?.AddRef();
        }

        public void CopyContentFromAndSetPts(IPayload fromPacket, long pts)
        {
            CopyContentFrom(fromPacket);
            SetPts(pts);
        }

        public void Unref()
        {
            DirectXResourceRef?.RemoveRef();
            DirectXResourceRef = null;
            Properties = new PacketProperties();
            Packet_Unref(Handle);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                Packet_Delete(Handle);
                Handle = IntPtr.Zero;
            }
        }

        public long GetPts()
        {
            return Properties.Pts;
        }

        internal void InitFromBuffer(byte[] buffer)
        {
            Packet_InitFromBuffer(Handle, ref buffer[0], buffer.Length);
        }

        public void CopyToBuffer(byte[] buffer)
        {
            Packet_CopyToBuffer(Handle, ref buffer[0]);
        }

        internal void InitFromBuffer(IntPtr buffer, int length, long pts)
        {
            Packet_InitFromBuffer2(Handle, buffer, length, pts);
            Properties.Pts = pts;
        }

        internal void InitFromBuffer(byte[] buffer, long pts)
        {
            Packet_InitFromBuffer3(Handle, ref buffer[0], buffer.Length, pts, ref Properties);
        }

        internal void InitFromDirectX(object dxRes, long pts)
        {
            DirectXResourceRef = new RefCounted<DirectXResource>((DirectXResource)dxRes);
            Properties.Pts = pts;
        }

        internal void InitFromBuffer(ReadOnlyMemory<byte> buffer, int start, int length, long pts, int streamIndex, bool iFrame)
        {
            unsafe
            {
                var span = buffer.Span;
                fixed (byte* pointer = &span.GetPinnableReference())
                {
                    Packet_InitFromBuffer5(Handle, pointer, length, pts, streamIndex, iFrame ? 1 : 0);
                }
            }

            Properties.Pts = pts;
            Properties.StreamIndex = streamIndex;
            Properties.Flags = iFrame ? 1 : 0; 
            Properties.Size = length;
        }


        internal bool InitFromBuffer(IntPtr buffer, int bitPerPixel, int width, int height, int sourceWidth, long pts, bool checkForZero)
        {
            int res = Packet_InitFromBuffer4(Handle, buffer, bitPerPixel, width, height, sourceWidth, pts, checkForZero ? 1 : 0);
            Properties.Pts = pts;

            return res != 0;
        }

        public void RescaleTimebase(ref AVRational from, ref AVRational to)
        {
            Packet_RescaleTimebase(Handle, ref from, ref to, ref Properties);
        }

        public void SetFlag(int flag) 
        { 
            // ! flag does not go to unmanaged properties
            Properties.Flags |= flag;
        }

        public void SetPts(long pts)
        {
            Packet_SetPts(Handle, pts);
            Properties.Pts = pts;
        }
    }
}
