using DynamicStreamer;
using DynamicStreamer.Contexts;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Streamster.ClientCore.Models
{
    public static class ModelToStreamerTranslator
    {
        public static VideoEncoderType Translate(EncoderType encoderType) => encoderType switch
        {
            EncoderType.Auto => VideoEncoderType.Auto,
            EncoderType.Hardware => VideoEncoderType.Hardware,
            EncoderType.Software => VideoEncoderType.Software,
            _ => VideoEncoderType.Auto
        };

        public static VideoEncoderQuality Translate(EncoderQuality encoderQuality) => encoderQuality switch
        {
            EncoderQuality.Speed => VideoEncoderQuality.Speed,
            EncoderQuality.Balanced => VideoEncoderQuality.Balanced,
            EncoderQuality.BalancedQuality => VideoEncoderQuality.BalancedQuality,
            EncoderQuality.Quality => VideoEncoderQuality.Quality,
            _ => VideoEncoderQuality.Balanced
        };


        public static PositionRect Translate(SceneRect i) => new PositionRect { Left = i.L, Top = i.T, Width = i.W, Height = i.H };

        internal static VideoRenderType Translate(RendererType rendererType) => rendererType switch
        {
            RendererType.HardwareSpecific => VideoRenderType.HardwareSpecific,
            RendererType.SoftwareFFMPEG => VideoRenderType.SoftwareFFMPEG,
            RendererType.SoftwareDirectX => VideoRenderType.SoftwareDirectX,
            _ => VideoRenderType.HardwareAuto,
        };

        internal static BlendingType Translate(BlenderType blenderType) => blenderType switch
        {
            BlenderType.Linear => BlendingType.Linear,
            BlenderType.Lanczos => BlendingType.Lanczos,
            BlenderType.BilinearLowRes => BlendingType.BilinearLowRes,
            BlenderType.Bicubic => BlendingType.Bicubic,
            BlenderType.Area => BlendingType.Area,
            _ => BlendingType.Smart,
        };

        internal static VideoFilterType Translate(SceneItemFilterType type) => type switch
        {
            SceneItemFilterType.None        => VideoFilterType.None,
            SceneItemFilterType.HFlip       => VideoFilterType.HFlip,
            SceneItemFilterType.VFlip       => VideoFilterType.VFlip,
            SceneItemFilterType.Warm        => VideoFilterType.Warm,
            SceneItemFilterType.Cold        => VideoFilterType.Cold,
            SceneItemFilterType.Dark        => VideoFilterType.Dark,
            SceneItemFilterType.Light       => VideoFilterType.Light,
            SceneItemFilterType.Vintage     => VideoFilterType.Vintage,
            SceneItemFilterType.Sepia       => VideoFilterType.Sepia,
            SceneItemFilterType.Grayscale   => VideoFilterType.Grayscale,
            SceneItemFilterType.Contrast    => VideoFilterType.Contrast,
            SceneItemFilterType.Brightness  => VideoFilterType.Brightness,
            SceneItemFilterType.Saturation  => VideoFilterType.Saturation,
            SceneItemFilterType.Gamma       => VideoFilterType.Gamma,
            SceneItemFilterType.Hue         => VideoFilterType.Hue,
            SceneItemFilterType.Opacity     => VideoFilterType.Opacity,
            SceneItemFilterType.Sharpness   => VideoFilterType.Sharpness,
            SceneItemFilterType.UserLut     => VideoFilterType.UserLut,
            SceneItemFilterType.Azure       => VideoFilterType.Azure,
            SceneItemFilterType.B_W         => VideoFilterType.B_W,
            SceneItemFilterType.Chill       => VideoFilterType.Chill,
            SceneItemFilterType.Pastel      => VideoFilterType.Pastel,
            SceneItemFilterType.Romantic    => VideoFilterType.Romantic,
            SceneItemFilterType.Sapphire    => VideoFilterType.Sapphire,
            SceneItemFilterType.Wine        => VideoFilterType.Wine,
                _ => VideoFilterType.None
        };
    }
}
