using System;
using System.Runtime.InteropServices;

namespace DynamicStreamer.Contexts
{
    class VideoBlenderHelper : IDisposable
    {
        private IntPtr _handle;

        public int InputOffset { get; set; }

        [DllImport(Core.DllName)] private static extern IntPtr VideoBlenderContext_Create();
        [DllImport(Core.DllName)] private static extern void VideoBlenderContext_Delete(IntPtr handle);
        [DllImport(Core.DllName)] private static extern int VideoBlenderContext_Init(IntPtr handle, IntPtr frame, int blendRgb);
        [DllImport(Core.DllName)] private static extern int VideoBlenderContext_Add(IntPtr handle, IntPtr frame, int x, int y, int src_y_offset, int src_y_count);
        [DllImport(Core.DllName)] private static extern int VideoBlenderContext_Get(IntPtr handle, IntPtr frame, long pts, ref FrameProperties frameProperties);

        public VideoBlenderHelper()
        {
            _handle = VideoBlenderContext_Create();
        }

        public void Init(Frame frame, int blendType)
        {
            VideoBlenderContext_Init(_handle, frame.Handle, blendType);
        }

        public void Add(Frame frame, int x, int y, int src_y_offset, int src_y_count)
        {
            VideoBlenderContext_Add(_handle, frame.Handle, x, y, src_y_offset, src_y_count);
        }

        public void Get(Frame frame, long pts)
        {
            VideoBlenderContext_Get(_handle, frame.Handle, pts, ref frame.Properties);
        }

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                VideoBlenderContext_Delete(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
