using SharpDX;
using System.Runtime.InteropServices;

namespace DynamicStreamer.DirectXHelpers
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ConverterFilterConstantBuffer
    {
        public float width;
        public float height;
        public float width_i;
        public float width_d2;
        public float height_d2;
        public float width_x2_i;

        public float dummy1;
        public float dummy2;

        public Vector4 color_vec0;
        public Vector4 color_vec1;
        public Vector4 color_vec2;
        public Vector3 color_range_min;
        public float dummy3;
        public Vector3 color_range_max;
        public float dummy4;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 96)]
    public struct BlendingConstantBuffer
    {
        public Matrix ViewProj;
        public Vector2 base_dimension;
        public Vector2 base_dimension_i;
        public float undistort_factor;
    }
}
