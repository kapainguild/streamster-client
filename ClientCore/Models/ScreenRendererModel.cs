using DynamicStreamer.Queues;
using Streamster.ClientCore.Cross;
using System;

namespace Streamster.ClientCore.Models
{
    public interface IScreenRenderer
    {
        void ShowFrame(FrameOutputData fromPool, IWindowStateManager manager);
    }

    public interface IScreenRendererHost
    {
        void Register(IScreenRenderer renderer);
    }

    public class ScreenRendererModel : IScreenRendererHost
    {
        private IScreenRenderer _screenRenderer;
        private readonly IWindowStateManager _windowStateManager;

        public Property<bool> IsEnabled { get; } = new Property<bool>(true);

        public Action OnChanged { get; set; }

        public ScreenRendererModel(IWindowStateManager windowStateManager)
        {
            IsEnabled.OnChange = (o, n) => OnIsEnabledChanged(n);
            _windowStateManager = windowStateManager;
        }

        private void OnIsEnabledChanged(bool newValue)
        {
            OnChanged?.Invoke();
        }

        public void OnFrame(FrameOutputData frameData)
        {
            if (_screenRenderer != null)
            {
                _screenRenderer.ShowFrame(frameData, _windowStateManager);
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
