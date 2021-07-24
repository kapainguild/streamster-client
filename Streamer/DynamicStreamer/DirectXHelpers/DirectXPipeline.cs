using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace DynamicStreamer.DirectXHelpers
{
    public class DirectXPipelineConfig
    {
        public static RectangleF FullRectangle = new RectangleF(0, 0, 1f, 1f);

        public string VertexShaderFile { get; set; }

        public string VertexShaderFunction { get; set; }

        public string PixelShaderFile { get; set; }

        public string PixelShaderFunction { get; set; }

        public bool Blend { get; set; }
    }

    public class DirectXPipeline<TConstantBuffer> : IDisposable where TConstantBuffer : struct
    {
        private CompilationResult _vertexShaderByteCode;
        private InputLayout _vertexLayout;
        private VertexShader _vertexShader;
        private CompilationResult _pixelShaderByteCode;
        private PixelShader _pixelShader;

        private SharpDX.Direct3D11.Buffer _dynamicConstantBuffer;
        private int _dynamicConstantBufferVersion;

        private TConstantBuffer _constantBufferStruct;
        private int _constantBufferVersion;

        private VertexBuffer _vertexBuffer;
        private bool _vertexBufferExternal;
        private Viewport _viewPort;

        private BlendState _blendState;
        private RawColor4? _debugColor = null;
        private readonly DirectXContext _dx;

        private static ConcurrentDictionary<string, CompilationResult> s_compilationCache = new ConcurrentDictionary<string, CompilationResult>();

        public DirectXPipeline(DirectXPipelineConfig config, DirectXContext dx)
        {
            try
            {
                _dx = dx.AddRef();

                _vertexShaderByteCode = CompileOrGet(config.VertexShaderFile, config.VertexShaderFunction, dx.VertexProfile); 
                _vertexShader = new VertexShader(dx.Device, _vertexShaderByteCode);
                _vertexLayout = new InputLayout(dx.Device, _vertexShaderByteCode, new[]
                {
                    new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0, InputClassification.PerVertexData, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float,   12, 0, InputClassification.PerVertexData, 0),
                });


                _pixelShaderByteCode = CompileOrGet(config.PixelShaderFile, config.PixelShaderFunction, dx.PixelProfile);
                _pixelShader = new PixelShader(dx.Device, _pixelShaderByteCode);

                //ShaderReflection refl = new ShaderReflection(_pixelShaderByteCode);
                //var s1 = refl.GetConstantBuffer(0);
                //var a1 = s1.GetVariable(0);
                //var a2 = s1.GetVariable(1);
                //var a3 = s1.GetVariable(2);
                //var a4 = s1.GetVariable(3);
                //var a5 = s1.GetVariable(4);
                //var a6 = s1.GetVariable(5);
                //var a7 = s1.GetVariable(6);


                var blendStateDescription = new BlendStateDescription();

                if (config.Blend)
                {
                    blendStateDescription.RenderTarget[0].IsBlendEnabled = true;
                    blendStateDescription.RenderTarget[0].SourceBlend = BlendOption.SourceAlpha;
                    blendStateDescription.RenderTarget[0].DestinationBlend = BlendOption.InverseSourceAlpha;
                    blendStateDescription.RenderTarget[0].BlendOperation = BlendOperation.Add;
                    blendStateDescription.RenderTarget[0].SourceAlphaBlend = BlendOption.One;
                    blendStateDescription.RenderTarget[0].DestinationAlphaBlend = BlendOption.Zero;
                    blendStateDescription.RenderTarget[0].AlphaBlendOperation = BlendOperation.Maximum;
                    blendStateDescription.RenderTarget[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;
                    blendStateDescription.AlphaToCoverageEnable = false;
                    //_immediateContext.OutputMerger.BlendSampleMask = ~0;
                    //_immediateContext.OutputMerger.BlendFactor = new Color4(0, 0, 0, 0);
                }
                else
                {
                    blendStateDescription.RenderTarget[0].IsBlendEnabled = false;
                    blendStateDescription.RenderTarget[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;
                }
                _blendState = new BlendState(dx.Device, blendStateDescription);

                if (typeof(TConstantBuffer) != typeof(int))
                {
                    int size = Marshal.SizeOf<TConstantBuffer>();
                    _dynamicConstantBuffer = new SharpDX.Direct3D11.Buffer(_dx.Device, size, ResourceUsage.Dynamic, BindFlags.ConstantBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, 0);
                }
            }
            catch (Exception e)
            {
                _dx.Broken(e);
            }
        }

        private CompilationResult CompileOrGet(string shaderFile, string shaderFunction, string profile)
        {
            var key = $"{shaderFile}::{shaderFunction}::{profile}";
            return s_compilationCache.GetOrAdd(key, k => ShaderBytecode.Compile(DirectXHelper.ReadResource(shaderFile), shaderFunction, profile, ShaderFlags.None));
        }

        public void SetPosition(RectangleF inputLayout, Viewport viewPort, bool hflip = false) 
        {
            _viewPort = viewPort;

            UpdatePosition(inputLayout, new RectangleF(0, 0, 1, 1), hflip, ref _vertexBuffer);
        }

        public void SetExternalPosition(Viewport viewPort, VertexBuffer vb)
        {
            _viewPort = viewPort;
            _vertexBufferExternal = true;
            _vertexBuffer = vb;
        }

        public void UpdatePosition(RectangleF inputLaylout, RectangleF ptz, bool hflip, ref VertexBuffer buffer)
        {
            try
            {
                if (buffer == null ||
                    buffer.InputLayout != inputLaylout ||
                    buffer.InputPtz != ptz ||
                    buffer.InputHFlip != hflip)
                {
                    buffer?.Dispose();
                    buffer = new VertexBuffer
                    {
                        InputHFlip = hflip,
                        InputLayout = inputLaylout,
                        InputPtz = ptz,
                        Buffer = CreateBuffer(inputLaylout, ptz, hflip)
                    };
                }
            }
            catch (Exception e)
            {
                _dx.Broken(e);
            }
        }

        private SharpDX.Direct3D11.Buffer CreateBuffer(RectangleF inputLayout, RectangleF ptz, bool hflip)
        {
            float x1 = inputLayout.Left * 2 - 1;
            float x2 = inputLayout.Right * 2 - 1;

            float y1 = 1f - inputLayout.Top * 2;
            float y2 = 1f - inputLayout.Bottom * 2;

            return SharpDX.Direct3D11.Buffer.Create(_dx.Device, BindFlags.VertexBuffer, new[]
            {
                     x1,  y2,  0,      hflip ? ptz.Right: ptz.Left, ptz.Bottom,
                     x1,  y1,  0,      hflip ? ptz.Right: ptz.Left, ptz.Top,
                     x2,  y2,  0,      hflip ? ptz.Left: ptz.Right, ptz.Bottom,
                     x2,  y1,  0,      hflip ? ptz.Left: ptz.Right, ptz.Top
            });
        }

        public void SetConstantBuffer(TConstantBuffer cb, bool compare = false)
        {
            if (_dynamicConstantBuffer == null)
                return;

            if (compare)
            {
                if (Equals(_constantBufferStruct, cb))
                    return;
            }

            _constantBufferStruct = cb;
            _constantBufferVersion++;
        }

        public void SetDebugColor(float r, float g, float b, float a)
        {
            _debugColor = new RawColor4(r, g, b ,a);
        }

        public void ResetDebugColor()
        {
            _debugColor = null;
        }

        public void Render(DeviceContext ctx, RenderTargetView renderTargetView, params ShaderResourceView[] shaderResourceViews) 
        {
            try
            {
                ctx.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
                ctx.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(_vertexBuffer.Buffer, Utilities.SizeOf<float>() * 5, 0));
                ctx.InputAssembler.InputLayout = _vertexLayout;

                ctx.VertexShader.Set(_vertexShader);
                ctx.PixelShader.Set(_pixelShader);
                if (_dynamicConstantBuffer != null)
                {
                    if (_dynamicConstantBufferVersion != _constantBufferVersion)
                    {
                        _dynamicConstantBufferVersion = _constantBufferVersion;

                        var dataBox = ctx.MapSubresource(_dynamicConstantBuffer, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
                        Utilities.Write(dataBox.DataPointer, ref _constantBufferStruct);
                        ctx.UnmapSubresource(_dynamicConstantBuffer, 0);
                    }

                    ctx.VertexShader.SetConstantBuffer(0, _dynamicConstantBuffer);
                    ctx.PixelShader.SetConstantBuffer(0, _dynamicConstantBuffer);
                }
                ctx.PixelShader.SetShaderResources(0, shaderResourceViews);
                ctx.Rasterizer.SetViewport(_viewPort);
                ctx.OutputMerger.BlendState = _blendState;
                ctx.OutputMerger.SetRenderTargets(renderTargetView);

                if (_debugColor != null)
                    ctx.ClearRenderTargetView(renderTargetView, _debugColor.Value);
                ctx.Draw(4, 0);
                ctx.Flush();
            }
            catch (Exception e)
            {
                _dx.Broken(e);
            }
        }

        public void Dispose()
        {
            _vertexLayout?.Dispose();
            _vertexShader?.Dispose();

            _pixelShader?.Dispose();
            _blendState?.Dispose();

            _dynamicConstantBuffer?.Dispose();
            if (!_vertexBufferExternal)
                _vertexBuffer?.Dispose();
            _dx.RemoveRef();
        }
    }


    public class VertexBuffer : IDisposable
    {
        public bool InputHFlip { get; set; }

        public RectangleF InputLayout { get; set; }

        public RectangleF InputPtz { get; set; }

        public SharpDX.Direct3D11.Buffer Buffer { get; set; }

        public void Dispose()
        {
            Buffer?.Dispose();
            Buffer = null;
        }
    }
}
