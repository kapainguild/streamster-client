using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientCore.Cross
{
    public interface IScreenRenderer
    {
        void ShowFrame(int width, int height, int version, byte[] buffer);
    }
}
