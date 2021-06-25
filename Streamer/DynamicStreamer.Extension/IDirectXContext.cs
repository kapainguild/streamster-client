using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.Extension
{
    public interface IDirectXContext
    {
        Device Device { get; }

        object CreateCopy(Texture2D texture);
    }
}
