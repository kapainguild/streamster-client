using SharpDX;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicStreamer.DirectXHelpers
{
    public class DirectXFilterRuntime : IDisposable
    {
        private VideoFilterChainDescriptor _filterChain;
        private DirectXContext _dx;
        private List<IDirectXFilterStage> _stages = new List<IDirectXFilterStage>();

        public RefCounted<DirectXResource> Render(DirectXContext dx, VideoFilterChainDescriptor filterChain, DeviceContext ctx, RefCounted<DirectXResource> input)
        {
            var res = Render(dx, filterChain, ctx, input.Instance);
            if (ReferenceEquals(res, input.Instance))
                return input.AddRef();
            else
                return new RefCounted<DirectXResource>(res);
        }

        internal DirectXResource Render(DirectXContext dx, VideoFilterChainDescriptor filterChain, DeviceContext ctx, DirectXResource texture)
        {
            DirectXResource current = texture;

            try
            {
                if (dx != _dx || !Equals(filterChain, _filterChain))
                {
                    TearDown();
                    _dx = dx;
                    _filterChain = filterChain;
                    Setup(dx, filterChain);
                }
                
                foreach (var stage in _stages)
                {
                    var next = stage.Render(ctx, current);
                    if (current != next && current != texture)
                        dx.Pool.Back(current);
                    current = next;
                    dx.Flush(ctx, "Filter stage");
                }
            }
            catch (SharpDXException e)
            {
                _dx.Broken(e);
            }

            return current;
        }

        private void Setup(DirectXContext dx, VideoFilterChainDescriptor filterChain)
        {
            if (filterChain != null)
            {
                foreach (var chain in filterChain.Filters)
                {
                    var stage = Create(chain);
                    if (stage != null)
                        _stages.Add(stage);
                }
            }
        }

        private IDirectXFilterStage Create(VideoFilterDescriptor desc)
        {
            switch (desc.Type)
            {
                case VideoFilterType.Warm: 
                    return DirectXFilterStageLUT.Create(_dx, "warm.png");
                case VideoFilterType.Cold: 
                    return DirectXFilterStageLUT.Create(_dx, "cool.png");
                case VideoFilterType.Dark: 
                    return new DirectXFilterStageColorCorrection(_dx, new ColorCorrectionConfig(Brightness: 0.4));
                case VideoFilterType.Light:     
                    return new DirectXFilterStageColorCorrection(_dx, new ColorCorrectionConfig(Brightness: 0.6));
                case VideoFilterType.Vintage:  
                    return DirectXFilterStageLUT.Create(_dx, "vintage.png", SingleFrameType.Png, NormalizeValue(desc.Value));
                case VideoFilterType.Sepia:    
                    return DirectXFilterStageLUT.Create(_dx, "sepia.png", SingleFrameType.Png, NormalizeValue(desc.Value));
                case VideoFilterType.Grayscale:
                    return DirectXFilterStageLUT.Create(_dx, "grayscale.png", SingleFrameType.Png, NormalizeValue(desc.Value));
                case VideoFilterType.Contrast:
                    return new DirectXFilterStageColorCorrection(_dx, new ColorCorrectionConfig(Contrast: NormalizeValue(desc.Value)));
                case VideoFilterType.Brightness:
                    return new DirectXFilterStageColorCorrection(_dx, new ColorCorrectionConfig(Brightness: NormalizeValue(desc.Value)));
                case VideoFilterType.Saturation:
                    return new DirectXFilterStageColorCorrection(_dx, new ColorCorrectionConfig(Saturation: NormalizeValue(desc.Value)));
                case VideoFilterType.Gamma:    
                    return new DirectXFilterStageColorCorrection(_dx, new ColorCorrectionConfig(Gamma: NormalizeValue(desc.Value)));

                case VideoFilterType.Hue: 
                    return new DirectXFilterStageColorCorrection(_dx, new ColorCorrectionConfig(HueShift: NormalizeValue(desc.Value)));
                case VideoFilterType.Opacity: 
                    return new DirectXFilterStageColorCorrection(_dx, new ColorCorrectionConfig(Opacity: NormalizeValue(desc.Value)));
                case VideoFilterType.Sharpness: 
                    return new DirectXFilterStageSharpness(_dx, NormalizeValue(desc.Value));
                case VideoFilterType.UserLut:
                        return DirectXFilterStageLUT.Create(_dx, desc.Data.Buffer, desc.Data.Type, NormalizeValue(desc.Value));

                case VideoFilterType.Azure:
                    return DirectXFilterStageLUT.Create(_dx, "Azure.png", SingleFrameType.Png, NormalizeValue(desc.Value));
                case VideoFilterType.B_W:
                    return DirectXFilterStageLUT.Create(_dx, "B_W.png", SingleFrameType.Png, NormalizeValue(desc.Value));
                case VideoFilterType.Chill:
                    return DirectXFilterStageLUT.Create(_dx, "Chill.png", SingleFrameType.Png, NormalizeValue(desc.Value));
                case VideoFilterType.Pastel:
                    return DirectXFilterStageLUT.Create(_dx, "Pastel.png", SingleFrameType.Png, NormalizeValue(desc.Value));
                case VideoFilterType.Romantic:
                    return DirectXFilterStageLUT.Create(_dx, "Romantic.png", SingleFrameType.Png, NormalizeValue(desc.Value));
                case VideoFilterType.Sapphire:
                    return DirectXFilterStageLUT.Create(_dx, "Sapphire.png", SingleFrameType.Png, NormalizeValue(desc.Value));
                case VideoFilterType.Wine:
                    return DirectXFilterStageLUT.Create(_dx, "Wine.png", SingleFrameType.Png, NormalizeValue(desc.Value));

                default:
                    return null;
            }
        }

        private double NormalizeValue(double value) => (value + 1.0) / 2.0;
        
        private void TearDown()
        {
            foreach (var stage in _stages)
                stage.Dispose();

            _stages.Clear();
        }

        public void Dispose()
        {
            TearDown();
        }
    }
}
