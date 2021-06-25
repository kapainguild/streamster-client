using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Linq;
using Device = SharpDX.Direct3D11.Device;

namespace DynamicStreamer.DirectXHelpers
{
    public static class DirectXContextFactory
    {
        private static SharpDX.DXGI.Factory1 s_factory;
        private static AdapterInfo[] s_adapters;

        public static AdapterInfo[] GetAdapters()
        {
            if (s_adapters == null)
            {
                var factory = GetOrCreateFactory();
                if (factory == null)
                    return new AdapterInfo[0];
                s_adapters = factory.Adapters1.Where(s => (s.Description1.Flags & SharpDX.DXGI.AdapterFlags.Software) == 0).Select(s => new AdapterInfo(s.Description.Description, GetVendor(s.Description.VendorId), s)).ToArray();
            }
            return s_adapters;
        }

        private static AdapterVendor GetVendor(int vendorId)
        {
            if (vendorId == 0x8086)
                return AdapterVendor.Intel;

            if (vendorId == 0x10de)
                return AdapterVendor.NVidia;


            return AdapterVendor.Other;
        }

        public static DirectXContext Create(VideoRenderOptions options, IStreamerBase streamerBase)
        {
            var device = CreateDevice(options);
            if (device.Item1 != null)
                return new DirectXContext(device.Item1, options, device.Item2, device.Item3, streamerBase);
            return null;
        }

        private static bool TryCreate(string name, ref bool deviceCreationFailed, ref (Device, AdapterInfo) device, Func<(Device, AdapterInfo)> creator)
        {
            try
            {
                device = creator();
                Core.LogInfo($"DirectX {name} created");
                return true;
            }
            catch (Exception e)
            {
                Core.LogError(e, $"Create '{name}' failed");
                deviceCreationFailed = true;
            }
            return false;
        }

        public static (Device, AdapterInfo, bool) CreateDevice(VideoRenderOptions options)
        {
            Core.LogInfo($"Creating renderer for options ({options})");
            if (options.Type == VideoRenderType.SoftwareFFMPEG)
                return (null, null, false);

            (Device, AdapterInfo) deviceInfo = (null, null);
            bool deviceCreationFailed = false;

            string windowAdapter = options.MainWindowHandle == IntPtr.Zero ? "" : GetHwndAdapter(options.MainWindowHandle);

            if (options.Type == VideoRenderType.HardwareSpecific)
            {
                if (TryCreate("with specified adapter", ref deviceCreationFailed, ref deviceInfo, () => CreateDeviceWithAdapter(options.Adapter)))
                    return PrepareResult(deviceInfo, windowAdapter);
            }

            if (options.Type == VideoRenderType.HardwareAuto || deviceCreationFailed)
            {
                if (TryCreate("auto from hwnd", ref deviceCreationFailed, ref deviceInfo, 
                    () => CreateDeviceWithAdapter(windowAdapter)))
                    return PrepareResult(deviceInfo, windowAdapter);
            }

            if (options.Type == VideoRenderType.HardwareAuto || deviceCreationFailed)
            {
                if (TryCreate("auto", ref deviceCreationFailed, ref deviceInfo,
                                () => CreateDeviceFromFirst(GetAdapters())))
                    return PrepareResult(deviceInfo, windowAdapter);
            }

            if (options.Type == VideoRenderType.SoftwareDirectX || deviceCreationFailed)
            {
                if (TryCreate("software adapter", ref deviceCreationFailed, ref deviceInfo,
                              () => (new Device(SharpDX.Direct3D.DriverType.Warp, DeviceCreationFlags.BgraSupport), null)))
                    return PrepareResult(deviceInfo, windowAdapter);
            }

            return PrepareResult(deviceInfo, windowAdapter);  
        }

        private static (Device, AdapterInfo, bool) PrepareResult((Device d, AdapterInfo a) deviceInfo, string windowAdapter)
        {
            return (deviceInfo.d, deviceInfo.a, deviceInfo.a != null && deviceInfo.a.Name == windowAdapter);
        }

        private static (Device, AdapterInfo) CreateDeviceFromFirst(AdapterInfo[] adapterInfos)
        {
            if (adapterInfos != null && adapterInfos.Length > 0)
                return CreateDeviceWithAdapter(adapterInfos[0].Name);
            else
                return (new Device(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport), null);
        }

        private static string GetHwndAdapter(IntPtr mainWindowHandle)
        {
            // based on how we create wpf device
            var d9 = CreateD3D9Devies(mainWindowHandle);
            if (d9.d3d9 == null)
                throw new InvalidOperationException($"Failed to create D3D9 for {mainWindowHandle}");

            using var a = d9.d3d;
            using var b = d9.d3d9;

            var adapter = a.Adapters.FirstOrDefault()?.Details?.Description;
            if (adapter == null)
                throw new InvalidOperationException($"Failed to create D3D9: no adapters");
            return adapter;
        }

        private static (Device, AdapterInfo) CreateDeviceWithAdapter(string name)
        {
            var adapters = GetAdapters();
            if (adapters == null)
                throw new InvalidOperationException($"Factory not found.");
            var adapter = adapters.FirstOrDefault(s => s.Name == name);
            if (adapter != null)
            {
                if (adapter.Name == "Microsoft Basic Render Driver")
                {
                    Core.LogInfo($"Creating directx on '{name}' ignored towards software rendering");
                    return (null, null);
                }
                else
                {
                    Core.LogInfo($"Creating directx on '{name}'");
                    return (new Device(adapter.adapter), adapter);
                }
            }
            else throw new InvalidOperationException($"Adapter '{name}' not found.");
        }

        private static SharpDX.DXGI.Factory1 GetOrCreateFactory()
        {
            if (s_factory == null)
            {
                try
                {
                    s_factory = new SharpDX.DXGI.Factory1();
                    Core.LogInfo("Hardware adapters: " + string.Join(", ", GetAdapters().Select(s => $"'{s.Name}'")));
                }
                catch (Exception e)
                {
                    Core.LogError(e, "Error creating factory");
                }
            }
            return s_factory;
        }

        public static (SharpDX.Direct3D9.Direct3DEx d3d, SharpDX.Direct3D9.DeviceEx d3d9) CreateD3D9Devies(IntPtr hwnd)
        {
            SharpDX.Direct3D9.Direct3DEx d3d = null;
            try
            {
                var presentparams = new SharpDX.Direct3D9.PresentParameters
                {
                    Windowed = true,
                    SwapEffect = SharpDX.Direct3D9.SwapEffect.Discard,
                    DeviceWindowHandle = hwnd,
                    PresentationInterval = SharpDX.Direct3D9.PresentInterval.Default
                };

                var deviceFlags = SharpDX.Direct3D9.CreateFlags.HardwareVertexProcessing | 
                                    SharpDX.Direct3D9.CreateFlags.Multithreaded | 
                                    SharpDX.Direct3D9.CreateFlags.FpuPreserve;

                d3d = new SharpDX.Direct3D9.Direct3DEx();
                var d = new SharpDX.Direct3D9.DeviceEx(d3d, 0, SharpDX.Direct3D9.DeviceType.Hardware, IntPtr.Zero, deviceFlags, presentparams);
                return (d3d, d);
            }
            catch (Exception e)
            {
                Core.LogError(e, "Failed to init Dx 9");
                d3d?.Dispose();
            }
            return (null, null);
        }
    }

    public record AdapterInfo(string Name, AdapterVendor Vendor, Adapter adapter);

    public enum AdapterVendor { Intel, NVidia, Other };
}
