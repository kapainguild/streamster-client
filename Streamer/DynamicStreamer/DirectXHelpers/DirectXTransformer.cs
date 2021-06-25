using DynamicStreamer;
using DynamicStreamer.DirectXHelpers;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynamicStreamer.DirectXHelpers
{
    class DirectXTransformer
    {
        private readonly DirectXContext _dx;
        private readonly int _width;
        private readonly int _height;
        private readonly int _pixelFormat;
        private static Dictionary<int, DirectXDownloaderFormatDescriptor> s_descriptors;

        private List<DirectXTransformerPlaneData> _planes;


        static DirectXTransformer()
        {
            s_descriptors = new Dictionary<int, DirectXDownloaderFormatDescriptor>
            {
                [Core.Const.PIX_FMT_NV12] = new DirectXDownloaderFormatDescriptor(new[] { new DirectXDownloaderFormatPlane("VSPos", "PS_Y", 1, 1, Format.R8_UNorm),
                                                                                          new DirectXDownloaderFormatPlane("VSTexPos_Left", "PS_UV_Wide", 2, 2, Format.R8G8_UNorm)}),


            };
        }

        private DirectXTransformer(DirectXContext dx, List<DirectXTransformerPlaneData> planes, int width, int height, int pixelFormat)
        {
            _dx = dx.AddRef();
            _planes = planes;
            _width = width;
            _height = height;
            _pixelFormat = pixelFormat;
        }

        public static bool IsFormatSupportedForTranslateTo(int pixelFormat)
        {
            return s_descriptors.TryGetValue(pixelFormat, out var desc);
        }

        public static DirectXTransformer Create(DirectXContext dx, int pixelFormat, int width, int height)
        {
            var desc = s_descriptors[pixelFormat];
            var pipelines = desc.Planes.Select(p => CreatePlane(dx, width, height, p)).ToList();
            return new DirectXTransformer(dx, pipelines, width, height, pixelFormat);
        }

        private static DirectXTransformerPlaneData CreatePlane(DirectXContext dx, int width, int height, DirectXDownloaderFormatPlane plane)
        {
            var config = new DirectXPipelineConfig
            {
                VertexShaderFile = "format_conversion.hlsl",
                VertexShaderFunction = plane.VS,
                PixelShaderFile = "format_conversion.hlsl",
                PixelShaderFunction = plane.PS,
            };

            var pipeline = new DirectXPipeline<ConverterFilterConstantBuffer>(config, dx);
            pipeline.SetDebugColor(0, 0, 0, 1);
            pipeline.SetPosition(DirectXPipelineConfig.FullRectangle, new Viewport(0, 0, width / plane.WidthFactor, height / plane.HeightFactor));
            var colorMatrix = ColorMatrices.GetInverted(ColorMatrices.Full709);
            var cm = colorMatrix.Values;
            var cb = new ConverterFilterConstantBuffer
            {
                width = width,
                height = height,
                width_i = 1.0f / width,
                width_d2 = width / 2,
                height_d2 = height / 2,
                width_x2_i = 0.5f / width,

                color_vec0 = new Vector4(cm[0], cm[1], cm[2], cm[3]),
                color_vec1 = new Vector4(cm[4], cm[5], cm[6], cm[7]),
                color_vec2 = new Vector4(cm[8], cm[9], cm[10], cm[11]),
                color_range_min = new Vector3(0.0f, 0.0f, 0.0f),
                color_range_max = new Vector3(1.0f, 1.0f, 1.0f),
            };

            pipeline.SetConstantBuffer(cb, false);

            return new DirectXTransformerPlaneData { Pipeline = pipeline , Format = plane.Format };
        }

        public void Dispose()
        {
            _planes.ForEach(s => s.Dispose());
            _dx.RemoveRef();
        }

        public DirectXResource Process(Frame input)
        {
            var texOut = _dx.Pool.Get("transfer out", DirectXResource.Desc(_width, _height, Format.NV12,
                BindFlags.RenderTarget, ResourceUsage.Default, ResourceOptionFlags.Shared, CpuAccessFlags.None));

            _dx.RunOnContext(ctx =>
            {
                for (int q = 0; q < _planes.Count; q++)
                {
                    Render(input.DirectXResourceRef.Instance, texOut, _planes[q], ctx);
                }
            }, "Transfer Render");

            return texOut;
        }

        private void Render(DirectXResource input, DirectXResource output, DirectXTransformerPlaneData plane, DeviceContext ctx)
        {
            using (var srv = input.GetShaderResourceView())
            using (var rtv = new RenderTargetView(ctx.Device, output.Texture2D, new RenderTargetViewDescription { Format = plane.Format, Dimension = RenderTargetViewDimension.Texture2D }))
            {
                plane.Pipeline.Render(ctx, rtv, srv);
            }
        }
    }

    public class DirectXTransformerPlaneData
    {
        public DirectXPipeline<ConverterFilterConstantBuffer> Pipeline { get; set; }
        public Format Format { get; internal set; }

        internal void Dispose()
        {
            Pipeline.Dispose();
        }
    }
}
