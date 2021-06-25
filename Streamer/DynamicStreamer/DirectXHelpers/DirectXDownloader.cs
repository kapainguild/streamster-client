using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicStreamer.DirectXHelpers
{
    public record DirectXDownloaderFormatPlane(string VS, string PS, int WidthFactor, int HeightFactor, Format Format);

    public record DirectXDownloaderFormatDescriptor(DirectXDownloaderFormatPlane[] Planes);


    public class DirectXDownloaderPlaneData
    {
        public DirectXDownloaderFormatPlane Format { get; set; }

        public DirectXPipeline<ConverterFilterConstantBuffer> Pipeline { get; set; }

        public DirectXResource GpuTexture { get; set; }

        public DirectXResource CpuTexture { get; set; }

        internal void Dispose(DirectXContext dx)
        {
            Pipeline.Dispose();
            dx.Pool.Back(GpuTexture);
            dx.Pool.Back(CpuTexture);
        }
    }

    class DirectXDownloader : IDisposable
    {
        private readonly DirectXContext _dx;
        private readonly int _width;
        private readonly int _height;
        private readonly int _pixelFormat;
        private static Dictionary<int, DirectXDownloaderFormatDescriptor> s_descriptors;

        private List<DirectXDownloaderPlaneData> _planes;


        static DirectXDownloader()
        {
            s_descriptors = new Dictionary<int, DirectXDownloaderFormatDescriptor>
            {
                [Core.Const.PIX_FMT_YUV420P] = new DirectXDownloaderFormatDescriptor(new[] {    new DirectXDownloaderFormatPlane("VSPos", "PS_Y", 1, 1, Format.R8_UNorm),
                                                                                                new DirectXDownloaderFormatPlane("VSTexPos_Left", "PS_U_Wide", 2, 2, Format.R8_UNorm),
                                                                                                new DirectXDownloaderFormatPlane("VSTexPos_Left", "PS_V_Wide", 2, 2, Format.R8_UNorm)}),

                [Core.Const.PIX_FMT_NV12] = new DirectXDownloaderFormatDescriptor(new[] {    new DirectXDownloaderFormatPlane("VSPos", "PS_Y", 1, 1, Format.R8_UNorm),
                                                                                                new DirectXDownloaderFormatPlane("VSTexPos_Left", "PS_UV_Wide", 2, 2, Format.R8G8_UNorm)}),


            };
        }

        private DirectXDownloader(DirectXContext dx, List<DirectXDownloaderPlaneData> planes, int width, int height, int pixelFormat)
        {
            _dx = dx.AddRef();
            _planes = planes;
            _width = width;
            _height = height;
            _pixelFormat = pixelFormat;
        }

        public static bool IsFormatSupportedForDecoderUpload(int pixelFormat)
        {
            // decoders now support only one plane
            return s_descriptors.TryGetValue(pixelFormat, out var desc) && desc.Planes.Length == 1;
        }

        public static bool IsFormatSupportedForFilterUpload(int pixelFormat)
        {
            return s_descriptors.ContainsKey(pixelFormat);
        }

        public static DirectXDownloader Create(DirectXContext dx, int pixelFormat, int width, int height)
        {
            var desc = s_descriptors[pixelFormat];

            var pipelines = desc.Planes.Select(p => CreatePlane(dx, width, height, p)).ToList();

            return new DirectXDownloader(dx, pipelines, width, height, pixelFormat);
        }

        private static DirectXDownloaderPlaneData CreatePlane(DirectXContext dx, int width, int height, DirectXDownloaderFormatPlane plane)
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

            return new DirectXDownloaderPlaneData
            {
                Format = plane,
                Pipeline = pipeline,
                GpuTexture = dx.Pool.Get("downloadGpu", DirectXResource.Desc(width / plane.WidthFactor, height / plane.HeightFactor, plane.Format)),
                CpuTexture = dx.Pool.Get("downloadCpu", DirectXResource.Desc(width / plane.WidthFactor, height / plane.HeightFactor, plane.Format, BindFlags.None, ResourceUsage.Staging, ResourceOptionFlags.None, CpuAccessFlags.Read))
            };
        }

        public void Dispose()
        {
            _planes.ForEach(s => s.Dispose(_dx));
            _dx.RemoveRef();
        }

        public void Download(Frame input, Frame output)
        {
            FramePlaneDesc[] dbs = null;

            //using var _ = new TimeMeasurer("download");

            _dx.RunOnContext(ctx => _planes.ForEach(p => Render(input.DirectXResourceRef.Instance, p, ctx)), "Download Render");
            _dx.RunOnContext(ctx => dbs = _planes.Select(p => Map(input.DirectXResourceRef.Instance, p, ctx)).ToArray(), "Download Begin");

            if (dbs != null)
                output.Init(_width, _height, _pixelFormat, input.Properties.Pts, dbs);

            _dx.RunOnContext(ctx => _planes.ForEach(p => Unmap(p, ctx)), "Download End");
        }

        private void Unmap(DirectXDownloaderPlaneData p, DeviceContext ctx)
        {
            ctx.UnmapSubresource(p.CpuTexture.Texture2D, 0);
        }

        private void Render(DirectXResource input, DirectXDownloaderPlaneData plane, DeviceContext ctx)
        {
            using (var srv = input.GetShaderResourceView())
            using (var rtv = plane.GpuTexture.GetRenderTargetView())
            {
                plane.Pipeline.Render(ctx, rtv, srv);
            }
        }

        private FramePlaneDesc Map(DirectXResource input, DirectXDownloaderPlaneData plane, DeviceContext ctx)
        {
            ctx.CopyResource(plane.GpuTexture.Texture2D, plane.CpuTexture.Texture2D);
            var db = ctx.MapSubresource(plane.CpuTexture.Texture2D, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
            return new FramePlaneDesc { Data = db.DataPointer, Stride = db.RowPitch, StrideCount = _height / plane.Format.HeightFactor };
        }

    }
}
