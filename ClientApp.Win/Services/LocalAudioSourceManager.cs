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
    public class LocalAudioSourceManager : ILocalAudioSourceManager
    {
        public async Task<LocalAudioSource[]> GetAudioSourcesAsync()
        {
            return await Task.Run(() => GetSources());
        }

        private LocalAudioSource[] GetSources()
        {
            var devices = DsDevice.GetDevicesOfCat(FilterCategory.AudioInputDevice);
            var videoDeviceNames = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice).Select(s => s.Name).Where(s => s != null).ToList();
            return devices.Select(s => GetAudioSource(s, videoDeviceNames.Contains(s.Name))).ToArray(); // TODO: put true
        }

        private LocalAudioSource GetAudioSource(DsDevice s, bool getCaps)
        {
            var caps = getCaps ? GetCapabilities(s) : (null, true);

            var state = InputDeviceState.Ready;
            if (ObsHelper.IsObsAudioVideoAndItIsOff(s.Name))
                state = InputDeviceState.NotStarted;
            else if (!caps.success)
                state = InputDeviceState.Failed;

            return new LocalAudioSource
            {
                Name = s.Name,
                Id = s.DevicePath,
                Type = s.DevicePath.ToLowerInvariant().Contains(":sw:") ? InputDeviceType.Virtual : InputDeviceType.USB,
                State = state,
                Capabilities = caps.caps
            };
        }

        private (LocalAudioSourceCapability[] caps, bool success) GetCapabilities(DsDevice device)
        {
            Log.Information($"Audio ({device.Name}): Getting Caps");
            var list = new List<LocalAudioSourceCapability>();

            bool failed = false;

            IntPtr pCaps = IntPtr.Zero;

            IFilterGraph2 filterGraph2 = null;
            IBaseFilter sourceFilter = null;
            IAMStreamConfig streamConfig = null;
            object pin = null;
            int count = 0;
            int size = 0;

            try
            {
                filterGraph2 = new FilterGraph() as IFilterGraph2;
                if (filterGraph2 == null)
                    throw new NotSupportedException("filter2 is null");

                LocalVideoSourceManager.AddCaptureFilter(filterGraph2, device, out sourceFilter);

                pin = DsFindPin.ByCategory(sourceFilter, PinCategory.Capture, 0);

                if (pin == null)
                {
                    Log.Information($"Audio ({device.Name}): First pin is null");
                    pin = sourceFilter;
                }

                streamConfig = pin as IAMStreamConfig;
                if (streamConfig == null)
                    throw new NotSupportedException("pin is null");

                LocalVideoSourceManager.Checked(() => streamConfig.GetNumberOfCapabilities(out count, out size), "GetNumberOfCapabilities", null);
                if (count <= 0)
                    throw new NotSupportedException("This video source does not report capabilities.");
                if (size != Marshal.SizeOf(typeof(AudioStreamConfigCaps)))
                    throw new NotSupportedException("Unable to retrieve video source capabilities. This video source requires a larger VideoStreamConfigCaps structure.");

                // Alloc memory for structure
                pCaps = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(AudioStreamConfigCaps)));

                for (int i = 0; i < count; i++)
                {
                    AMMediaType mediaType = null;
                    LocalVideoSourceManager.Checked(() => streamConfig.GetStreamCaps(i, out mediaType, pCaps), "GetStreamCaps", null);

                    AudioStreamConfigCaps caps = (AudioStreamConfigCaps)Marshal.PtrToStructure(pCaps, typeof(AudioStreamConfigCaps));

                    var result = new LocalAudioSourceCapability()
                    {
                        MinimumChannels = caps.MinimumChannels,
                        MaximumChannels = caps.MaximumChannels,
                        MinimumSampleFrequency = caps.MinimumSampleFrequency,
                        MaximumSampleFrequency = caps.MaximumSampleFrequency
                    };

                    list.Add(result);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"Error during retreiving caps for '{device.Name}'");
                failed = true;
            }
            finally
            {
                if (pCaps != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pCaps);
            }

            Log.Information($"Audio ({device.Name}): Releasing");

            try
            {
                LocalVideoSourceManager.ReleaseComObject(sourceFilter);
                LocalVideoSourceManager.ReleaseComObject(filterGraph2);
                LocalVideoSourceManager.ReleaseComObject(streamConfig);
                LocalVideoSourceManager.ReleaseComObject(pin);
            }
            catch (Exception e)
            {
                Log.Error(e, $"ReleaseComObject({device.Name}) failed");
            }

            Log.Information($"Caps {device.Name}: Count: {list.Count}/{count}, Str={size} ({string.Join("; ", list.Where(s => !s.IsStandart()).Select(s => s.ToString()))})");

            return (list.ToArray(), !failed);
        }
    }
}
