using Serilog;
using Streamster.ClientCore.Cross;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace Streamster.ClientApp.Win.Services
{
    public class ImageHelper : IImageHelper
    {
        public (int width, int height) GetSize(byte[] data)
        {
            try
            {
                BitmapImage biImg = new BitmapImage();
                MemoryStream ms = new MemoryStream(data);
                biImg.BeginInit();
                biImg.StreamSource = ms;
                biImg.EndInit();
                return (biImg.PixelWidth, biImg.PixelHeight);
            }
            catch
            {
                return (12, 9);
            }
        }
    }
}
