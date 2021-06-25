using DynamicStreamer.DirectXHelpers;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Streamster.ClientCore.Models
{
    public class StaticResources
    {
        private static byte[] _background;
        private static byte[] _badSource;
        private static byte[] _noSignal;

        public static byte[] Background => Get(ref _background, "Background.png");

        public static byte[] BadSource => Get(ref _badSource, "BadSource.png");

        public static byte[] NoSignal => Get(ref _noSignal, "NoSignal.png");

        private static byte[] Get(ref byte[] field, string name)
        {
            if (field == null)
                field = ReadResourceAsBytes(name);
            return field;
        }

        private static byte[] ReadResourceAsBytes(string name) => DirectXHelper.ReadResourceAsBytes(name, "Resources", "Streamster.ClientCore", Assembly.GetExecutingAssembly());
    }
}
