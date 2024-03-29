// adapted from OBS project
/*
 * bilinear low res scaling, samples 8 pixels of a larger image to scale to a
 * low resolution image below half size
 */


uniform float4x4 ViewProj;
uniform Texture2D image;

SamplerState textureSampler {
	Filter    = Linear;
	AddressU  = Clamp;
	AddressV  = Clamp;
};

struct VertData {
	float4 pos : POSITION;
	float2 uv  : TEXCOORD0;
};

struct VertOut {
	float4 pos : SV_POSITION;
	float2 uv  : TEXCOORD;
};

VertOut VSDefault(VertData v_in)
{
	VertData vert_out;
	vert_out.pos = mul(float4(v_in.pos.xyz, 1.0), ViewProj);
	vert_out.uv = v_in.uv;
	return vert_out;
}


float4 pixel(float2 uv)
{
	return image.Sample(textureSampler, uv);
}

float4 DrawLowresBilinear(VertData v_in)
{
	float2 uv = v_in.uv;
	float2 stepxy  = float2(ddx(uv.x), ddy(uv.y));
	float2 stepxy1 = stepxy * 0.0625;
	float2 stepxy3 = stepxy * 0.1875;
	float2 stepxy5 = stepxy * 0.3125;
	float2 stepxy7 = stepxy * 0.4375;

	// Simulate Direct3D 8-sample pattern
	float4 out_color;
	out_color  = pixel(uv + float2( stepxy1.x, -stepxy3.y));
	out_color += pixel(uv + float2(-stepxy1.x,  stepxy3.y));
	out_color += pixel(uv + float2( stepxy5.x,  stepxy1.y));
	out_color += pixel(uv + float2(-stepxy3.x, -stepxy5.y));
	out_color += pixel(uv + float2(-stepxy5.x,  stepxy5.y));
	out_color += pixel(uv + float2(-stepxy7.x, -stepxy1.y));
	out_color += pixel(uv + float2( stepxy3.x,  stepxy7.y));
	out_color += pixel(uv + float2( stepxy7.x, -stepxy7.y));
	return out_color * 0.125;
}

float4 PSDrawLowresBilinearRGBA(VertData v_in) : SV_TARGET
{
	return DrawLowresBilinear(v_in);
}

float4 PSDrawLowresBilinearRGBADivide(VertOut v_in) : SV_TARGET
{
	float4 rgba = DrawLowresBilinear(v_in);
	float alpha = rgba.a;
	float multiplier = (alpha > 0.0) ? (1.0 / alpha) : 0.0;
	return float4(rgba.rgb * multiplier, alpha);
}
