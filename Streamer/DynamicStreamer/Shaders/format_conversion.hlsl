/******************************************************************************
    Copyright (C) 2014 by Hugh Bailey <obs.jim@gmail.com>

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 2 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
******************************************************************************/

uniform float     width;
uniform float     height;
uniform float     width_i;
uniform float     width_d2;
uniform float     height_d2;
uniform float     width_x2_i;

uniform float4    color_vec0;
uniform float4    color_vec1;
uniform float4    color_vec2;
uniform float3    color_range_min = {0.0, 0.0, 0.0};
uniform float3    color_range_max = {1.0, 1.0, 1.0};

uniform Texture2D image;
uniform Texture2D image1;
uniform Texture2D image2;
uniform Texture2D image3;

SamplerState def_sampler {
	Filter   = Linear;
	AddressU = Clamp;
	AddressV = Clamp;
};

struct FragPos {
	float4 pos : SV_POSITION;
};

struct VertTexPos {
	float2 uv  : TEXCOORD0;
	float4 pos : SV_POSITION;
};

struct VertPosWide {
	float3 pos_wide : TEXCOORD0;
	float4 pos : SV_POSITION;
};

struct VertTexPosWide {
	float3 uuv : TEXCOORD0;
	float4 pos : SV_POSITION;
};

struct FragTex {
	float2 uv : TEXCOORD0;
};

struct FragPosWide {
	float3 pos_wide : TEXCOORD0;
};

struct FragTexWide {
	float3 uuv : TEXCOORD0;
};

VertTexPos VSTexPosHalf_Reverse(uint id : SV_VertexID)
{
	float idHigh = float(id >> 1);
	float idLow = float(id & uint(1));

	float x = idHigh * 4.0 - 1.0;
	float y = idLow * 4.0 - 1.0;

	float u = idHigh * 2.0;
	float v = (1.0 - idLow * 2.0);

	VertTexPos vert_out;
	vert_out.uv = float2(width_d2 * u, height * v);
	vert_out.pos = float4(x, y, 0.0, 1.0);
	return vert_out;
}



FragPos VSPos(uint id : SV_VertexID)
{
	float idHigh = float(id >> 1);
	float idLow = float(id & uint(1));

	float x = idHigh * 4.0 - 1.0;
	float y = idLow * 4.0 - 1.0;

	FragPos vert_out;
	vert_out.pos = float4(x, y, 0.0, 1.0);
	return vert_out;
}

VertTexPosWide VSTexPos_Left(uint id : SV_VertexID)
{
	float idHigh = float(id >> 1);
	float idLow = float(id & uint(1));

	float x = idHigh * 4.0 - 1.0;
	float y = idLow * 4.0 - 1.0;

	float u_right = idHigh * 2.0;
	float u_left = u_right - width_i;
	float v = /*obs_glsl_compile ? (idLow * 2.0) :*/ (1.0 - idLow * 2.0);

	VertTexPosWide vert_out;
	vert_out.uuv = float3(u_left, u_right, v);
	vert_out.pos = float4(x, y, 0.0, 1.0);
	return vert_out;
}

VertTexPos VSTexPosHalfHalf_Reverse(uint id : SV_VertexID)
{
	float idHigh = float(id >> 1);
	float idLow = float(id & uint(1));

	float x = idHigh * 4.0 - 1.0;
	float y = idLow * 4.0 - 1.0;

	float u = idHigh * 2.0;
	float v = /*obs_glsl_compile ? (idLow * 2.0) :*/ (1.0 - idLow * 2.0);

	VertTexPos vert_out;
	vert_out.uv = float2(width_d2 * u, height_d2 * v);
	vert_out.pos = float4(x, y, 0.0, 1.0);
	return vert_out;
}

VertPosWide VSPosWide_Reverse(uint id : SV_VertexID)
{
	float idHigh = float(id >> 1);
	float idLow = float(id & uint(1));

	float x = idHigh * 4.0 - 1.0;
	float y = idLow * 4.0 - 1.0;

	float u = idHigh * 2.0;
	float v = /*obs_glsl_compile ? (idLow * 2.0) : */(1.0 - idLow * 2.0);

	VertPosWide vert_out;
	vert_out.pos_wide = float3(float2(width, width_d2) * u, height * v);
	vert_out.pos = float4(x, y, 0.0, 1.0);
	return vert_out;
}

float PS_Y(FragPos frag_in) : SV_TARGET
{
	float3 rgb = image.Load(int3(frag_in.pos.xy, 0)).rgb;
	float y = dot(color_vec0.xyz, rgb) + color_vec0.w;
	return y;
}

