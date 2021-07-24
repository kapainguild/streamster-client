using DynamicStreamer.Extension;
using SharpDX.Direct3D11;
using System;
using System.Diagnostics;
using System.Threading;
using Windows.Foundation.Metadata;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace DynamicStreamer.Screen
{
    public class ScreenCaptureEngine : IDisposable
    {
        private readonly IDirectXContext _dx;
        private readonly Action<SizeInt32> _onSizeChanged;
        private SizeInt32 _initSize;

        private readonly AutoResetEvent _frameAvailable = new AutoResetEvent(false);
        private volatile bool _continueProcessing = true;

        private IDirect3DDevice _device;
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureSession _session;
        private Device _d3dDevice;
        private Texture2D _screenTexture;
        

        private SizeInt32 _requestedSize;

        public ScreenCaptureEngine(ScreenCaptureRequest request, IDirectXContext dx, Action<SizeInt32> onSizeChanged)
        {
            _dx = dx;
            _onSizeChanged = onSizeChanged;
            _initSize = request.InitialSize;

            ScreenCaptureManager.Instance.Logger.Info($"Opening capture {request.Id} ({_initSize.Width}x{_initSize.Height})");

            ScreenCaptureManager.Instance.MainThreadExecutor.Execute(() =>
            {
                _device = dx == null ? Direct3D11Helper.CreateDevice() : Direct3D11Helper.CreateDirect3DDeviceFromSharpDXDevice(dx.Device);
                _d3dDevice = Direct3D11Helper.CreateSharpDXDevice(_device);
                try
                {
                    _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                        _device,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        2,
                        request.InitialSize);

                    _framePool.FrameArrived += OnFrameArrived;
                    _session = _framePool.CreateCaptureSession(request.Item);
                    if (ApiInformation.IsApiContractPresent(typeof(Windows.Foundation.UniversalApiContract).FullName, 10))
                        SetCursor(_session, request.Cursor);

                    _session.StartCapture();
                }
                catch (Exception e)
                {
                    ScreenCaptureManager.Instance.Logger.Error(e, $"Failed to create capture");
                }
            }, true);

            _screenTexture = CreateTexture(_initSize);

            ScreenCaptureManager.Instance.Logger.Info($"Opened capture {request.Id}");
        }

        public void Resize(SizeInt32 size)
        {
            _initSize = size;
            _screenTexture?.Dispose();
            _screenTexture = CreateTexture(size);

            ScreenCaptureManager.Instance.MainThreadExecutor.Execute(() =>
            {
                if (_framePool != null)
                {
                    _framePool.Recreate(
                        _device,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized,
                        2,
                        size);
                }

            }, true);
        }

        private void SetCursor(GraphicsCaptureSession session, bool cursor)
        {
            session.IsCursorCaptureEnabled = cursor;
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            _frameAvailable.Set();
        }

        public void Interrupt()
        {
            _continueProcessing = false;
            _frameAvailable.Set();
        }

        public void Dispose()
        {
            _screenTexture?.Dispose();

            ScreenCaptureManager.Instance.MainThreadExecutor.Execute(() =>
            {
                _session?.Dispose();
                _session = null;
                if (_framePool != null)
                {
                    _framePool.FrameArrived -= OnFrameArrived;
                    _framePool.Dispose();
                    _framePool = null;
                }
                _d3dDevice?.Dispose();
                _d3dDevice = null;
                _device?.Dispose();
                _device = null;
                _frameAvailable.Dispose();
            }, false);
        }

        

        public void Read(SizeInt32 configSize, Func<IntPtr, int, int, int, int, object, bool> setPacket)
        {
            while (true)
            {
                if (!_continueProcessing)
                    throw new OperationCanceledException();


                if (configSize.Width != 0 &&
                    (configSize.Width != _initSize.Width ||
                    configSize.Height != _initSize.Height))
                {
                    Resize(configSize);
                }

                SizeInt32? newSize = null;
                bool processed = false;

                using (var frame = _framePool.TryGetNextFrame())
                {
                    if (frame != null)
                    {

                        if (frame.ContentSize.Width != _initSize.Width || frame.ContentSize.Height != _initSize.Height)
                        {
                            if (frame.ContentSize.Width != _requestedSize.Width || frame.ContentSize.Height != _requestedSize.Height)
                            {
                                _requestedSize = frame.ContentSize;
                                newSize = frame.ContentSize;
                            }
                        }
                        processed = ProcessFrame(frame, setPacket);
                    }
                }

                if (newSize != null)
                    _onSizeChanged(newSize.Value);

                if (processed == true)
                    break;

                _frameAvailable.WaitOne();
            }
        }

        private Texture2D CreateTexture(SizeInt32 size)
        {
            if (_dx == null)
            {
                var textureDesc = new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    BindFlags = BindFlags.None,
                    Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                    Width = size.Width,
                    Height = size.Height,
                    OptionFlags = ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = { Count = 1, Quality = 0 },
                    Usage = ResourceUsage.Staging
                };
                return new Texture2D(_d3dDevice, textureDesc);
            }
            return null;
        }

        private bool ProcessFrame(Direct3D11CaptureFrame frame, Func<IntPtr, int, int, int, int, object, bool> setPacket)
        {
            bool processed = true;
            using (var bitmap = Direct3D11Helper.CreateSharpDXTexture2D(frame.Surface))
            {
                if (_dx == null)
                {
                    _d3dDevice.ImmediateContext.CopyResource(bitmap, _screenTexture);
                    var data = _d3dDevice.ImmediateContext.MapSubresource(_screenTexture, 0, MapMode.Read, MapFlags.None);

                    if (_initSize.Width * 4 > data.RowPitch)
                        ScreenCaptureManager.Instance.Logger.Warining($"Width of capture is higher then Pitch {_initSize.Width * 4} > {data.RowPitch}");

                    processed = setPacket(data.DataPointer, 4, _initSize.Width, _initSize.Height, data.RowPitch, null);
                    _d3dDevice.ImmediateContext.UnmapSubresource(_screenTexture, 0);
                }
                else
                {
                    var dxRes = _dx.CreateCopy(bitmap);
                    processed = setPacket(IntPtr.Zero, 0, 0, 0, 0, dxRes);
                }
            }
            return processed;
        }
    }
}
