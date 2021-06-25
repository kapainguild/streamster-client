using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.Extension
{
    public interface IMainThreadExecutor
    {
        void Execute(Action action, bool sync);
    }
}