float2 PS_UV_Wide(FragTexWide frag_in) : SV_TARGET
{
	float3 rgb_left = image.Sample(def_sampler, frag_in.uuv.xz).rgb;
	float3 rgb_right = image.Sample(def_sampler, frag_in.uuv.yz).rgb;
	float3 rgb = (rgb_left + rgb_right) * 0.5;
	float u = dot(color_vec1.xyz, rgb) + color_vec1.w;
	float v = dot(color_vec2.xyz, rgb) + color_vec2.w;
	return float2(u, v);
}

float PS_U(FragPos frag_in) : SV_TARGET
{
	float3 rgb = image.Load(int3(frag_in.pos.xy, 0)).rgb;
	float u = dot(color_vec1.xyz, rgb) + color_vec1.w;
	return u;
}

float PS_V(FragPos frag_in) : SV_TARGET
{
	float3 rgb = image.Load(int3(frag_in.pos.xy, 0)).rgb;
	float v = dot(color_vec2.xyz, rgb) + color_vec2.w;
	return v;
}

float PS_U_Wide(FragTexWide frag_in) : SV_TARGET
{
	float3 rgb_left = image.Sample(def_sampler, frag_in.uuv.xz).rgb;
	float3 rgb_right = image.Sample(def_sampler, frag_in.uuv.yz).rgb;
	float3 rgb = (rgb_left + rgb_right) * 0.5;
	float u = dot(color_vec1.xyz, rgb) + color_vec1.w;
	return u;
}

float PS_V_Wide(FragTexWide frag_in) : SV_TARGET
{
	float3 rgb_left = image.Sample(def_sampler, frag_in.uuv.xz).rgb;
	float3 rgb_right = image.Sample(def_sampler, frag_in.uuv.yz).rgb;
	float3 rgb = (rgb_left + rgb_right) * 0.5;
	float v = dot(color_vec2.xyz, rgb) + color_vec2.w;
	return v;
}

float3 YUV_to_RGB(float3 yuv)
{
	yuv = clamp(yuv, color_range_min, color_range_max);
	float r = dot(color_vec0.xyz, yuv) + color_vec0.w;
	float g = dot(color_vec1.xyz, yuv) + color_vec1.w;
	float b = dot(color_vec2.xyz, yuv) + color_vec2.w;
	return float3(r, g, b);
}

float4 PSYUY2_Reverse(FragTex frag_in) : SV_TARGET
{
	float4 y2uv = image.Load(int3(frag_in.uv.xy, 0));
	float2 y01 = y2uv.zx;
	float2 cbcr = y2uv.yw;
	float leftover = frac(frag_in.uv.x);
	float y = (leftover < 0.5) ? y01.x : y01.y;
	float3 yuv = float3(y, cbcr);
	float3 rgb = YUV_to_RGB(yuv);
	return float4(rgb, 1.0);
}

float4 PSPlanar422_DS_Reverse(FragPosWide frag_in) : SV_TARGET
{
	float y = image.Load(int3(frag_in.pos_wide.xz, 0)).x;
	int3 xy0_chroma = int3(frag_in.pos_wide.yz, 0);
	float cb = image1.Load(xy0_chroma).x;
	float cr = image2.Load(xy0_chroma).x;
	float3 yuv = float3(y, cb, cr);
	float3 rgb = YUV_to_RGB(yuv);
	return float4(rgb, 1);
}

float4 PSYUY3333_Reverse(FragTex frag_in) : SV_TARGET
{
	float4 y2uv = image.Load(int3(frag_in.uv.xy, 0));
	float2 y01 = y2uv.xz; // zx -> xz
	float2 cbcr = y2uv.yw;
	float leftover = frac(frag_in.uv.x);
	float y = (leftover < 0.5) ? y01.x : y01.y;
	float3 yuv = float3(y, cbcr);
	float3 rgb = YUV_to_RGB(yuv);
	return float4(rgb, 1.0);
}

float4 PSUYVY_Reverse(FragTex frag_in) : SV_TARGET
{
	float4 y2uv = image.Load(int3(frag_in.uv.xy, 0));
	//return float4(color_vec0.xyz, 1.0);
	//return y2uv;// float4(0, frag_in.uv.y / 1000, 1, 1);
	float2 y01 = y2uv.yw;
	float2 cbcr = y2uv.zx;
	float leftover = frac(frag_in.uv.x);
	float y = (leftover < 0.5) ? y01.x : y01.y;
	float3 yuv = float3(y, cbcr);
	float3 rgb = YUV_to_RGB(yuv);
	return float4(rgb, 1.0);
}


float4 PSYVYU_Reverse(FragTex frag_in) : SV_TARGET
{
	float4 y2uv = image.Load(int3(frag_in.uv.xy, 0));
	float2 y01 = y2uv.zx;
	float2 cbcr = y2uv.wy;
	float leftover = frac(frag_in.uv.x);
	float y = (leftover < 0.5) ? y01.x : y01.y;
	float3 yuv = float3(y, cbcr);
	float3 rgb = YUV_to_RGB(yuv);
	return float4(rgb, 1.0);
}

