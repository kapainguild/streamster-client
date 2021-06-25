// adapted from OBS project
uniform float4x4 ViewProj;
uniform Texture2D image;

SamplerState def_sampler {
	Filter   = Linear;
	AddressU = Clamp;
	AddressV = Clamp;
};

struct VertInOut {
	float4 pos : POSITION;
	float2 uv  : TEXCOORD0;
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


float4 PSDrawBare(VertInOut vert_in) : SV_TARGET
{
	return image.Sample(def_sampler, vert_in.uv);
}

float4 PSDrawAlphaDivide(VertInOut vert_in) : SV_TARGET
{
	float4 rgba = image.Sample(def_sampler, vert_in.uv);
	float alpha = rgba.a;
	float multiplier = (alpha > 0.0) ? (1.0 / alpha) : 0.0;
	return float4(rgba.rgb * multiplier, alpha);
}
