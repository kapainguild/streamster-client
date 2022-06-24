﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Harmonic.Networking.Amf.Serialization.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ClassFieldAttribute : Attribute
    {
        public string Name { get; set; } = null;
    }
}