float3 PSPlanar420_Reverse(VertTexPos frag_in) : SV_TARGET
{
	float y = image.Load(int3(frag_in.pos.xy, 0)).x;
	int3 xy0_chroma = int3(frag_in.uv, 0);
	float cb = image1.Load(xy0_chroma).x;
	float cr = image2.Load(xy0_chroma).x;
	float3 yuv = float3(y, cb, cr);
	float3 rgb = YUV_to_RGB(yuv);
	return rgb;
}

float4 PSPlanar420A_Reverse(VertTexPos frag_in) : SV_TARGET
{
	int3 xy0_luma = int3(frag_in.pos.xy, 0);
	float y = image.Load(xy0_luma).x;
	int3 xy0_chroma = int3(frag_in.uv, 0);
	float cb = image1.Load(xy0_chroma).x;
	float cr = image2.Load(xy0_chroma).x;
	float alpha = image3.Load(xy0_luma).x;
	float3 yuv = float3(y, cb, cr);
	float4 rgba = float4(YUV_to_RGB(yuv), alpha);
	return rgba;
}

float3 PSPlanar422_Reverse(FragPosWide frag_in) : SV_TARGET
{
	float y = image.Load(int3(frag_in.pos_wide.xz, 0)).x;
	int3 xy0_chroma = int3(frag_in.pos_wide.yz, 0);
	float cb = image1.Load(xy0_chroma).x;
	float cr = image2.Load(xy0_chroma).x;
	float3 yuv = float3(y, cb, cr);
	float3 rgb = YUV_to_RGB(yuv);
	return rgb;
}

float4 PSPlanar422A_Reverse(FragPosWide frag_in) : SV_TARGET
{
	int3 xy0_luma = int3(frag_in.pos_wide.xz, 0);
	float y = image.Load(xy0_luma).x;
	int3 xy0_chroma = int3(frag_in.pos_wide.yz, 0);
	float cb = image1.Load(xy0_chroma).x;
	float cr = image2.Load(xy0_chroma).x;
	float alpha = image3.Load(xy0_luma).x;
	float3 yuv = float3(y, cb, cr);
	float4 rgba = float4(YUV_to_RGB(yuv), alpha);
	return rgba;
}

float3 PSPlanar444_Reverse(FragPos frag_in) : SV_TARGET
{
	int3 xy0 = int3(frag_in.pos.xy, 0);
	float y = image.Load(xy0).x;
	float cb = image1.Load(xy0).x;
	float cr = image2.Load(xy0).x;
	float3 yuv = float3(y, cb, cr);
	float3 rgb = YUV_to_RGB(yuv);
	return rgb;
}

float4 PSPlanar444A_Reverse(FragPos frag_in) : SV_TARGET
{
	int3 xy0 = int3(frag_in.pos.xy, 0);
	float y = image.Load(xy0).x;
	float cb = image1.Load(xy0).x;
	float cr = image2.Load(xy0).x;
	float alpha = image3.Load(xy0).x;
	float3 yuv = float3(y, cb, cr);
	float4 rgba = float4(YUV_to_RGB(yuv), alpha);
	return rgba;
}

float4 PSAYUV_Reverse(FragPos frag_in) : SV_TARGET
{
	float4 yuva = image.Load(int3(frag_in.pos.xy, 0));
	float4 rgba = float4(YUV_to_RGB(yuva.xyz), yuva.a);
	return rgba;
}

float3 PSNV12_Reverse(VertTexPos frag_in) : SV_TARGET
{
	float y = image.Load(int3(frag_in.pos.xy, 0)).x;
	float2 cbcr = image1.Load(int3(frag_in.uv, 0)).xy;
	float3 yuv = float3(y, cbcr);
	float3 rgb = YUV_to_RGB(yuv);
	return rgb;
}

float3 PSY800_Limited(FragPos frag_in) : SV_TARGET
{
	float limited = image.Load(int3(frag_in.pos.xy, 0)).x;
	float full = (255.0 / 219.0) * limited - (16.0 / 219.0);
	return float3(full, full, full);
}

float3 PSY800_Full(FragPos frag_in) : SV_TARGET
{
	float3 full = image.Load(int3(frag_in.pos.xy, 0)).xxx;
	return full;
}

float4 PSRGB_Limited(FragPos frag_in) : SV_TARGET
{
	float4 rgba = image.Load(int3(frag_in.pos.xy, 0));
	rgba.rgb = (255.0 / 219.0) * rgba.rgb - (16.0 / 219.0);
	return rgba;
}

