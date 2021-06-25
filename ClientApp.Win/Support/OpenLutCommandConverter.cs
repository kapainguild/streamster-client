using DynamicStreamer.Helpers;
using Serilog;
using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Streamster.ClientApp.Win.Support
{
    class OpenLutCommandConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Action<string, byte[]> action && parameter is string filter)
                return new TransientCommand(() =>
                {
                    Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
                    dlg.Filter = filter;

                    if (dlg.ShowDialog() == true)
                    {
                        bool failed = false;
                        try
                        {
                            var bytes = File.ReadAllBytes(dlg.FileName);

                            if (Path.GetExtension(dlg.FileName)?.ToLower() == ".png")
                            {
                                BitmapImage biImg = new BitmapImage();
                                using MemoryStream ms = new MemoryStream(bytes);
                                biImg.BeginInit();
                                biImg.StreamSource = ms;
                                biImg.EndInit();

                                if (biImg.PixelWidth != 512 || biImg.PixelHeight != 512)
                                    throw new InvalidOperationException($"Wrong LUT size {biImg.PixelWidth}x{biImg.PixelHeight}");
                            }
                            else
                            {
                                var data = CubeParser.Read(bytes);
                                if (data == null)
                                    throw new InvalidOperationException("Bad cube format");
                            }

                            action(dlg.FileName, bytes);
                        }
                        catch (Exception e)
                        {
                            Log.Warning(e, $"Failed to open image file");
                            failed = true;
                        }
                        if (failed)
                            action(dlg.FileName, null);
                    }
                });

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
