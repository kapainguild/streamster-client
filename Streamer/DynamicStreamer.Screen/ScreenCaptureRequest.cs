using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;

namespace DynamicStreamer.Screen
{
    public class ScreenCaptureItem
    {
        public ScreenCaptureItem(string name, IntPtr handle, bool isProgram, int w, int h)
        {
            Name = name;
            Handle = handle;
            IsProgram = isProgram;
            Width = w;
            Height = h;
        }

        public string Name { get; set; }
        public IntPtr Handle { get; set; }
        public bool IsProgram { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }
    }

    public class ScreenCaptureRequest
    {
        public string Id { get; set; }

        public bool Cursor { get; set; }

        public SizeInt32 InitialSize { get; set; }

        public SizeInt32? RunitimeSize { get; set; }

        public GraphicsCaptureItem Item { get; set; }

        public string DebugName { get; set; }

        public override bool Equals(object obj)
        {
            return obj is ScreenCaptureRequest request &&
                   Id == request.Id &&
                   Cursor == request.Cursor;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ Cursor.GetHashCode();
        }

        public override string ToString() => $"Cursot:{Cursor} I:{InitialSize.Width}x{InitialSize.Height} R:{RunitimeSize?.Width}x{RunitimeSize?.Height} Item:{DebugName}";
    }

    public class GraphicsCaptureItemWrapper
    {
        public GraphicsCaptureItemWrapper(GraphicsCaptureItem wrapped, string prefix)
        {
            Wrapped = wrapped;
            Prefix = prefix;
        }

        public GraphicsCaptureItem Wrapped { get; set; }
        public string Prefix { get; }
    }
}
