// adapted from OBS project

uniform float4x4 ViewProj;
uniform texture_rect image;

sampler_state def_sampler {
	Filter   = Linear;
	AddressU = Clamp;
	AddressV = Clamp;
};

struct VertInOut {
	float4 pos : POSITION;
	float2 uv  : TEXCOORD0;
};

VertInOut VSDefault(VertInOut vert_in)
{
	VertInOut vert_out;
	vert_out.pos = mul(float4(vert_in.pos.xyz, 1.0), ViewProj);
	vert_out.uv  = vert_in.uv;
	return vert_out;
}

float4 PSDrawBare(VertInOut vert_in) : TARGET
{
	return image.Sample(def_sampler, vert_in.uv);
}

technique Draw
{
	pass
	{
		vertex_shader = VSDefault(vert_in);
		pixel_shader  = PSDrawBare(vert_in);
	}
}
