﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Harmonic.Networking.Amf.Common
{
    class TypeRegisterState
    {
        public Type Type { get; set; }
        public Dictionary<string, Action<object, object>> Members { get; set; }
    }
}
