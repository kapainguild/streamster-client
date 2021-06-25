using DynamicStreamer;
using DynamicStreamer.DirectXHelpers;
using DynamicStreamer.Queues;
using Serilog;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.Direct3D9;
using Streamster.ClientCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Frame = DynamicStreamer.Frame;

namespace Streamster.ClientApp.Win.Controls
{
    public class DirectXPresenter : Image, IScreenRenderer
    {
        private D3DImage _d3dimage = new D3DImage();
        private WriteableBitmap _softwareImage;
        private Direct3DEx _direct3d;
        private DeviceEx _device;
        private Surface _surfaceD9;
        private Texture2D _sharedResource;
        private DirectXContext _sharedResourceOwner;
        private DirectXContext _sharedResourceOwnerAttempted;
        private DirectXContext _cpuTextureOwner;
        private IScreenRendererHost _host;
        private RefCounted<FromPool<Frame>> _displayingFrame;
        private readonly object _lock = new object();
        private bool _deviceWasTryingToInit;
        private DirectXResource _cpuTexture;
        private int _traceCounter = 0;

        private Queue<RefCounted<FromPool<Frame>>> _currentQueue = new Queue<RefCounted<FromPool<Frame>>>();

        public DirectXPresenter()
        {
            Source = _d3dimage;

            CompositionTarget.Rendering += CompositionTarget_Rendering;
            Unloaded += DirectXPresenter_Unloaded;
            DataContextChanged += DirectXPresenter_DataContextChanged;
        }

