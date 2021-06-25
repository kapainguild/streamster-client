using DynamicStreamer.DirectXHelpers;
using System;
using System.Runtime.InteropServices;

namespace DynamicStreamer
{
    public class Frame : IPayload
    {
        public IntPtr Handle { get; set; }

        public FrameProperties Properties = new FrameProperties();

        public RefCounted<DirectXResource> DirectXResourceRef { get; set; }

        [DllImport(Core.DllName)] private static extern IntPtr Frame_Create();
        [DllImport(Core.DllName)] private static extern void Frame_Delete(IntPtr handle);
        [DllImport(Core.DllName)] private static extern void Frame_Unref(IntPtr handle);
        [DllImport(Core.DllName)] private static extern IntPtr Frame_CopyContentFrom(IntPtr handle, IntPtr from);
        [DllImport(Core.DllName)] private static extern IntPtr Frame_CopyContentFromAndSetPts(IntPtr handle, IntPtr from, long pts);
        [DllImport(Core.DllName)] private static extern IntPtr Frame_RescaleTimebase(IntPtr handle, ref AVRational from, ref AVRational to, ref FrameProperties frameProperties);
        [DllImport(Core.DllName)] private static extern IntPtr Frame_Init(IntPtr handle, int width, int height, int pixelFormat, long pts, int planesCount, ref FramePlaneDesc planes, ref FrameProperties frameProperties);
        [DllImport(Core.DllName)] private static extern IntPtr Frame_GenerateSilence(IntPtr handle, long pts, ref FrameProperties frameProperties);
        
        public void InitFromDirectX(DirectXResource currentFrame, long currentFramePts)
        {
            DirectXResourceRef = new RefCounted<DirectXResource>(currentFrame);

            Properties = new FrameProperties
            {
                Pts = currentFramePts
            };
        }

        public void InitFromDirectX(RefCounted<DirectXResource> currentFrame, long currentFramePts)
        {
            DirectXResourceRef = currentFrame.AddRef();

            Properties = new FrameProperties
            {
                Pts = currentFramePts
            };
        }

        [DllImport(Core.DllName)] private static extern IntPtr Frame_SetPts(IntPtr handle, long pts);

        public Frame()
        {
            Handle = Frame_Create();
        }

        public void Unref()
        {
            DirectXResourceRef?.RemoveRef();
            DirectXResourceRef = null;
            Frame_Unref(Handle);
            Properties = new FrameProperties();
        }

        public void CopyContentFrom(IPayload fromPayload)
        {
            Frame from = (Frame)fromPayload;
            Frame_CopyContentFrom(Handle, from.Handle);
            Properties = from.Properties;

            DirectXResourceRef?.RemoveRef();
            DirectXResourceRef = from.DirectXResourceRef?.AddRef();
        }

        public void CopyContentFromAndSetPts(IPayload fromPayload, long pts)
        {
            Frame from = (Frame)fromPayload;
            Frame_CopyContentFromAndSetPts(Handle, from.Handle, pts);
            Properties = from.Properties;
            Properties.Pts = pts;

            DirectXResourceRef?.RemoveRef();
            DirectXResourceRef = from.DirectXResourceRef?.AddRef();
        }

        internal void GenerateSilence(long pts)
        {
            Frame_GenerateSilence(Handle, pts, ref Properties);
        }

        internal void Init(int width, int height, int pixelFormat, long pts, FramePlaneDesc[] planes)
        {
            Frame_Init(Handle, width, height, pixelFormat, pts, planes.Length, ref planes[0], ref Properties);
        }

        public void SetPts(long pts)
        {
            Frame_SetPts(Handle, pts);
            Properties.Pts = pts;
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                Frame_Delete(Handle);
                Handle = IntPtr.Zero;
            }
        }

        public void RescaleTimebase(ref AVRational from, ref AVRational to)
        {
            Frame_RescaleTimebase(Handle, ref from, ref to, ref Properties);
        }

        public long GetPts() => Properties.Pts;

        
    }
}
