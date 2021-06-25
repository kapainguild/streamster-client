using DynamicStreamer.Queues;
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
    public interface IScreenRenderer
    {
        void ShowFrame(FrameOutputData fromPool);
    }

    public interface IScreenRendererHost
    {
        void Register(IScreenRenderer renderer);
    }

    public class ScreenRendererModel : IScreenRendererHost
    {
        private IScreenRenderer _screenRenderer;

        public Property<bool> IsEnabled { get; } = new Property<bool>(true);

        public Action OnChanged { get; set; }

        public ScreenRendererModel()
        {
            IsEnabled.OnChange = (o, n) => OnIsEnabledChanged(n);
        }

        private void OnIsEnabledChanged(bool newValue)
        {
            OnChanged?.Invoke();
        }

        public void OnFrame(FrameOutputData frameData)
        {
            if (_screenRenderer != null)
            {
                _screenRenderer.ShowFrame(frameData);
            }
            else
                frameData.Frame.Dispose();
        }

        public void Register(IScreenRenderer renderer)
        {
            _screenRenderer = renderer;
        }
    }
}
