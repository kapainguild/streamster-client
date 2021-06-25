// adapted from OBS project

uniform float4x4 ViewProj;
uniform float2 base_dimension;
uniform float2 base_dimension_i;
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

struct VertInOut {
	float4 pos : SV_POSITION;
	float2 uv  : TEXCOORD;
	
};

VertInOut VSDefault(VertData vert_in)
{
	VertInOut vert_out;
	vert_out.pos = mul(float4(vert_in.pos.xyz, 1.0), ViewProj);
	vert_out.uv  = vert_in.uv;
	return vert_out;
}


float4 DrawArea(float2 uv)
{
	float2 uv_delta = float2(ddx(uv.x), ddy(uv.y));

	// Handle potential OpenGL flip.
	//if (obs_glsl_compile)
	//	uv_delta.y = abs(uv_delta.y);

	float2 uv_min = uv - 0.5 * uv_delta;
	float2 uv_max = uv_min + uv_delta;

	float2 load_index_begin = floor(uv_min * base_dimension);
	float2 load_index_end = ceil(uv_max * base_dimension);

	float2 target_dimension = 1.0 / uv_delta;
	float2 target_pos = uv * target_dimension;
	float2 target_pos_min = target_pos - 0.5;
	float2 target_pos_max = target_pos + 0.5;
	float2 scale = base_dimension_i * target_dimension;

	float4 total_color = float4(0.0, 0.0, 0.0, 0.0);

	float load_index_y = load_index_begin.y;
	do {
		float source_y_min = load_index_y * scale.y;
		float source_y_max = source_y_min + scale.y;
		float y_min = max(source_y_min, target_pos_min.y);
		float y_max = min(source_y_max, target_pos_max.y);
		float height = y_max - y_min;

		float load_index_x = load_index_begin.x;
		do {
			float source_x_min = load_index_x * scale.x;
			float source_x_max = source_x_min + scale.x;
			float x_min = max(source_x_min, target_pos_min.x);
			float x_max = min(source_x_max, target_pos_max.x);
			float width = x_max - x_min;
			float area = width * height;

			float4 color = image.Load(int3(load_index_x, load_index_y, 0));
			total_color += area * color;

			++load_index_x;
		} while (load_index_x < load_index_end.x);

		++load_index_y;
	} while (load_index_y < load_index_end.y);

	return total_color;
}

float4 PSDrawAreaRGBA(VertData frag_in) : SV_TARGET
{
	return DrawArea(frag_in.uv);
}

float4 PSDrawAreaRGBADivide(VertData frag_in) : SV_TARGET
{
	float4 rgba = DrawArea(frag_in.uv);
	float alpha = rgba.a;
	float multiplier = (alpha > 0.0) ? (1.0 / alpha) : 0.0;
	return float4(rgba.rgb * multiplier, alpha);
}

float4 PSDrawAreaRGBAUpscale(VertData frag_in) : SV_TARGET
{
	float2 uv = frag_in.uv;
	float2 uv_delta = float2(ddx(uv.x), ddy(uv.y));

	// Handle potential OpenGL flip.
	//if (obs_glsl_compile)
	//	uv_delta.y = abs(uv_delta.y);

	float2 uv_min = uv - 0.5 * uv_delta;
	float2 uv_max = uv_min + uv_delta;

	float2 load_index_first = floor(uv_min * base_dimension);
	float2 load_index_last = ceil(uv_max * base_dimension) - 1.0;

	if (load_index_first.x < load_index_last.x) {
		float uv_boundary_x = load_index_last.x * base_dimension_i.x;
		uv.x = ((uv.x - uv_boundary_x) / uv_delta.x) * base_dimension_i.x + uv_boundary_x;
	} else
		uv.x = (load_index_first.x + 0.5) * base_dimension_i.x;
	if (load_index_first.y < load_index_last.y) {
		float uv_boundary_y = load_index_last.y * base_dimension_i.y;
		uv.y = ((uv.y - uv_boundary_y) / uv_delta.y) * base_dimension_i.y + uv_boundary_y;
	} else
		uv.y = (load_index_first.y + 0.5) * base_dimension_i.y;

	return image.Sample(textureSampler, uv);
}
