// adapted from OBS project

uniform float4x4 ViewProj;

uniform texture2D image : register(t0);
uniform texture3D clut_3d: register(t1);
uniform texture2D clut_1d : register(t2);

uniform float clut_amount;
uniform float3 clut_scale;
uniform float3 clut_offset;
uniform float3 domain_min;
uniform float3 domain_max;
uniform float cube_width_i;

SamplerState textureSampler {
	Filter    = Linear;
	AddressU  = Clamp;
	AddressV  = Clamp;
	AddressW  = Clamp;
};

struct VertDataIn {
	float4 pos : POSITION;
	float2 uv  : TEXCOORD0;
};

struct VertDataOut {
	float4 pos : SV_POSITION;
	float2 uv  : TEXCOORD0;
};

VertDataOut VSDefault(VertDataIn v_in)
{
	VertDataOut vert_out;
	vert_out.uv = v_in.uv;
	vert_out.pos = mul(float4(v_in.pos.xyz, 1.0), ViewProj);
	return vert_out;
}

float4 LUT1D(VertDataOut v_in) : SV_TARGET
{
	float4 textureColor = image.Sample(textureSampler, v_in.uv);

	if (textureColor.r >= domain_min.r && textureColor.r <= domain_max.r) {
		float u = textureColor.r * clut_scale.r + clut_offset.r;
		float channel = clut_1d.Sample(textureSampler, float2(u, 0.5)).r;
		textureColor.r = lerp(textureColor.r, channel, clut_amount);
	}

	if (textureColor.g >= domain_min.g && textureColor.g <= domain_max.g) {
		float u = textureColor.g * clut_scale.g + clut_offset.g;
		float channel = clut_1d.Sample(textureSampler, float2(u, 0.5)).g;
		textureColor.g = lerp(textureColor.g, channel, clut_amount);
	}

	if (textureColor.b >= domain_min.b && textureColor.b <= domain_max.b) {
		float u = textureColor.b * clut_scale.b + clut_offset.b;
		float channel = clut_1d.Sample(textureSampler, float2(u, 0.5)).b;
		textureColor.b = lerp(textureColor.b, channel, clut_amount);
	}

	return textureColor;
}


float4 LUT3D(VertDataOut v_in) : SV_TARGET
{
	float4 textureColor = image.Sample(textureSampler, v_in.uv);
	//textureColor.rgb = domain_max;
	//return textureColor;
	float r = textureColor.r;
	float g = textureColor.g;
	float b = textureColor.b;
	if (r >= domain_min.r && r <= domain_max.r &&
		g >= domain_min.g && g <= domain_max.g &&
		b >= domain_min.b && b <= domain_max.b)
	{
		float3 clut_pos = textureColor.rgb * clut_scale + clut_offset;
		float3 floor_pos = floor(clut_pos);

		float3 fracRGB = clut_pos - floor_pos;

		float3 uvw0 = (floor_pos + 0.5) * cube_width_i;
		float3 uvw3 = (floor_pos + 1.5) * cube_width_i;

		float fracL, fracM, fracS;
		float3 uvw1, uvw2;
		if (fracRGB.r < fracRGB.g) {
			if (fracRGB.r < fracRGB.b) {
				if (fracRGB.g < fracRGB.b) {
					// f(R) < f(G) < f(B)
					fracL = fracRGB.b;
					fracM = fracRGB.g;
					fracS = fracRGB.r;
					uvw1 = float3(uvw0.x, uvw0.y, uvw3.z);
					uvw2 = float3(uvw0.x, uvw3.y, uvw3.z);
				} else {
					// f(R) < f(B) <= f(G)
					fracL = fracRGB.g;
					fracM = fracRGB.b;
					fracS = fracRGB.r;
					uvw1 = float3(uvw0.x, uvw3.y, uvw0.z);
					uvw2 = float3(uvw0.x, uvw3.y, uvw3.z);
				}
			} else {
				// f(B) <= f(R) < f(G)
				fracL = fracRGB.g;
				fracM = fracRGB.r;
				fracS = fracRGB.b;
				uvw1 = float3(uvw0.x, uvw3.y, uvw0.z);
				uvw2 = float3(uvw3.x, uvw3.y, uvw0.z);
			}
		} else if (fracRGB.r < fracRGB.b) {
			// f(G) <= f(R) < f(B)
			fracL = fracRGB.b;
			fracM = fracRGB.r;
			fracS = fracRGB.g;
			uvw1 = float3(uvw0.x, uvw0.y, uvw3.z);
			uvw2 = float3(uvw3.x, uvw0.y, uvw3.z);
		} else if (fracRGB.g < fracRGB.b) {
			// f(G) < f(B) <= f(R)
			fracL = fracRGB.r;
			fracM = fracRGB.b;
			fracS = fracRGB.g;
			uvw1 = float3(uvw3.x, uvw0.y, uvw0.z);
			uvw2 = float3(uvw3.x, uvw0.y, uvw3.z);
		} else {
			// f(B) <= f(G) <= f(R)
			fracL = fracRGB.r;
			fracM = fracRGB.g;
			fracS = fracRGB.b;
			uvw1 = float3(uvw3.x, uvw0.y, uvw0.z);
			uvw2 = float3(uvw3.x, uvw3.y, uvw0.z);
		}

		/* use filtering to collapse 4 taps to 2 */
		/* use max to kill potential zero-divide NaN */

		float coeff01 = (1.0 - fracM);
		float weight01 = max((fracL - fracM) / coeff01, 0.0);
		float3 uvw01 = lerp(uvw0, uvw1, weight01);
		float3 sample01 = clut_3d.Sample(textureSampler, uvw01).rgb;

		float coeff23 = fracM;
		float weight23 = max(fracS / coeff23, 0.0);
		float3 uvw23 = lerp(uvw2, uvw3, weight23);
		float3 sample23 = clut_3d.Sample(textureSampler, uvw23).rgb;

		float3 luttedColor = (coeff01 * sample01) + (coeff23 * sample23);
		textureColor.rgb = lerp(textureColor.rgb, luttedColor, clut_amount);
	}

	return textureColor;
}
