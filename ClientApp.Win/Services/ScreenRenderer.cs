using Streamster.ClientCore;
using Streamster.ClientCore.Cross;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Streamster.ClientApp.Win.Services
{
    public class ScreenRenderer : IScreenRenderer
    {
        private int _version = int.MinValue;

        public Property<WriteableBitmap> Screen { get; } = new Property<WriteableBitmap>();

        public void ShowFrame(int width, int height, int version, byte[] buffer)
        {
            if (version != _version)
            {
                _version = version;
                WriteableBitmap bmp = Screen.Value;
                if (Screen.Value == null || height != Screen.Value.PixelHeight ||
                    width != Screen.Value.PixelWidth)
                {
                    bmp = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr24, null);
                    Screen.Value = bmp;
                }
                bmp.WritePixels(new Int32Rect(0, 0, width, height), buffer, width * 3, 0);
            }
        }
    }
}
