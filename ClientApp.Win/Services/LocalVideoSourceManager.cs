using DirectShowLib;
using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Streamster.ClientApp.Win.Services
{
    class LocalVideoSourceManager : ILocalVideoSourceManager
    {
        private bool _initialLogging = true;

        public async Task<LocalVideoSource[]> GetVideoSourcesAsync()
        {
            return await Task.Run(() => GetVideoSources());
        }

        private LocalVideoSource[] GetVideoSources()
        {
            var devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            var res =  devices.Select(s => GetVideoSource(s)).ToArray();
            _initialLogging = false;
            return res;
        }

        private LocalVideoSource GetVideoSource(DsDevice s)
        {
            var caps = GetCapabilities(s);

            var state = caps.state;
            if (ObsHelper.IsObsAudioVideoAndItIsOff(s.Name))
                state = InputDeviceState.NotStarted;

            return new LocalVideoSource
            {
                Name = s.Name,
                Id = s.DevicePath,
                Type = s.DevicePath.ToLowerInvariant().Contains("?\\usb#") ? InputDeviceType.USB : InputDeviceType.Virtual,
                State = state,
                Capabilities = caps.caps
            };
        }

        public static void AddCaptureFilter(IFilterGraph2 filterGraph, DsDevice dsDevice, out IBaseFilter baseFilter)
        {
            var iid = new Guid("56a86895-0ad4-11ce-b03a-0020af0ba770");
            dsDevice.Mon.BindToObject(null, null, ref iid, out var rawFilter);
            baseFilter = (IBaseFilter)rawFilter;
            var local = baseFilter;
            Checked(() => filterGraph.AddFilter(local, "captureFilter"), "AddFilter", dsDevice);
        }

        public static void ReleaseComObject(object obj)
        {
            if (obj != null)
                Marshal.ReleaseComObject(obj);
        }

        private (LocalVideoSourceCapability[] caps, InputDeviceState state) GetCapabilities(DsDevice device)
        {
            if (_initialLogging)
                Log.Information($"Caps {device.Name}: getting");
            var list = new List<LocalVideoSourceCapability>();
            IntPtr pCaps = IntPtr.Zero;

            IFilterGraph2 filterGraph2 = null;
            IBaseFilter sourceFilter = null;
            IAMStreamConfig streamConfig = null;
            object pin = null;
            InputDeviceState state = InputDeviceState.Ready;
            try
            {
                filterGraph2 = new FilterGraph() as IFilterGraph2;
                if (filterGraph2 == null)
                    throw new NotSupportedException("filter2 is null");

                LocalVideoSourceManager.AddCaptureFilter(filterGraph2, device, out sourceFilter);

                pin = DsFindPin.ByCategory(sourceFilter, PinCategory.Capture, 0);

                if (pin == null)
                    pin = sourceFilter;

                streamConfig = pin as IAMStreamConfig;
                if (streamConfig == null)
                    throw new NotSupportedException("pin is null");

                int count = 0;
                int size = 0;
                Checked(() => streamConfig.GetNumberOfCapabilities(out count, out size), "GetNumberOfCapabilities", null);

                if (count <= 0)
                    throw new NotSupportedException("This video source does not report capabilities.");
                if (size != Marshal.SizeOf(typeof(VideoStreamConfigCaps)))
                    throw new NotSupportedException("Unable to retrieve video source capabilities. This video source requires a larger VideoStreamConfigCaps structure.");

                // Alloc memory for structure
                pCaps = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(VideoStreamConfigCaps)));

                for (int i = 0; i < count; i++)
                {
                    AMMediaType mediaType = null;
                    Checked(() => streamConfig.GetStreamCaps(i, out mediaType, pCaps), "GetStreamCaps", null);

                    VideoStreamConfigCaps caps = (VideoStreamConfigCaps)Marshal.PtrToStructure(pCaps, typeof(VideoStreamConfigCaps));

                    var format = GetMediaTypeInfo(mediaType, out var height, out var width, out var compression, out var videoInfoHeader, out var videoInfoHeader2);

                    var result = new LocalVideoSourceCapability()
                    {
                        MaxF = GetFps(caps.MinFrameInterval),
                        MinF = GetFps(caps.MaxFrameInterval),
                        Fmt = format,
                        W = width,
                        H = height,
                    };

                    list.Add(result);
                }
            }
            catch (UnauthorizedAccessException e)
            {
                Log.Warning(e, $"Error during retreiving caps for '{device.Name}' (Locked)");
                state = InputDeviceState.Locked;
            }
            catch (Exception e)
            {
                Log.Error(e, $"Error during retreiving caps for '{device.Name}'");
                state = InputDeviceState.Failed;
            }
            finally
            {
                if (pCaps != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pCaps);
            }

            try
            {
                ReleaseComObject(sourceFilter);
                ReleaseComObject(filterGraph2);
                ReleaseComObject(streamConfig);
                ReleaseComObject(pin);
            }
            catch (Exception e)
            {
                Log.Error(e, $"ReleaseComObject('{device.Name}') failed");
            }

            if (_initialLogging)
                Log.Information($"Caps {device.Name}: {string.Join("; ", list.Select(s => s.ToString()))}");

            return (list.ToArray(), state);
        }

        public static LocalVideoSourceCapabilityFormat GetMediaTypeInfo(AMMediaType mediaType, out int height, out int width, out int compression, out VideoInfoHeader v, out VideoInfoHeader2 v2)
        {
            compression = -1;
            v = null;
            v2 = null;
            if (mediaType.formatType == FormatType.VideoInfo)
            {
                v = new VideoInfoHeader();
                Marshal.PtrToStructure(mediaType.formatPtr, v);
                height = v.BmiHeader.Height;
                width = v.BmiHeader.Width;
                compression = v.BmiHeader.Compression;
            }
            else if (mediaType.formatType == FormatType.VideoInfo2)
            {
                v2 = new VideoInfoHeader2();
                Marshal.PtrToStructure(mediaType.formatPtr, v2);

                height = v2.BmiHeader.Height;
                width = v2.BmiHeader.Width;
                compression = v2.BmiHeader.Compression;
            }
            else
                throw new InvalidOperationException($"Invalid media type FormatType={mediaType}");

            switch (compression)
            {
                case 0x47504A4D: return LocalVideoSourceCapabilityFormat.MJpeg; 
                case 0x32595559: return LocalVideoSourceCapabilityFormat.Raw;
                case 0x34363248: return LocalVideoSourceCapabilityFormat.H264;
                case 0x3231564e: return LocalVideoSourceCapabilityFormat.NV12;
                case 0x30323449: return LocalVideoSourceCapabilityFormat.I420;
                case 0x0: return LocalVideoSourceCapabilityFormat.Empty;
                default:
                    Log.Warning($"Unknown video source format/compression {compression}");
                    return LocalVideoSourceCapabilityFormat.Unknown; 
            }
        }

        public static void Checked(Func<int> action, string name, DsDevice device)
        {
            int hr = action();
            if (hr < 0)
            {
                var error = DsError.GetErrorText(hr);
                throw new InvalidOperationException($"{name} failed. {error} ({hr})");
            }
        }

        public static double GetFps(long interval)
        {
            if (interval == 0) return 30;
            return Math.Round(10000000d / (double)interval, 2); 
        }
    }
}
