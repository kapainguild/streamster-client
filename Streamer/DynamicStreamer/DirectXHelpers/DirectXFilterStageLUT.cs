using DynamicStreamer.Helpers;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DynamicStreamer.DirectXHelpers
{
    public class DirectXFilterStageLUT : DirectXFilterStage<LutFilterConstantBuffer>
    {
        private SharpDX.Direct3D11.Resource _lutTexture1;
        private SharpDX.Direct3D11.Resource _lutTexture3;

        public DirectXFilterStageLUT(DirectXContext dx, SharpDX.Direct3D11.Resource texture1, SharpDX.Direct3D11.Resource texture3, double amount, int size, DirectXPipelineConfig dxConfig, Vector3 domainMin, Vector3 domainMax) :
            base(dx, dxConfig)
        {
            _lutTexture1 = texture1;
            _lutTexture3 = texture3;
            float sz = size - 1.0f;
            var cb = new LutFilterConstantBuffer
            {
                ViewProj = Matrix.Identity,
                clut_amount = (float)amount,
                clut_scale = new Vector3(sz, sz, sz), //64-1
                clut_offset = new Vector3(0, 0, 0),
                cube_width_i = 1.0f / size,
                domain_min = domainMin,
                domain_max = domainMax,
            };
            Pipeline.SetConstantBuffer(cb);
            Pipeline.SetDebugColor(1, 0.5f, 0, 1);
        }

        public static IDirectXFilterStage Create(DirectXContext dx, string resource, SingleFrameType type = SingleFrameType.Png, double amount = 1.0)
        {
            var content = DirectXHelper.ReadResourceAsBytes(resource);
            return Create(dx, content, type, amount);
        }

        public static IDirectXFilterStage Create(DirectXContext dx, byte[] buffer, SingleFrameType type, double amount)
        {
            if (type == SingleFrameType.Cube)
            {
                var cube = CubeParser.Read(buffer);
                if (cube != null)
                {
                    var texture1 = cube.Is3D ? null : CreateTexture1D(dx, cube);
                    var texture3 = cube.Is3D ? CreateTexture3D(dx, cube) : null;
                    return new DirectXFilterStageLUT(dx, texture1, texture3, amount, cube.Size, CreateConfig(cube.Is3D), cube.DomainMin, cube.DomainMax);
                }
            }
            else if (type == SingleFrameType.Png)
            {
                var texture = CreateTexture3D(dx, buffer);
                return new DirectXFilterStageLUT(dx, null, texture, amount, 64, CreateConfig(true), new Vector3(0, 0, 0), new Vector3(1, 1, 1));
            }
            return null;
        }

        private static unsafe SharpDX.Direct3D11.Resource CreateTexture1D(DirectXContext dx, CubeData cube)
        {
            var buf = cube.Data.Select(s => new Half(s).RawValue).ToArray();

            fixed (ushort* ptr = buf)
            {
                IntPtr intptr = (IntPtr)ptr;

                var cubeWidth = cube.Size;
                var rowSizeBits = cubeWidth * 64; // 64 bits
                var sliceSizeBytes = 1 * rowSizeBits / 8;
                var newRowSize = rowSizeBits / 8;
                var newSlizeSize = sliceSizeBytes;
                var data = new DataBox(intptr, newRowSize, 0);

                return new Texture2D(dx.Device, new Texture2DDescription()
                {
                    Width = cube.Size,
                    Height = 1,
                    BindFlags = BindFlags.ShaderResource,
                    Usage = ResourceUsage.Default,
                    CpuAccessFlags = CpuAccessFlags.None,
                    Format = Format.R16G16B16A16_Float,
                    MipLevels = 1,
                    OptionFlags = ResourceOptionFlags.None,
                    SampleDescription = new SampleDescription(1, 0),
                    ArraySize = 1,

                }, new[] { data });
            }
        }

        private static unsafe SharpDX.Direct3D11.Resource CreateTexture3D(DirectXContext dx, CubeData cube)
        {
            var buf = cube.Data.Select(s => new Half(s).RawValue).ToArray();

            fixed (ushort* ptr = buf)
            {
                IntPtr intptr = (IntPtr)ptr;

                var cubeWidth = cube.Size;
                var rowSizeBits = cubeWidth * 64; // 64 bits
                var sliceSizeBytes = cubeWidth * rowSizeBits / 8;
                var newRowSize = rowSizeBits / 8;
                var newSlizeSize = sliceSizeBytes;
                var data = new DataBox(intptr, newRowSize, newSlizeSize);

                return new Texture3D(dx.Device, new Texture3DDescription()
                {
                    Width = cube.Size,
                    Height = cube.Size,
                    Depth = cube.Size,
                    BindFlags = BindFlags.ShaderResource,
                    Usage = ResourceUsage.Default,
                    CpuAccessFlags = CpuAccessFlags.None,
                    Format = SharpDX.DXGI.Format.R16G16B16A16_Float,
                    MipLevels = 1,
                    OptionFlags = ResourceOptionFlags.None,

                }, new[] { data });
            }

        }

        private static DirectXPipelineConfig CreateConfig(bool is3d)
        {
            return new DirectXPipelineConfig
            {
                PixelShaderFile = "color_grade_filter.hlsl",
                VertexShaderFile = "color_grade_filter.hlsl",
                PixelShaderFunction = is3d ? "LUT3D" : "LUT1D",
                VertexShaderFunction = "VSDefault",
                Blend = true
            };
        }

        protected override void Render(DeviceContext ctx, RenderTargetView rtv, ShaderResourceView srv, int width, int height)
        {
            using var lutSrv1 = _lutTexture1 != null ? new ShaderResourceView(Dx.Device, _lutTexture1) : null;
            using var lutSrv3 = _lutTexture3 != null ? new ShaderResourceView(Dx.Device, _lutTexture3) : null;
            Pipeline.Render(ctx, rtv, srv, lutSrv3, lutSrv1);
        }

        public override void Dispose()
        {
            base.Dispose();
            _lutTexture1?.Dispose();
            _lutTexture3?.Dispose();
        }


        private static unsafe Texture3D CreateTexture3D(DirectXContext dx, byte[] file)
        {
            using MemoryStream stream = new MemoryStream(file);
            using var bitmapDecoder = new SharpDX.WIC.BitmapDecoder(
                dx.ImagingFactory2,
                stream,
                SharpDX.WIC.DecodeOptions.CacheOnLoad);

            using var bitmapSource = new SharpDX.WIC.FormatConverter(dx.ImagingFactory2);

            bitmapSource.Initialize(
                bitmapDecoder.GetFrame(0),
                SharpDX.WIC.PixelFormat.Format32bppPRGBA,
                SharpDX.WIC.BitmapDitherType.None,
                null,
                0.0,
                SharpDX.WIC.BitmapPaletteType.Custom);

            int width = bitmapSource.Size.Width;
            int stride = width * 4;
            int height = bitmapSource.Size.Height;

            var source = new byte[stride * height];
            bitmapSource.CopyPixels(source, stride);

            var target = new byte[stride * height];

            int cubeWidth = 64;

            var macro_width = width / cubeWidth;
            var macro_height = height / cubeWidth;

            IntPtr ptr;


            fixed (byte* bbuffer = source)
            fixed (byte* bcursor = target)
            {
                int* buffer = (int*)bbuffer;
                int* cursor = (int*)bcursor;

                for (int z = 0; z < cubeWidth; ++z)
                {
                    int z_x = (z % macro_width) * cubeWidth;
                    int z_y = (z / macro_height) * cubeWidth;
                    for (int y = 0; y < cubeWidth; ++y)
                    {
                        int row_index = width * (z_y + y);
                        for (int x = 0; x < cubeWidth; ++x)
                        {
                            int index = row_index + z_x + x;

                            *cursor = *(buffer + index);
                            cursor += 1;
                        }
                    }
                }

                ptr = (IntPtr)bcursor;
            }

            var rowSizeBits = cubeWidth * 32;
            var sliceSizeBytes = cubeWidth * rowSizeBits / 8;
            var newRowSize = rowSizeBits / 8;
            var newSlizeSize = sliceSizeBytes;

            var data = new DataBox(ptr, newRowSize, newSlizeSize);

            return new Texture3D(dx.Device, new Texture3DDescription()
            {
                Width = cubeWidth,
                Height = cubeWidth,
                Depth = cubeWidth,
                BindFlags = BindFlags.ShaderResource,
                Usage = ResourceUsage.Default,
                CpuAccessFlags = CpuAccessFlags.None,
                Format = SharpDX.DXGI.Format.R8G8B8A8_UNorm,
                MipLevels = 1,
                OptionFlags = ResourceOptionFlags.None,

            }, new[] { data });
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 128)]
    public struct LutFilterConstantBuffer
    {
        public Matrix ViewProj;
        public float clut_amount;
        public Vector3 clut_scale;
        public Vector3 clut_offset;
        public float gap1;
        public Vector3 domain_min;
        public float gap2;
        public Vector3 domain_max;
        public float cube_width_i;
    }
}