float3 PSBGR3_Limited(FragPos frag_in) : SV_TARGET
{
	float x = frag_in.pos.x * 3.0;
	float y = frag_in.pos.y;
	float b = image.Load(int3(x - 1.0, y, 0)).x;
	float g = image.Load(int3(x, y, 0)).x;
	float r = image.Load(int3(x + 1.0, y, 0)).x;
	float3 rgb = float3(r, g, b);
	rgb = (255.0 / 219.0) * rgb - (16.0 / 219.0);
	return rgb;
}

float3 PSBGR3_Full(FragPos frag_in) : SV_TARGET
{
	float x = frag_in.pos.x * 3.0;
	float y = frag_in.pos.y;
	float b = image.Load(int3(x - 1.0, y, 0)).x;
	float g = image.Load(int3(x, y, 0)).x;
	float r = image.Load(int3(x + 1.0, y, 0)).x;
	float3 rgb = float3(r, g, b);
	return rgb;
}


/*
technique Planar_Y
{
	pass
	{
		vertex_shader = VSPos(id);
		pixel_shader  = PS_Y(frag_in);
	}
}

technique Planar_U
{
	pass
	{
		vertex_shader = VSPos(id);
		pixel_shader  = PS_U(frag_in);
	}
}

technique Planar_V
{
	pass
	{
		vertex_shader = VSPos(id);
		pixel_shader  = PS_V(frag_in);
	}
}

technique Planar_U_Left
{
	pass
	{
		vertex_shader = VSTexPos_Left(id);
		pixel_shader  = PS_U_Wide(frag_in);
	}
}

technique Planar_V_Left
{
	pass
	{
		vertex_shader = VSTexPos_Left(id);
		pixel_shader  = PS_V_Wide(frag_in);
	}
}

technique NV12_Y
{
	pass
	{
		vertex_shader = VSPos(id);
		pixel_shader  = PS_Y(frag_in);
	}
}

technique NV12_UV
{
	pass
	{
		vertex_shader = VSTexPos_Left(id);
		pixel_shader  = PS_UV_Wide(frag_in);
	}
}

technique UYVY_Reverse
{
	pass
	{
		vertex_shader = VSTexPosHalf_Reverse(id);
		pixel_shader  = PSUYVY_Reverse(frag_in);
	}
}

technique YUY2_Reverse
{
	pass
	{
		vertex_shader = VSTexPosHalf_Reverse(id);
		pixel_shader  = PSYUY2_Reverse(frag_in);
	}
}

technique YVYU_Reverse
{
	pass
	{
		vertex_shader = VSTexPosHalf_Reverse(id);
		pixel_shader  = PSYVYU_Reverse(frag_in);
	}
}

technique I420_Reverse
{
	pass
	{
		vertex_shader = VSTexPosHalfHalf_Reverse(id);
		pixel_shader  = PSPlanar420_Reverse(frag_in);
	}
}

technique I40A_Reverse
{
	pass
	{
		vertex_shader = VSTexPosHalfHalf_Reverse(id);
		pixel_shader  = PSPlanar420A_Reverse(frag_in);
	}
}

technique I422_Reverse
{
	pass
	{
		vertex_shader = VSPosWide_Reverse(id);
		pixel_shader  = PSPlanar422_Reverse(frag_in);
	}
}

technique I42A_Reverse
{
	pass
	{
		vertex_shader = VSPosWide_Reverse(id);
		pixel_shader  = PSPlanar422A_Reverse(frag_in);
	}
}

technique I444_Reverse
{
	pass
	{
		vertex_shader = VSPos(id);
		pixel_shader  = PSPlanar444_Reverse(frag_in);
	}
}

technique YUVA_Reverse
{
	pass
	{
		vertex_shader = VSPos(id);
		pixel_shader  = PSPlanar444A_Reverse(frag_in);
	}
}

technique AYUV_Reverse
{
	pass
	{
		vertex_shader = VSPos(id);
		pixel_shader  = PSAYUV_Reverse(frag_in);
	}
}

technique NV12_Reverse
{
	pass
	{
		vertex_shader = VSTexPosHalfHalf_Reverse(id);
		pixel_shader  = PSNV12_Reverse(frag_in);
	}
}

technique Y800_Limited
{
	pass
	{
		vertex_shader = VSPos(id);
		pixel_shader  = PSY800_Limited(frag_in);
	}
}

technique Y800_Full
{
	pass
	{
		vertex_shader = VSPos(id);
		pixel_shader  = PSY800_Full(frag_in);
	}
}

technique RGB_Limited
{
	pass
	{
		vertex_shader = VSPos(id);
		pixel_shader  = PSRGB_Limited(frag_in);
	}
}

technique BGR3_Limited
{
	pass
	{
		vertex_shader = VSPos(id);
		pixel_shader  = PSBGR3_Limited(frag_in);
	}
}

technique BGR3_Full
{
	pass
	{
		vertex_shader = VSPos(id);
		pixel_shader  = PSBGR3_Full(frag_in);
	}
}
*/