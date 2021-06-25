using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientCore.Cross
{
    public interface IImageHelper
    {
        (int width, int height) GetSize(byte[] data);
    }
}
