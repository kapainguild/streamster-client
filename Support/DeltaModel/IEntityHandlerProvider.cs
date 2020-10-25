using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Clutch.DeltaModel
{
    public interface IEntityHandlerProvider
    {
        IEntityHandler GetHandler();
    }
}
