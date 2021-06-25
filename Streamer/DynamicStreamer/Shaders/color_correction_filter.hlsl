/*****************************************************************************
Copyright (C) 2016 by c3r1c3 <c3r1c3@nevermindonline.com>

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
*****************************************************************************/

uniform float4x4 ViewProj;
uniform texture2D image;

uniform float3 gamma;

/* Pre-Compute variables. */
uniform float4x4 color_matrix;


SamplerState textureSampler {
	Filter   = Linear;
	AddressU = Clamp;
	AddressV = Clamp;
};

struct VertData1 {
	float4 pos : POSITION;
	float2 uv : TEXCOORD0;
};

struct VertData {
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
};

VertData VSDefault(VertData1 vert_in)
{
	VertData vert_out;
	vert_out.pos = mul(float4(vert_in.pos.xyz, 1.0), ViewProj);
	vert_out.uv = vert_in.uv;
	return vert_out;
}

float4 PSColorFilterRGBA(VertData vert_in) : SV_TARGET
{
	/* Grab the current pixel to perform operations on. */
	float4 currentPixel = image.Sample(textureSampler, vert_in.uv);

	/* Always address the gamma first. */
	currentPixel.rgb = pow(currentPixel.rgb, gamma);

	/* Much easier to manipulate pixels for these types of operations
	 * when in a matrix such as the below. See
	 * http://www.graficaobscura.com/matrix/index.html and
	 * https://docs.rainmeter.net/tips/colormatrix-guide/for more info.
	 */
	currentPixel = mul(color_matrix, currentPixel);

	return currentPixel;
}
