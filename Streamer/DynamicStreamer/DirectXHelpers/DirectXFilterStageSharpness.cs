using SharpDX;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicStreamer.DirectXHelpers
{
    class DirectXFilterStageSharpness : DirectXFilterStage<SharpnessConstantBuffer>
    {
        private float _amount;

        public DirectXFilterStageSharpness(DirectXContext dx, double amount) : base(dx, new DirectXPipelineConfig
        {
            PixelShaderFile = "sharpness.hlsl",
            VertexShaderFile = "sharpness.hlsl",
            PixelShaderFunction = "PSDrawBare",
            VertexShaderFunction = "VSDefault",
            Blend = false
        })
        {
            _amount = (float)amount;
        }

        protected override void Render(DeviceContext ctx, RenderTargetView rtv, ShaderResourceView srv, int width, int height)
        {
            Pipeline.SetConstantBuffer(new SharpnessConstantBuffer
            {
                ViewProj = Matrix.Identity,
                sharpness = _amount,
                width = width,
                height = height
            }, true);

            base.Render(ctx, rtv, srv, width, height);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct SharpnessConstantBuffer
    {
        public Matrix ViewProj;
        public float sharpness;
        public float width;
        public float height;
        public float dummy1;
    }
}
