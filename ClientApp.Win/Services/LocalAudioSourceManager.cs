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
    class LocalAudioSourceManager : ILocalAudioSourceManager
    {
        private List<AudioSource> _sources = new List<AudioSource>();
        private Action<IAudioSource> _sourceChanged;
        private Task<IAudioSource[]> _retrieveSourcesListTask;
        private DateTime _lastUpdated = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private string _runningSourceId;

        public LocalAudioSourceManager()
        {
            _sourceChanged = (d) => { };
        }

        public void SetRunningSource(string localAudioId)
        {
            var changed = new List<AudioSource>();
            lock (this)
            {
                if(_runningSourceId != localAudioId)
                {
                    var o = _sources.FirstOrDefault(s => s.Id == _runningSourceId);
                    if (o != null)
                    {
                        changed.Add(o);
                        o.State = InputState.Unknown;
                    }

                    var n = _sources.FirstOrDefault(s => s.Id == localAudioId);
                    if (n != null)
                    {
                        changed.Add(n);
                        n.State = InputState.Running;
                    }

                    _runningSourceId = localAudioId;
                }
            }

            changed.ForEach(s => _sourceChanged(s));
        }


        public void Start(Action<IAudioSource> audioDeviceChanged)
        {
            _sourceChanged = audioDeviceChanged;
            List<AudioSource> snapshotAudio;
            lock (this)
            {
                snapshotAudio = _sources.ToList();
            }
            snapshotAudio.ForEach(s => _sourceChanged(s));
        }

        public async Task<IAudioSource[]> RetrieveSourcesListAsync()
        {
            Task<IAudioSource[]> current = null;
            var now = DateTime.UtcNow;
            lock (this)
            {
                if (now - _lastUpdated < TimeSpan.FromSeconds(5))
                    return _sources.OfType<IAudioSource>().ToArray();

                if (_retrieveSourcesListTask == null)
                    _retrieveSourcesListTask = Task.Run(() => RetrieveSourceList());
                current = _retrieveSourcesListTask;
            }
            try
            {
                return await current;
            }
            finally
            {
                lock (this)
                {
                    _lastUpdated = DateTime.UtcNow;
                    _retrieveSourcesListTask = null;
                }
            }
        }

        private IAudioSource[] RetrieveSourceList()
        {
            var devices = DsDevice.GetDevicesOfCat(FilterCategory.AudioInputDevice);
            var videoDeviceNames = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice).Select(s => s.Name).ToList();
            //var devices = new DsDevice[0];

            List<AudioSource> snapshot = null;
            List<AudioSource> snapshotToUpdate = null;

            lock (this)
            {
                var newDevices = devices.Where(s => !_sources.Any(r => r.Id == s.DevicePath)).Select(s => new AudioSource
                {
                    Name = s.Name,
                    Id = s.DevicePath,
                    DsDevice = s,
                    Type = s.DevicePath.ToLowerInvariant().Contains(":sw:") ? InputType.Virtual : InputType.USB 
                }).ToList();
                var existingDevices = _sources.Where(s => devices.Any(r => r.DevicePath == s.Id)).ToList();
                var removedDevices = _sources.Where(s => !devices.Any(r => r.DevicePath == s.Id)).ToList();

                _sources.AddRange(newDevices);
                removedDevices.ForEach(d => d.State = InputState.Removed);
                snapshotToUpdate = existingDevices.Concat(newDevices).ToList();
                snapshot = _sources.ToList();
            }
            snapshotToUpdate.ForEach(s => UpdateSourceState(s, videoDeviceNames));
            snapshot.ForEach(s => _sourceChanged(s));
            return snapshot.OfType<IAudioSource>().ToArray();
        }

        private void UpdateSourceState(AudioSource s, List<string> videoDeviceNames)
        {
            if (ObsHelper.IsObsAudioVideoAndItIsOff(s.Name))
                s.State = InputState.ObsIsNotStarted;
            else if (s.Id == _runningSourceId)
                s.State = InputState.Running;
            else
            {
                if (CanGetState(s, videoDeviceNames))
                    s.State = GetState(s);
                else
                    s.State = InputState.Ready;
            }
        }

        private bool CanGetState(AudioSource s, List<string> videoDeviceNames)
        {
            if (videoDeviceNames.Any(video => s.Name.Contains(video)) && !s.Name.ToLower().Contains("xsplit"))
            {
                Log.Information($"Audio ({s.Name}): can not get state");
                return false;
            }
            return true;
        }

        private InputState GetState(AudioSource device)
        {
            Log.Information($"Audio ({device.Name}): Getting Caps");
            var list = new List<AudioSourceCapability>();

            var state = InputState.Ready;

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

                LocalVideoSourceManager.AddCaptureFilter(filterGraph2, device.DsDevice, out sourceFilter);

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

                    var result = new AudioSourceCapability()
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

                state = InputState.Failed;
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

            return state;
        }
    }

    class AudioSource : IAudioSource
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public InputState State { get; set; }

        public InputType Type { get; set; }

        public DsDevice DsDevice { get; internal set; }

        public AudioSourceCapability[] InternalCapabilities { get; set; }
    }


    public class AudioSourceCapability
    {
        public int MinimumChannels { get; set; }
        public int MaximumChannels { get; set; }
        public int MinimumSampleFrequency { get; set; }
        public int MaximumSampleFrequency { get; set; }

        public override string ToString()
        {
            return $"{MinimumChannels}-{MaximumChannels}x{MinimumSampleFrequency}-{MaximumSampleFrequency}";
        }

        public bool IsStandart() => MinimumChannels == 1 &&
                                    MaximumChannels == 2 &&
                                    MinimumSampleFrequency == 11025 &&
                                    MaximumSampleFrequency == 44100;
    }
}
