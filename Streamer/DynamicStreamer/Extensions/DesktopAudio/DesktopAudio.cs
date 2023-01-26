using DynamicStreamer.Contexts;
using Serilog;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace DynamicStreamer.Extensions.DesktopAudio
{
    public class DesktopAudio : IDisposable
    {
        private static Guid IAudioClientId = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
        private static Guid IAudioRenderClientId = new Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");
        private static Guid IAudioCaptureClientId = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");

        private volatile bool _continueProcessing = true;
        private volatile bool _continueSilenceThread = true;

        private Thread _silenceThread;

        private ManualResetEvent _stopReading = new ManualResetEvent(false);
        private ManualResetEvent _stopSilenceThread = new ManualResetEvent(false);
        private AutoResetEvent _dataAvailable = new AutoResetEvent(false);

        private IAudioClient _audioClient;
        private IAudioClient _audioClientForRendering;
        private IMMDevice _endpoint;
        private IAudioRenderClient _audioRenderClient;
        private IAudioCaptureClient _audioCaptureClient;
        private int _bytesPerFrame;
        private InputTimeAdjuster _timeAdjuster = new InputTimeAdjuster();

        public WaveFormatEx Open()
        {
            StopSilenceThread();

            Log.Information("Opening DesktopAudio");
            IMMDeviceEnumerator deviceEnumerator = null;
            IntPtr mixFormatPtr = IntPtr.Zero;
            try
            {
                bool render = true;
                deviceEnumerator = Activator.CreateInstance(typeof(MMDeviceEnumerator)) as IMMDeviceEnumerator;
                var res = deviceEnumerator.GetDefaultAudioEndpoint(
                    render ? DataFlowEnum.Render : DataFlowEnum.Capture, 
                    render ? RoleEnum.Console : RoleEnum.Communications, 
                    out _endpoint);

                if (render)
                    StartSilenceGeneration();

                Checked(_endpoint.Activate(ref IAudioClientId, ClsCtxEnum.All, IntPtr.Zero, out var obj), "Activate");
                _audioClient = (IAudioClient)obj;

                Checked(_audioClient.GetMixFormat(out mixFormatPtr), "GetMixFormat");
                WaveFormatEx outputFormat = (WaveFormatEx)Marshal.PtrToStructure(mixFormatPtr, typeof(WaveFormatEx));

                if (!render) // for render it is checked in the StartSilenceGeneration();
                    CheckFormat(outputFormat);

                _bytesPerFrame = outputFormat.BlockAlign;

                var flags = AudioClientStreamFlagsEnum.StreamFlagsEventCallback | (render ? AudioClientStreamFlagsEnum.StreamFlagsLoopback : AudioClientStreamFlagsEnum.None);
                Checked(_audioClient.Initialize(AudioClientShareModeEnum.Shared, flags, 10_000_000 * 5, 0, mixFormatPtr, Guid.Empty), "Initialize");

                Checked(_audioClient.GetService(IAudioCaptureClientId, out var captureObj), "GetService");
                _audioCaptureClient = (IAudioCaptureClient)captureObj;

#pragma warning disable CS0618 // Type or member is obsolete
                Checked(_audioClient.SetEventHandle(_dataAvailable.Handle), "SetEventHandle");
#pragma warning restore CS0618 // Type or member is obsolete
                Checked(_audioClient.Start(), "Start");

                return outputFormat;
            }
            catch (Exception e)
            {
                Core.LogError(e, "Open desktop audio failed");
                StopSilenceThread();

                ReleaseComFields();
                throw;
            }
            finally
            {
                if (mixFormatPtr != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(mixFormatPtr);

                ReleaseComObject(ref deviceEnumerator);
            }
        }

        private void StopSilenceThread()
        {
            if (_silenceThread != null)
            {
                _continueSilenceThread = false;
                _stopSilenceThread.Set();

                if (!_silenceThread.Join(4000))
                    Log.Error("Failed to join silence thread");

                _continueSilenceThread = true;
                _stopSilenceThread.Reset();
                _silenceThread = null;
                Log.Information("Silence thread stopped");
            }
        }

        public void Dispose()
        {
            Interrupt();
            StopSilenceThread();

            ReleaseComFields();

            _stopReading.Dispose();
            _dataAvailable.Dispose();
            _stopSilenceThread.Dispose();
        }

        public void Interrupt()
        {
            _continueProcessing = false;
            _stopReading.Set();
        }

        public void Read(Packet packet)
        {
            int captureSize = 0;
            while (_continueProcessing && captureSize == 0)
            {
                Checked(_audioCaptureClient.GetNextPacketSize(out captureSize), "GetNextPacketSize");

                if (captureSize == 0)
                    WaitHandle.WaitAny(new WaitHandle[] { _stopReading, _dataAvailable });
            }

            if (captureSize > 0)
            {
                _audioCaptureClient.GetBuffer(out var buffer, out var frames, out var flags, out var pos, out var s);

                //TODO: investigate which is better
                var time = _timeAdjuster.Add(s, Core.GetCurrentTime());
                packet.InitFromBuffer(buffer, frames * _bytesPerFrame, time);
                //Core.LogInfo($"DA: {frames * _bytesPerFrame} {s}");
                _audioCaptureClient.ReleaseBuffer(frames);
            }
            else throw new OperationCanceledException();
        }

        private void StartSilenceGeneration()
        {
            IntPtr mixFormatPtr = IntPtr.Zero;
            try
            {
                Checked(_endpoint.Activate(ref IAudioClientId, ClsCtxEnum.All, IntPtr.Zero, out var obj), "Silence.Activate");
                _audioClientForRendering = (IAudioClient)obj;

                Checked(_audioClientForRendering.GetMixFormat(out mixFormatPtr), "Silence.GetMixFormat");
                WaveFormatEx format = (WaveFormatEx)Marshal.PtrToStructure(mixFormatPtr, typeof(WaveFormatEx));

                CheckFormat(format);

                Checked(_audioClientForRendering.Initialize(AudioClientShareModeEnum.Shared, AudioClientStreamFlagsEnum.None, 10_000_000 * 5, 0, mixFormatPtr, Guid.Empty), "Silence.Initialize");
                Checked(_audioClientForRendering.GetBufferSize(out var bufferSize), "Silence.GetBufferSize");
                Checked(_audioClientForRendering.GetService(IAudioRenderClientId, out var renderObj), "Silence.GetService");

                _audioRenderClient = (IAudioRenderClient)renderObj;

                Checked(_audioClientForRendering.Start(), "Silence.Start");

                _silenceThread = new Thread(() => SilenceGenerationRoutine(bufferSize, format));
                _silenceThread.Name = "Silence generator";
                _silenceThread.Start();
            }
            catch (Exception e)
            {
                ReleaseComObject(ref _audioClientForRendering);
                ReleaseComObject(ref _audioRenderClient);
                Core.LogError(e, "Faied to StartSilenceGeneration");
            }
            finally
            {
                if (mixFormatPtr != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(mixFormatPtr);
            }
        }

        private void CheckFormat(WaveFormatEx format)
        {
            if (format.WaveFormat != 65534 ||
                format.SubFormat != new Guid("00000003-0000-0010-8000-00aa00389b71"))
            {
                if (format.WaveFormat != 3)
                    throw new InvalidOperationException($"Unsupported DesktopAudio Format {format.WaveFormat} - {format.SubFormat}");
            }
        }


        private void SilenceGenerationRoutine(int bufferSize, WaveFormatEx format)
        {
            try
            {
                var buffer = new float[bufferSize * format.Channels];
                var bufferDurationMs = bufferSize * 1000 / format.SampleRate;

                while (_continueSilenceThread)
                {
                    _audioClientForRendering.GetCurrentPadding(out var padding);
                    int numFramesAvailable = bufferSize - padding;

                    _audioRenderClient.GetBuffer(numFramesAvailable, out var nativeBuffer);
                    if (nativeBuffer != IntPtr.Zero)
                    {
                        Marshal.Copy(buffer, 0, nativeBuffer, numFramesAvailable * format.Channels);
                        _audioRenderClient.ReleaseBuffer(numFramesAvailable, AudioClientBufferFlags.None);
                    }

                    _stopReading.WaitOne(bufferDurationMs / 2);
                }
            }
            catch (Exception e)
            {
                Log.Warning(e, "Error in Silence generator. Exiting");
            }
        }

        private void ReleaseComFields()
        {
            ReleaseComObject(ref _audioClientForRendering);
            ReleaseComObject(ref _audioRenderClient);
            ReleaseComObject(ref _audioCaptureClient);
            ReleaseComObject(ref _audioClient);
            ReleaseComObject(ref _endpoint);
        }


        public static void ReleaseComObject<T>(ref T obj) where T : class
        {
            if (obj != null)
            {
                Marshal.ReleaseComObject(obj);
                obj = null;
            }
        }

        public static void Checked(int hr, string name)
        {
            if (hr < 0)
                throw new InvalidOperationException($"{name} failed with ({hr})");
        }
    }
}