        private void DirectXPresenter_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is IScreenRendererHost host)
            {
                if (_host == null)
                {
                    _host = host;
                    _host.Register(this);
                }
            }
            else
            {
                if (_host != null)
                    _host.Register(null);
            }
        }

        private void DirectXPresenter_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_host != null)
                _host.Register(null);

            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            lock (_lock)
            {
                _currentQueue.ToList().ForEach(s => s.RemoveRef());
                _currentQueue.Clear();
                _displayingFrame?.RemoveRef();
                _displayingFrame = null;
            }
        }

        public void ShowFrame(FrameOutputData fromPool)
        {
            if ((_traceCounter++) % 300 == 0 && fromPool.Trace != null)
                Core.LogInfo($"trace ui: {fromPool.Trace.GetDump()}");

            lock (_lock)
            {
                _currentQueue.Enqueue(new RefCounted<FromPool<Frame>>(fromPool.Frame));
                if (_currentQueue.Count > 2)
                {
                    var old = _currentQueue.Dequeue();
                    old.RemoveRef();
                }
            }
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            //Core.LogInfo("Frame out");

            var previousShown = _displayingFrame;

            lock (_lock)
            {
                if (_currentQueue.Count == 0)
                {
                    return;
                }
                _displayingFrame = _currentQueue.Dequeue();
            }
            //Core.LogInfo("Frame out");
            try
            {
                if (_host != null) // means in fact we are initialized
                    DisplayFrame(_displayingFrame);
            }
            finally
            {
                previousShown?.RemoveRef();
            }
        }
        private void DisplayFrame(RefCounted<FromPool<Frame>> rframe)
        {
            var frame = rframe.Instance.Item;
            var dxRef = frame.DirectXResourceRef;

            if (dxRef != null)
            {
                if (!_deviceWasTryingToInit)
                {
                    _deviceWasTryingToInit = true;
                    InitDx9(dxRef.Instance);
                }
                if (_device != null)
                {
                    if (_sharedResource == null)
                    {
                        if (dxRef.Instance.GetDx() != _sharedResourceOwnerAttempted)
                        {
                            _sharedResourceOwnerAttempted = dxRef.Instance.GetDx();
                            OpenSharedResourceIfNeeded(dxRef.Instance);
                        }
                    }
                    else
                        OpenSharedResourceIfNeeded(dxRef.Instance);
                }

                if (_sharedResource != null)
                    DisplayAsTexture(dxRef.Instance);
                else
                    DownloadAndDisplayAsBitmap(dxRef.Instance);
            }
            else
                DisplayAsBitmap(frame);
        }

        private void DisplayAsBitmap(Frame frame)
        {
            int width = frame.Properties.Width;
            int height = frame.Properties.Height;

            if (width > 0 && height > 0)
            {
                PrepareSoftwareImage(width, height, PixelFormats.Bgr24);

                _softwareImage.WritePixels(new Int32Rect(0, 0, width, height), frame.Properties.DataPtr0, height * width * 3, width * 3);

                if (Source != _softwareImage)
                    Source = _softwareImage;
            }
            else
                Log.Error("wrong DisplayAsBitmap");
        }

        private void PrepareSoftwareImage(int width, int height, PixelFormat format)
        {
            if (_softwareImage == null ||
                _softwareImage.PixelWidth != width ||
                _softwareImage.PixelHeight != height ||
                _softwareImage.Format != format)
            {
                _softwareImage = new WriteableBitmap(width, height, 96, 96, format, null);
            }
        }

        private void DownloadAndDisplayAsBitmap(DirectXResource res)
        {
            int width = res.Texture2D.Description.Width;
            int height = res.Texture2D.Description.Height;
            var dx = res.GetDx();

            PrepareSoftwareImage(width, height, PixelFormats.Bgra32);

            if (_cpuTexture == null ||
                _cpuTexture.Texture2D == null ||
                _cpuTexture.Texture2D.Description.Width != width ||
                _cpuTexture.Texture2D.Description.Height != height ||
                _cpuTextureOwner != dx)
            {
                _cpuTexture?.Dispose();
                _cpuTextureOwner = dx;
                _cpuTexture = dx.Pool.Get("uiCpu", DirectXResource.Desc(width, height, SharpDX.DXGI.Format.B8G8R8A8_UNorm, BindFlags.None, ResourceUsage.Staging, ResourceOptionFlags.None, CpuAccessFlags.Read));
            }

            DataBox db = new DataBox();
            dx.RunOnContext(ctx =>
            {
                ctx.CopyResource(res.Texture2D, _cpuTexture.Texture2D);
                ctx.Flush();
                db = ctx.MapSubresource(_cpuTexture.Texture2D, 0, MapMode.Read, MapFlags.None);
            }, "Download for ui");

            if (db.SlicePitch > 0)
                _softwareImage.WritePixels(new Int32Rect(0, 0, width, height), db.DataPointer, db.SlicePitch, db.RowPitch);
            dx.RunOnContext(ctx => ctx.UnmapSubresource(_cpuTexture.Texture2D, 0), "Unmap for ui");

            if (Source != _softwareImage)
                Source = _softwareImage;
        }

        private void DisplayAsTexture(DirectXResource res)
        {
            res.GetDx().RunOnContext(ctx =>
            {
                ctx.CopyResource(res.Texture2D, _sharedResource);
                ctx.Flush();
            }
            , "CopyToUI");
            _d3dimage.Lock();

            _d3dimage.AddDirtyRect(new Int32Rect(0, 0, res.Texture2D.Description.Width, res.Texture2D.Description.Height));
            _d3dimage.Unlock();

            if (Source != _d3dimage)
                Source = _d3dimage;
        }

        private void OpenSharedResourceIfNeeded(DirectXResource res)
        {
            try
            {
                if (_sharedResource == null ||
                    _sharedResource.Description.Width != res.Texture2D.Description.Width ||
                    _sharedResource.Description.Height != res.Texture2D.Description.Height ||
                    _sharedResourceOwner != res.GetDx())
                {
                    _surfaceD9?.Dispose();
                    _sharedResource?.Dispose();

                    _surfaceD9 = null;
                    _sharedResource = null;
                    _sharedResourceOwner = null;


                    IntPtr handle = IntPtr.Zero;
                    _surfaceD9 = Surface.CreateRenderTarget(_device, res.Texture2D.Description.Width, res.Texture2D.Description.Height, Format.A8R8G8B8, MultisampleType.None, 0, true, ref handle);
                    _sharedResource = res.GetDx().Device.OpenSharedResource<Texture2D>(handle);
                    _sharedResourceOwner = res.GetDx();

                    _d3dimage.Lock();
                    _d3dimage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _surfaceD9.NativePointer);
                    _d3dimage.Unlock();
                }
            }
            catch (Exception e)
            {
                Core.LogError(e, "Failed to open shared resource");
            }
        }

        private void InitDx9(DirectXResource res)
        {

            var window = Window.GetWindow(this);
            var windowHandle = new WindowInteropHelper(window).Handle;

            (_direct3d, _device) = DirectXContextFactory.CreateD3D9Devies(windowHandle);
        }
    }

}
