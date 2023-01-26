using Serilog;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.DirectXHelpers
{

    record DirectXUploaderFormatPlane(double PitchFactor, int WidthFactor, int HeightFactor, Format Format);

    record DirectXUploaderFormatDescriptor(string VetextShaderFunction, string PixelShaderFunction, DirectXUploaderFormatPlane[] Planes);

    public class DirectXUploader : IDisposable
    {
        private readonly DirectXContext _dx;
        private DirectXPipeline<ConverterFilterConstantBuffer> _pipeline;
        private readonly int _width;
        private readonly int _height;
        private readonly DirectXUploaderFormatDescriptor _descriptor;
        private static Dictionary<int, DirectXUploaderFormatDescriptor> s_descriptors;
        private DeviceContext _defferedContext;


        static DirectXUploader()
        {
            s_descriptors = new Dictionary<int, DirectXUploaderFormatDescriptor>
            {
                [Core.Const.PIX_FMT_YUYV422] = new DirectXUploaderFormatDescriptor("VSTexPosHalf_Reverse", "PSYUY2_Reverse", new[] { new DirectXUploaderFormatPlane(2, 2, 1, Format.B8G8R8A8_UNorm) }),
                [Core.Const.PIX_FMT_YUV422P] = new DirectXUploaderFormatDescriptor("VSPosWide_Reverse", "PSPlanar422_DS_Reverse", new[] { 
                                                                                                                                    new DirectXUploaderFormatPlane(1, 1, 1, Format.R8_UNorm),
                                                                                                                                    new DirectXUploaderFormatPlane(0.5, 2, 1, Format.R8_UNorm),
                                                                                                                                    new DirectXUploaderFormatPlane(0.5, 2, 1, Format.R8_UNorm)})
            };
        }

        private DirectXUploader(DirectXContext dx, DirectXPipeline<ConverterFilterConstantBuffer> pipeline, int width, int height, DirectXUploaderFormatDescriptor descriptor)
        {
            _dx = dx.AddRef();
            _pipeline = pipeline;
            _width = width;
            _height = height;
            _descriptor = descriptor;
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

        public static DirectXUploader Create(DirectXContext dx, int pixelFormat, int width, int height)
        {
            int aligned = ((width - 1) / 16 + 1) * 16;

            if (aligned != width)
            {
                Log.Information($"DirectXUploader created with aligned {aligned} width instead of {width}");
                width = aligned;
            }

            var desc = s_descriptors[pixelFormat];

            var config = new DirectXPipelineConfig
            {
                VertexShaderFile = "format_conversion.hlsl",
                VertexShaderFunction = desc.VetextShaderFunction,
                PixelShaderFile = "format_conversion.hlsl",
                PixelShaderFunction = desc.PixelShaderFunction,
               // Blend = true
            };

            var pipeline = new DirectXPipeline<ConverterFilterConstantBuffer>(config, dx);
            pipeline.SetDebugColor(1, 0, 1, 1);
            pipeline.SetPosition(DirectXPipelineConfig.FullRectangle, new Viewport(0, 0, width, height));

            var colorMatrix = ColorMatrices.NotFull601;
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

            return new DirectXUploader(dx, pipeline, width, height, desc);
        }

        public void Dispose()
        {
            try
            {
                _defferedContext?.Dispose();
                _pipeline?.Dispose();
            }
            catch { }
            _dx.RemoveRef();
        }

        public DirectXResource Upload(IntPtr dataPointer, IntPtr dataPointer1, IntPtr dataPointer2)
        {
            try
            {
                if (_dx.IsBrokenAndLog("Upload"))
                    return null;

                _defferedContext = _defferedContext ?? new DeviceContext(_dx.Device);

                var source0 = LoadPlane(0, dataPointer);
                var source1 = LoadPlane(1, dataPointer1);
                var source2 = LoadPlane(2, dataPointer2);
                var target = _dx.Pool.Get("uploadTarget", DirectXResource.Desc(_width, _height));

                using (var srv0 = source0.GetShaderResourceView())
                using (var srv1 = source1?.GetShaderResourceView())
                using (var srv2 = source2?.GetShaderResourceView())
                using (var rtv = target.GetRenderTargetView())
                {
                    _pipeline.Render(_defferedContext, rtv, srv0, srv1, srv2);
                    _dx.Flush(_defferedContext, "er");
                }

                _dx.Pool.Back(source0);
                _dx.Pool.Back(source1);
                _dx.Pool.Back(source2);
                return target;
            }
            catch(Exception e)
            {
                _dx.Broken(e);
            }
            return null;
        }

        private DirectXResource LoadPlane(int num, IntPtr dataPointer)
        {
            if (dataPointer != IntPtr.Zero && num < _descriptor.Planes.Length)
            {
                var plane = _descriptor.Planes[num];
                DataBox db = new DataBox
                {
                    DataPointer = dataPointer,
                    RowPitch = (int)(_width * plane.PitchFactor)
                };
                return _dx.Pool.Get("uploadSource" + num, DirectXResource.Desc(_width / plane.WidthFactor, _height / plane.HeightFactor, plane.Format, BindFlags.ShaderResource, ResourceUsage.Immutable), db);
            }
            return null;
        }
    }
}
