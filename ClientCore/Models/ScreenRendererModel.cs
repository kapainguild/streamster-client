using Serilog;
using Streamster.ClientCore.Cross;
using Streamster.ClientData;
using Streamster.ClientData.Model;
using Streamster.DynamicStreamerWrapper;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Streamster.ClientCore.Models
{
    public class ScreenRendererModel : IDisposable
    {
        public IScreenRenderer ScreenRenderer { get; }

        public int BufferVersion { get; set; }

        private byte[] _buffer;
        private int _bufferHeight;
        private int _bufferWidth;
        
        private DateTime _lastFrameTime = DateTime.MinValue;

        private byte[] _blackBuffer;

        private int _incomingSequenceNumber;

        private volatile int _byPass = 1;
        private Streamer _streamer;
        private bool _localStreamer;
        private int _removeStreamerId;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly CoreData _coreData;

        public ScreenRendererModel(IScreenRenderer screenRenderer, CoreData coreData)
        {
            ScreenRenderer = screenRenderer;
            _coreData = coreData;
            

            _coreData.Subscriptions.SubscribeForProperties<IDevice>(s => s.DisplayVideoHidden, (i, c, p) => ShowHidePreview());
        }

        public void Start()
        {
            TaskHelper.RunUnawaited(MainLoop(), "ScreenRendererModel.MainLoop");
        }

        private void ShowHidePreview()
        {
            ChangeVisibility(!_coreData.ThisDevice.DisplayVideoHidden);
        }

        public void SetStreamer(Streamer streamer, bool local)
        {
            ChangeVisibility(false);
            _streamer = streamer;
            _localStreamer = local;
            ChangeVisibility(!_coreData.ThisDevice.DisplayVideoHidden);
        }

        private void ChangeVisibility(bool visible)
        {
            try
            {
                if (_streamer != null)
                {
                    if (visible)
                    {
                        if (_localStreamer)
                            this._streamer.SetDirectFrameCallback(OnFrame);
                        else
                            _removeStreamerId = this._streamer.SetOutputCallback(OnFrame, 0);
                    }
                    else
                    {
                        if (_localStreamer)
                            this._streamer.SetDirectFrameCallback(null);
                        else
                            this._streamer.SetOutputCallback(null, _removeStreamerId);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Initialization error");
            }
        }

        private void OnFrame(int width, int height, int length, long data)
        {
            _incomingSequenceNumber++;

            if (_incomingSequenceNumber % _byPass != 0)
                return;

            lock (this)
            {
                _bufferHeight = height;
                _bufferWidth = width;
                if (_buffer == null || _buffer.Length != length)
                    _buffer = new byte[length];

                IntPtr ptr = new IntPtr(data);
                Marshal.Copy(ptr, _buffer, 0, length);
                BufferVersion++;
                _lastFrameTime = DateTime.UtcNow;
            }
            _coreData.RunOnMainThread(() => CommitToUI());
        }

        private async Task MainLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                int bypass = 1;

                if (_coreData.Settings.Fps > 30)
                    bypass++;

                if (_coreData.ThisDevice.KPIs.Encoder.QueueSize > 6)
                    bypass++;

                lock (this)
                {
                    if (_lastFrameTime != DateTime.MinValue && !_coreData.ThisDevice.DisplayVideoHidden)
                    {
                        if (DateTime.UtcNow - _lastFrameTime > TimeSpan.FromSeconds(2))
                        {
                            int size = 3 * _bufferHeight * _bufferWidth;
                            if (_blackBuffer == null || _blackBuffer.Length != size)
                                _blackBuffer = new byte[size];
                            ScreenRenderer.ShowFrame(_bufferWidth, _bufferHeight, -1, _blackBuffer);
                        }
                    }
                }
                await Task.Delay(2000, _cts.Token);
            }
        }

        private void CommitToUI()
        {
            lock (this)
            {
                ScreenRenderer.ShowFrame(_bufferWidth, _bufferHeight, BufferVersion, _buffer);
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
        }
    }
}
