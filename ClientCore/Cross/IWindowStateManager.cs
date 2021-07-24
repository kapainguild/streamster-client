
using System;

namespace Streamster.ClientCore.Cross
{
    public interface IWindowStateManager
    {
        void Start();

        IntPtr WindowHandle { get; }

        bool IsMinimized();

        event EventHandler IsMinimizedChanged;
    }
}
