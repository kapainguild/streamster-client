using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicStreamerCef
{
    public interface IRenderTarget
    {
        Size Size { get; }

        void OnPaint(bool main, IntPtr buffer, int width, int height);

        void OnPopupShow(bool show);

        void OnPopupSize(int left, int top, int width, int height);
    }
}
