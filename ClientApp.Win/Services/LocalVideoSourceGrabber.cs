using DirectShowLib;
using Serilog;
using Streamster.ClientData.Model;
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Streamster.ClientApp.Win.Services
{
    class LocalVideoSourceGrabber : ISampleGrabberCB
    {
        private DateTime _last = DateTime.MinValue;
        private int _height = 0;
        private int _width = 0;
        MemoryStream _stream = new MemoryStream();
        private readonly Action<VideoInputPreview> _callback;
        private readonly string _name;
        private readonly bool _getFrames;
        private readonly ISampleGrabber _grabber;

        public LocalVideoSourceGrabber(string name, bool getFrames, ISampleGrabber grabber, Action<VideoInputPreview> callback)
        {
            _callback = callback;
            _name = name;
            _getFrames = getFrames;
            _grabber = grabber;
        }

        public int SampleCB(double SampleTime, IMediaSample pSample) => 0;

        public int BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
        {
            DateTime now = DateTime.UtcNow;
            if ((now - _last).TotalMilliseconds > 150) // just to avoid issues with bad cameras
            {
                _last = now;
                try
                {
                    if (_width == 0)
                    {
                        var mediaType = new AMMediaType();
                        _grabber.GetConnectedMediaType(mediaType);
                        LocalVideoSourceManager.GetMediaTypeInfo(mediaType, out _height, out _width, out var _, out var _, out var _);

                        if (_width == 0 || _height == 0)
                            throw new InvalidOperationException($"Unable to GetMediaTypeInfo");
                    }

                    if (_getFrames)
                    {
                        BitmapSource image = BitmapSource.Create(
                                _width,
                                _height,
                                96,
                                96,
                                PixelFormats.Bgr24,
                                null,
                                pBuffer,
                                BufferLen,
                                _width * 3);

                        JpegBitmapEncoder encoder = new JpegBitmapEncoder { QualityLevel = _width > 500 ? 30 : 50 };

                        encoder.Frames.Add(BitmapFrame.Create(image));

                        _stream.Position = 0;
                        encoder.Save(_stream);

                        var buffer = new byte[_stream.Position];
                        Array.Copy(_stream.GetBuffer(), buffer, _stream.Position);
                        _callback(new VideoInputPreview
                        {
                            Data = buffer,
                            W = _width,
                            H = _height
                        });
                    }
                    else
                        _callback(null);
                }
                catch (Exception e)
                {
                    Log.Warning(e, $"Grabbing of '{_name}' failed");
                }
            }
            return 0;
        }

    }
}
