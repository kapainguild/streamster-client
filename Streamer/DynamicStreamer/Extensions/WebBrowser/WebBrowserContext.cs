using DynamicStreamer.Contexts;
using DynamicStreamerCef;
using System;
using System.Drawing;
using System.IO;

namespace DynamicStreamer.Extensions.WebBrowser
{
    public record WebBrowserContextSetup(string BaseFolder, int Fps, int PageWidth, int PageHeight);
    
    public class WebBrowserContext : IInputContext, IRenderTarget
    {
        public static string Name = "webbrowser";
        private const int MaxPackets = 3;
        private readonly IStreamerBase _streamer;
        private ChromiumWebBrowser _cefBrowser;

        private Size _size = new Size(1920, 1080);

        public InputConfig Config { get; private set; }

        Size IRenderTarget.Size => _size;

        private InputBufferQueue _queue;

        public WebBrowserContext(IStreamerBase streamer, MainThreadExecutor mainThreadExecutor)
        {
            _streamer = streamer;
        }

        public virtual void Dispose()
        {
            _cefBrowser?.Dispose();
            _cefBrowser = null;
            _queue.Dispose();
            _queue = null;
        }

        public void Interrupt()
        {
            _queue?.Interrupt();
        }

        public void Analyze(int duration, int streamsCount)
        {
        }

        public virtual void Open(InputSetup setup)
        {
            _queue?.Dispose();
            var contextSetup = (WebBrowserContextSetup)setup.ObjectInput;
            _size = new Size(contextSetup.PageWidth, contextSetup.PageHeight);
            _queue = new InputBufferQueue("WebBrowserQueue", _streamer, MaxPackets, setup.Dx, contextSetup.PageWidth, contextSetup.PageHeight);

            CefServer.EnsureInit(new CefConfiguration
                {
                    CachePathRoot = Path.Combine(contextSetup.BaseFolder, "cache"),
                    CachePathRequest = Path.Combine(contextSetup.BaseFolder, "cache\\request"),
                    CachePathGlobal = Path.Combine(contextSetup.BaseFolder, "cache\\global"),
                    LogVerbose = false,
                    LogFile = Path.Combine(contextSetup.BaseFolder, "wb.log")
                });

            if (_cefBrowser == null)
            {
                _cefBrowser = CefServer.CreateBrowser(setup.Input, contextSetup.Fps, this);
            }
            else
                _cefBrowser.Load(setup.Input);

            Config = new InputConfig(
                new InputStreamProperties[]
                {
                    new InputStreamProperties
                    {
                        CodecProps = new CodecProperties
                        {
                            codec_type = AVMediaType.AVMEDIA_TYPE_VIDEO,
                            codec_id = Core.Const.CODEC_ID_RAWVIDEO,
                            width = _size.Width,
                            height = _size.Height,
                            bits_per_coded_sample = 4*8,
                            format = setup.Dx == null ?  Core.Const.PIX_FMT_BGRA : Core.PIX_FMT_INTERNAL_DIRECTX,

                            extradata = new byte[1024]
                        }
                    }
                }
            );
        }

        public void Read(Packet packet, InputSetup setup)
        {
            _queue.Dequeue(packet);
        }

        void IRenderTarget.OnPaint(bool main, IntPtr buffer, int width, int height)
        {
            if (main)
                _queue?.Enqueue(buffer, width * height * 4);
        }

        void IRenderTarget.OnPopupShow(bool show)
        {
            //TODO:NextRelease
        }

        void IRenderTarget.OnPopupSize(int left, int top, int width, int height)
        {
            //TODO:NextRelease
        }
    }
}
