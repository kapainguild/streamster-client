using SharpDX;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DynamicStreamer.DirectXHelpers
{
    public record ColorCorrectionConfig(double Gamma = 0.5, double Contrast = 0.5, double Brightness = 0.5, double Saturation = 0.5, double HueShift = 0.5, double Opacity = 1.0);

    class DirectXFilterStageColorCorrection : DirectXFilterStage<ColorCorrectionConstantBuffer>
    {
		static float root3 = 0.57735f;
		static float red_weight = 0.299f;
		static float green_weight = 0.587f;
		static float blue_weight = 0.114f;

		public DirectXFilterStageColorCorrection(DirectXContext dx, ColorCorrectionConfig ccConfig) : base(dx, new DirectXPipelineConfig
            {
                PixelShaderFile = "color_correction_filter.hlsl",
                VertexShaderFile = "color_correction_filter.hlsl",
                PixelShaderFunction = "PSColorFilterRGBA",
                VertexShaderFunction = "VSDefault",
                Blend = false
            })
        {
            var cc = Normalize(ccConfig);

            var cb = CreateBuffer(cc);
            Pipeline.SetConstantBuffer(cb);
		}

        private ColorCorrectionConstantBuffer CreateBuffer(ColorCorrectionConfig c)
        {
			/* Build our Gamma numbers. */
			double gamma = c.Gamma;
			gamma = (gamma< 0.0) ? (-gamma + 1.0) : (1.0 / (gamma + 1.0));
			var gammaVec = new Vector3((float) gamma, (float) gamma, (float) gamma);

			/* Build our contrast number. */
			var contrast = (float)c.Contrast + 1.0f;
			float one_minus_con = (1.0f - contrast) / 2.0f;

			/* Now let's build our Contrast matrix. */
		    var con_matrix = new Matrix(contrast,
					      0.0f,
					      0.0f,
					      0.0f,
					      0.0f,
					      contrast,
					      0.0f,
					      0.0f,
					      0.0f,
					      0.0f,
					      contrast,
					      0.0f,
					      one_minus_con,
					      one_minus_con,
					      one_minus_con,
					      1.0f);

			/* Build our brightness number. */
			var brightness = (float)c.Brightness;

			/*
			 * Now let's build our Brightness matrix.
			 * Earlier (in the function color_correction_filter_create) we set
			 * this matrix to the identity matrix, so now we only need
			 * to set the 3 variables that have changed.
			 */
			var bright_matrix = Matrix.Identity;
			bright_matrix.M41 = brightness;
			bright_matrix.M42 = brightness;
			bright_matrix.M43 = brightness;

			/* Build our Saturation number. */
			var saturation = (float)c.Saturation + 1.0f;

			/* Factor in the selected color weights. */
			float one_minus_sat_red = (1.0f - saturation) * red_weight;
			float one_minus_sat_green = (1.0f - saturation) * green_weight;
			float one_minus_sat_blue = (1.0f - saturation) * blue_weight;
			float sat_val_red = one_minus_sat_red + saturation;
			float sat_val_green = one_minus_sat_green + saturation;
			float sat_val_blue = one_minus_sat_blue + saturation;

			/* Now we build our Saturation matrix. */
			var sat_matrix = new Matrix(sat_val_red,
					      one_minus_sat_red,
					      one_minus_sat_red,
					      0.0f,
					      one_minus_sat_green,
					      sat_val_green,
					      one_minus_sat_green,
					      0.0f,
					      one_minus_sat_blue,
					      one_minus_sat_blue,
					      sat_val_blue,
					      0.0f,
					      0.0f,
					      0.0f,
					      0.0f,
					      1.0f);

			/* Build our Hue number. */
			var hue_shift = (float)c.HueShift;

			/* Build our Transparency number. */
			var opacity = (float)c.Opacity * 0.01f;

			/* Hue is the radian of 0 to 360 degrees. */
			float half_angle = 0.5f * (float)(hue_shift / (180.0f / Math.PI));

			/* Pseudo-Quaternion To Matrix. */
			float rot_quad1 = root3 * (float)Math.Sin(half_angle);
			var rot_quaternion = new Vector3(rot_quad1, rot_quad1, rot_quad1);
			var rot_quaternion_w = (float)Math.Cos(half_angle);

			var cross = rot_quaternion * rot_quaternion;
			var square = rot_quaternion * rot_quaternion;
			var wimag = rot_quaternion * rot_quaternion_w;

			square = square * 2.0f;
			var diag = new Vector3(0.5f, 0.5f, 0.5f) - square;
			var a_line = cross + wimag;
			var b_line = cross - wimag;

			/* Now we build our Hue and Opacity matrix. */
			var hue_op_matrix = new Matrix(diag.X * 2.0f,
									 b_line.Z * 2.0f,
									 a_line.Y * 2.0f,
									 0.0f,

									 a_line.Z * 2.0f,
									 diag.Y * 2.0f,
									 b_line.X * 2.0f,
									 0.0f,

									 b_line.Y * 2.0f,
									 a_line.X * 2.0f,
									 diag.Z * 2.0f,
									 0.0f,

									 0.0f,
									 0.0f,
									 0.0f,
									 opacity);

			//var color = new Vector4(1, 1, 1, 1); // 

			//var color_matrix = new Matrix();
			//color_matrix.M11 = color.X;
			//color_matrix.M22 = color.Y;
			//color_matrix.M33 = color.Z;

			//color_matrix.M41 = color.W * color.X;
			//color_matrix.M42 = color.W * color.Y;
			//color_matrix.M43 = color.W * color.Z;

			var final_matrix = bright_matrix * con_matrix * sat_matrix * hue_op_matrix; //* color_matrix;

			return new ColorCorrectionConstantBuffer { ColorMatrix = final_matrix, Gamma = gammaVec, ViewProj = Matrix.Identity };
        }

        private ColorCorrectionConfig Normalize(ColorCorrectionConfig i)
        {
            return new ColorCorrectionConfig(
                Normalize(i.Gamma, -0.60, 0.0, 3.0), 
                Normalize(i.Contrast, -0.56, 0.0, 0.80),
                Normalize(i.Brightness, -0.18, 0.0, 0.34),
                Normalize(i.Saturation, -1.0, 0.0, 3.0),
                Normalize(i.HueShift, -180.0, 0.0, 180.0),
                Normalize(i.Opacity, 0.0, 50.0, 100.0));
        }

        private double Normalize(double val, double min, double med, double max)
        {
            if (val < 0.5)
                return (val - 0.5) / 0.5 * (med - min) + med;
            else
                return (val - 0.5) / 0.5 * (max - med) + med;
        }
    }

	[StructLayout(LayoutKind.Sequential, Pack = 4)]
	public struct ColorCorrectionConstantBuffer
    {
		public Matrix ViewProj;
		public Vector3 Gamma;
		public float dummy;
        public Matrix ColorMatrix;
    }
}
