using DynamicStreamer;
using DynamicStreamer.DirectXHelpers;
using DynamicStreamer.Queues;
using Serilog;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.Direct3D9;
using Streamster.ClientCore.Cross;
using Streamster.ClientCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        private int _occludedCounter = 0;
        private bool _occludedState = false;

        private Queue<RefCounted<FromPool<Frame>>> _currentQueue = new Queue<RefCounted<FromPool<Frame>>>();
        private IntPtr _windowHandle;
        private bool _reinitSurfaces;
        private IWindowStateManager _windowStateManager;

        public Action RecoverCommand { get; }

        public DirectXPresenter()
        {
            Source = _d3dimage;

            CompositionTarget.Rendering += CompositionTarget_Rendering;
            Unloaded += DirectXPresenter_Unloaded;
            DataContextChanged += DirectXPresenter_DataContextChanged;

            RecoverCommand = () => _reinitSurfaces = true;
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

        public void ShowFrame(FrameOutputData fromPool, IWindowStateManager windowStateManager)
        {
            if ((_traceCounter++) % 300 == 0 && fromPool.Trace != null)
                Core.LogInfo($"trace ui: {fromPool.Trace.GetDump()}");

            lock (_lock)
            {
                _windowStateManager = windowStateManager;
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
                    InitDx9();
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
            if (res.Texture2D == null)
            {
                Core.LogWarning("DisplayAsTexture failed as Texture2D == null");
                return;
            }

            res.GetDx().RunOnContext(ctx =>
            {
                ctx.CopyResource(res.Texture2D, _sharedResource);
                ctx.Flush();
            }, "CopyToUI");

            if (_d3dimage.TryLock(TimeSpan.FromMilliseconds(1500)))
            {
                _d3dimage.AddDirtyRect(new Int32Rect(0, 0, res.Texture2D.Description.Width, res.Texture2D.Description.Height));
                _d3dimage.Unlock();
            }
            else
                Core.LogWarning("Failed to Lock DirectXPresenter/d3dimage");

            if (Source != _d3dimage)
            {
                Core.LogInfo("Assigning new D3DImage");
                Source = _d3dimage;
            }
        }

        private void OpenSharedResourceIfNeeded(DirectXResource res)
        {
            try
            {
                bool reinit = false;
                _occludedCounter++;

                if (_occludedCounter % 1 == 0) // every 200 ms
                {
                    var state = _device?.CheckDeviceState(_windowHandle) ?? DeviceState.Ok;
                    bool occludedState = state == DeviceState.PresentOccluded; // happens when windows is locked; then we could get black screen in preview d3dImage

                    reinit = _occludedState && !occludedState; // reinit if change from bad -> good
                    if (reinit)
                    {
                        Core.LogInfo("occluded -> normal");

                        if (_windowStateManager.IsMinimized())
                            _windowStateManager.IsMinimizedChanged += OnwindowStateManagerIsMinimizedChanged;
                        else
                            _ = ReinitSurfacesWithDelay();
                    }

                    if (!_occludedState && occludedState)
                        Core.LogInfo("normal -> occluded");

                    _occludedState = occludedState;
                }

                if (_sharedResource == null ||
                    _sharedResource.Description.Width != res.Texture2D.Description.Width ||
                    _sharedResource.Description.Height != res.Texture2D.Description.Height ||
                    _sharedResourceOwner != res.GetDx() ||
                    _reinitSurfaces)
                {
                    Core.LogInfo("Initing DX Presenter surface");
                    Source = null;
                    _reinitSurfaces = false;

                    _surfaceD9?.Dispose();
                    _sharedResource?.Dispose();

                    _surfaceD9 = null;
                    _sharedResource = null;
                    _sharedResourceOwner = null;


                    IntPtr handle = IntPtr.Zero;
                    _surfaceD9 = Surface.CreateRenderTarget(_device, res.Texture2D.Description.Width, res.Texture2D.Description.Height, Format.A8R8G8B8, MultisampleType.None, 0, true, ref handle);
                    if (handle == IntPtr.Zero)
                    {
                        Core.LogWarning("DirectX 9 Device failed to create Surface. Reinit Devices");
                        _device?.Dispose();
                        _direct3d?.Dispose();
                        _device = null;
                        _direct3d = null;
                        InitDx9();
                        _surfaceD9 = Surface.CreateRenderTarget(_device, res.Texture2D.Description.Width, res.Texture2D.Description.Height, Format.A8R8G8B8, MultisampleType.None, 0, true, ref handle);

                        if (handle == IntPtr.Zero)
                        {
                            Core.LogWarning("DirectX 9 Device failed to create Surface after recreation.");

                        }
                    }
                    _sharedResource = res.GetDx().Device.OpenSharedResource<Texture2D>(handle);
                    _sharedResourceOwner = res.GetDx();

                    _d3dimage = new D3DImage();
                    _d3dimage.Lock();
                    _d3dimage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _surfaceD9.NativePointer);
                    _d3dimage.Unlock();

                    Core.LogInfo("Inited DX Presenter surface");
                }
            }
            catch (Exception e)
            {
                Core.LogError(e, "Failed to open shared resource");
            }
        }

        private void OnwindowStateManagerIsMinimizedChanged(object sender, EventArgs e)
        {
            if (!_windowStateManager.IsMinimized())
            {
                _windowStateManager.IsMinimizedChanged -= OnwindowStateManagerIsMinimizedChanged;
                _reinitSurfaces = true;
            }
        }

        private async Task ReinitSurfacesWithDelay()
        {
            await Task.Delay(500);
            _reinitSurfaces = true;
        }

        private void InitDx9()
        {
            (_direct3d, _device) = DirectXContextFactory.CreateD3D9Devies(GetWindowHandle());
        }

        private IntPtr GetWindowHandle()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                var window = Window.GetWindow(this);
                _windowHandle = new WindowInteropHelper(window).Handle;
            }
            return _windowHandle;
        }
    }

}
