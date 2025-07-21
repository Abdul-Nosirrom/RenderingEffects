#pragma once
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


static const float2 DOWNSAMPLE_OFFSETS[13] =
{
	float2(-1, 1), float2(0, 1), float2(1, 1),
		float2(-0.5, 0.5), float2(0.5, 0.5),
	float2(-1, 0), float2(0, 0), float2(1, 0),
		float2(-0.5, -0.5), float2(0.5, -0.5),
	float2(-1, -1), float2(0, -1), float2(1, -1),
};

/// A	.	B	.	C
/// .	D	.	E	.
/// F	.	G	.	H
/// .	I	.	J	.
/// K	.	L	.	M
float3 DownSample13Tap(Texture2D _Source, float2 uv, float2 _texelSize)
{
	float3 A = SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv + _texelSize * DOWNSAMPLE_OFFSETS[0], 0).rgb;
	float3 B = SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv + _texelSize * DOWNSAMPLE_OFFSETS[1], 0).rgb;
	float3 C = SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv + _texelSize * DOWNSAMPLE_OFFSETS[2], 0).rgb;
	float3 D = SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv + _texelSize * DOWNSAMPLE_OFFSETS[3], 0).rgb;

	float3 E = SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv + _texelSize * DOWNSAMPLE_OFFSETS[4], 0).rgb;
	float3 F = SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv + _texelSize * DOWNSAMPLE_OFFSETS[5], 0).rgb;
	float3 G = SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv + _texelSize * DOWNSAMPLE_OFFSETS[6], 0).rgb;
	float3 H = SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv + _texelSize * DOWNSAMPLE_OFFSETS[7], 0).rgb;

	float3 I = SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv + _texelSize * DOWNSAMPLE_OFFSETS[8], 0).rgb;
	float3 J = SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv + _texelSize * DOWNSAMPLE_OFFSETS[9], 0).rgb;

	float3 K = SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv + _texelSize * DOWNSAMPLE_OFFSETS[10], 0).rgb;
	float3 L = SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv + _texelSize * DOWNSAMPLE_OFFSETS[11], 0).rgb;
	float3 M = SAMPLE_TEXTURE2D_LOD(_Source, sampler_LinearClamp, uv + _texelSize * DOWNSAMPLE_OFFSETS[12], 0).rgb;

	
	float2 div = float2(0.5, 0.125); // Normalization for both individual box samples & the final result
	div /= 4;
	
	return	div.x * (D + E + I + J) +
			div.y * (A + B + F + G) +
			div.y * (F + G + K + L) +
			div.y * (B + C + G + H) +
			div.y * (G + H + L + M);
}

//---------------------------------------------------------------------

static const float UPSAMPLE_WEIGHTS[9] =
{
	1.f/16, 2.f/16, 1.f/16,
	2.f/16, 4.f/16, 2.f/16,
	1.f/16, 2.f/16, 1.f/16
};

static const float2 UPSAMPLE_OFFSETS[9] =
{
	float2(-1, 1), float2(0, 1), float2(1, 1),
	float2(-1, 0), float2(0, 0), float2(1, 0),
	float2(-1, -1), float2(0, -1), float2(1, -1)
};

float4 UpSample9Tap(Texture2D tex, float2 uv, float2 texelSize)
{
	float4 s = float4(0,0,0,1);

	[unroll]
	for (int i = 0; i < 9; i++)
	{
		float2 offset = UPSAMPLE_OFFSETS[i] * texelSize;
		float weight = UPSAMPLE_WEIGHTS[i];

		s.rgb += SAMPLE_TEXTURE2D_LOD(tex, sampler_LinearClamp , uv + offset, 0).rgb * weight;
	}

	return s;
}