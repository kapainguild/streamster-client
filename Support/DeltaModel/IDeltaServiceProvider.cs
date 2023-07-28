using System;
using System.Collections.Generic;
using System.Text;

namespace DeltaModel
{
    public interface IDeltaServiceProvider
    {
        T Get<T>();
    }
}
