using SharpDX;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.DirectXHelpers
{
    public class ColorMatrix
    {
        public float[] Values { get; set; }

        public ColorMatrix(params float[] values)
        {
            Values = values;
        }
    }

    public class ColorMatrices
    {
        public static ColorMatrix Full709 = new ColorMatrix(1.000000f, 0.000000f, 1.581000f, -0.793600f, 1.000000f, -0.188062f,
                                                   -0.469967f, 0.330305f, 1.000000f, 1.862906f, 0.000000f, -0.935106f,
                                                   0.000000f, 0.000000f, 0.000000f, 1.000000f);

        public static ColorMatrix NotFull709 = new ColorMatrix(1.164384f, 0.000000f, 1.792741f, -0.972945f, 1.164384f, -0.213249f,
                                                   -0.532909f, 0.301483f, 1.164384f, 2.112402f, 0.000000f, -1.133402f,
                                                   0.000000f, 0.000000f, 0.000000f, 1.000000f);

        public static ColorMatrix Full601 = new ColorMatrix(1.000000f, 0.000000f, 1.407520f, -0.706520f, 
                                                            1.000000f, -0.345491f, -0.716948f, 0.533303f, 
                                                            1.000000f, 1.778976f, 0.000000f, -0.892976f,
                                                            0.000000f, 0.000000f, 0.000000f, 1.000000f);

        public static ColorMatrix NotFull601 = new ColorMatrix(1.164384f, 0.000000f, 1.596027f, -0.874202f, 1.164384f, -0.391762f,
                                                   -0.812968f, 0.531668f, 1.164384f, 2.017232f, 0.000000f, -1.085631f,
                                                   0.000000f, 0.000000f, 0.000000f, 1.000000f);

        public static ColorMatrix GetInverted(ColorMatrix source)
        {
            var s = new Matrix(source.Values);
            s.Invert();
            return new ColorMatrix(s.ToArray());
        }
    }
}
