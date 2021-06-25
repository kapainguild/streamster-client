using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DynamicStreamer.DirectXHelpers
{
    public class TextureLoader
    {
        
        // the code below failing due to GC
        /*public static SharpDX.WIC.BitmapSource LoadBitmap(DirectXContext directXContext, Stream stream)
        {
            var bitmapDecoder = new SharpDX.WIC.BitmapDecoder(
                directXContext.ImagingFactory2,
                stream,
                SharpDX.WIC.DecodeOptions.CacheOnLoad);

            var formatConverter = new SharpDX.WIC.FormatConverter(directXContext.ImagingFactory2);

            formatConverter.Initialize(
                bitmapDecoder.GetFrame(0),
                SharpDX.WIC.PixelFormat.Format32bppPRGBA,
                SharpDX.WIC.BitmapDitherType.None,
                null,
                0.0,
                SharpDX.WIC.BitmapPaletteType.Custom);

            return formatConverter;
        }*/

        public static DirectXResource CreateTexture2DFromBitmap(DirectXContext directXContext, SharpDX.WIC.BitmapSource bitmapSource)
        {
            // Allocate DataStream to receive the WIC image pixels
            int stride = bitmapSource.Size.Width * 4;
            using (var buffer = new SharpDX.DataStream(bitmapSource.Size.Height * stride, true, true))
            {
                try
                {
                    // Copy the content of the WIC to the buffer
                    bitmapSource.CopyPixels(stride, buffer);
                }
                catch (Exception e)
                {
                    Core.LogError(e, "Failed to load image into Dx");
                }

                return directXContext.Pool.Get("loadbitmap", DirectXResource.Desc(bitmapSource.Size.Width,
                                                             bitmapSource.Size.Height,
                                                             SharpDX.DXGI.Format.R8G8B8A8_UNorm,
                                                             SharpDX.Direct3D11.BindFlags.ShaderResource,
                                                             SharpDX.Direct3D11.ResourceUsage.Immutable),
                                               new SharpDX.DataRectangle(buffer.DataPointer, stride));
               
            }
        }

        public static DirectXResource Load(DirectXContext dx, byte[] buffer)
        {
            using (MemoryStream stream = new MemoryStream(buffer))
            {
                var bitmapDecoder = new SharpDX.WIC.BitmapDecoder(
                dx.ImagingFactory2,
                stream,
                SharpDX.WIC.DecodeOptions.CacheOnLoad);

                var formatConverter = new SharpDX.WIC.FormatConverter(dx.ImagingFactory2);

                formatConverter.Initialize(
                    bitmapDecoder.GetFrame(0),
                    SharpDX.WIC.PixelFormat.Format32bppPRGBA,
                    SharpDX.WIC.BitmapDitherType.None,
                    null,
                    0.0,
                    SharpDX.WIC.BitmapPaletteType.Custom);
                //var bmp = TextureLoader.LoadBitmap(dx, stream);
                return TextureLoader.CreateTexture2DFromBitmap(dx, formatConverter);
            }
        }
    }
}
