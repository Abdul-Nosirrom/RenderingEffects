#pragma once
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


float2 PolarCoordinates(float2 uv)
{
	float2 centeredUV = uv * 2 - 1;
	float angle = atan2(centeredUV.y, centeredUV.x) / TWO_PI + 0.5; // Make it between [0, 1]
	float rad = length(centeredUV);
	return float2(angle, rad);
}

float2 SwirlUV(float2 uv, float rotation)
{
	float mid = 0.5;
	return float2(
		cos(rotation) * (uv.x - mid) + sin(rotation) * (uv.y - mid) + mid,
		cos(rotation) * (uv.y - mid) + sin(rotation) * (uv.x - mid) + mid);
}

float2 DistortionUV(float2 uv, TEXTURE2D_PARAM(tex, sampler_tex), float4 tex_ST, float strength = 1, float2 panning = 0)
{
	float2 dispUV = TRANSFORM_TEX(uv, tex) + panning;
	float2 displ = SAMPLE_TEXTURE2D(tex, sampler_tex, dispUV).rg;
	displ = (displ * 2 - 1) * strength; // Scale to [-1, 1] range
	return uv + displ;
}