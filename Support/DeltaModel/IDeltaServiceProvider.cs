using System;
using System.Collections.Generic;
using System.Text;

namespace Clutch.DeltaModel
{
    public interface IDeltaServiceProvider
    {
        T Get<T>();
    }
}
