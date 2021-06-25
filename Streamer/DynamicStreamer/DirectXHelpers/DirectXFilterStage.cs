using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.WIC;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.DirectXHelpers
{
    public interface IDirectXFilterStage : IDisposable
    {
        DirectXResource Render(DeviceContext ctx, DirectXResource current);
    };

    public class DirectXFilterStage<TConstantBuffer> : IDirectXFilterStage where TConstantBuffer : struct
    {
        public  DirectXPipeline<TConstantBuffer> Pipeline { get; set; }

        public DirectXContext Dx { get; }

        public DirectXFilterStage(DirectXContext dx, DirectXPipelineConfig config)
        {
            Dx = dx.AddRef();
            Pipeline = new DirectXPipeline<TConstantBuffer>(config, Dx);
        }

        public virtual void Dispose()
        {
            Dx.RemoveRef();
            Pipeline?.Dispose();
        }

        public virtual DirectXResource Render(DeviceContext ctx, DirectXResource source)
        {
            var target = Dx.Pool.Get("FilterStage", DirectXResource.Desc(source.Texture2D.Description.Width, source.Texture2D.Description.Height));

            using var rtv = target.GetRenderTargetView();
            using var srv = source.GetShaderResourceView();

            Pipeline.SetPosition(DirectXPipelineConfig.FullRectangle, new Viewport(0, 0, source.Texture2D.Description.Width, source.Texture2D.Description.Height));
            Render(ctx, rtv, srv, source.Texture2D.Description.Width, source.Texture2D.Description.Height);

            return target;
        }

        protected virtual void Render(DeviceContext ctx, RenderTargetView rtv, ShaderResourceView srv, int widht, int height)
        {
            Pipeline.Render(ctx, rtv, srv);
        }
    }


    
}
