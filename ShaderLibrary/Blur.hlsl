#pragma once
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScalingClamping.hlsl"

//--------------------------------------------------------------------------------------------------------------------
//	GAUSSIAN BLUR
//--------------------------------------------------------------------------------------------------------------------

static const float k_gaussianWeights_3[3] = { 0.25, 0.5, 0.25 };
static const float k_gaussianWeights_9[9] = { 0.01621622, 0.05405405, 0.12162162, 0.19459459, 0.22702703, 0.19459459, 0.12162162, 0.05405405, 0.01621622 };
static const float k_gaussianWeights_9_bilinear[5] = { 0.07027027, 0.31621622, 0.22702703, 0.31621622, 0.07027027 };

static const float k_gaussianOffsets_3[3] = { -1, 0, 1 };
static const float k_gaussianOffsets_9[9] = { -4, -3, -2, -1, 0, 1, 2, 3, 4 };
static const float k_gaussianOffsets_9_bilinear[5] = { -3.23076923, -1.38461538, 0, 1.38461538, 3.23076923 };

#define BLUR_AXIS(tex, samp, uv, axis, weights, offsets, count) \
{ \
	float4 result = 0; \
	[unroll] \
	for (int _i = 0; _i < count; _i++) \
	{ \
		result += SAMPLE_TEXTURE2D_LOD(tex, samp, ClampUVForBilinear(uv + axis * offsets[_i]), 0) * weights[_i]; \
	} \
	return result; \
}

#define BLUR_COMBINED(tex, samp, uv, texelSize, weights, offsets, count) \
{ \
	float4 result = 0; \
	[unroll] \
	for (int _y = 0; _y < count; _y++) \
	{ \
		[unroll] \
		for (int _x = 0; _x < count; _x++) \
		{ \
			float2 offset = float2(offsets[_x], offsets[_y]); \
			float weight = weights[_x] * weights[_y]; \
			result += SAMPLE_TEXTURE2D_LOD(tex, samp, ClampUVForBilinear(uv + offset * texelSize), 0) * weight; \
		} \
	} \
	return result; \
}

float4 Blur_GaussianH_3(TEXTURE2D_PARAM(_Source, sampler_Source), float2 uv, float2 texelSize)
BLUR_AXIS(_Source, sampler_Source, uv, float2(texelSize.x, 0), k_gaussianWeights_3, k_gaussianOffsets_3, 3)

float4 Blur_GaussianV_3(TEXTURE2D_PARAM(_Source, sampler_Source), float2 uv, float2 texelSize)
BLUR_AXIS(_Source, sampler_Source, uv, float2(0, texelSize.y), k_gaussianWeights_3, k_gaussianOffsets_3, 3)

// weights at each point = weight[x] * weight[y]
float4 Blur_Gaussian_3(TEXTURE2D_PARAM(_Source, sampler_Source), float2 uv, float2 texelSize)
BLUR_COMBINED(_Source, sampler_Source, uv, texelSize, k_gaussianWeights_3, k_gaussianOffsets_3, 3)

float4 Blur_GaussianH_9(TEXTURE2D_PARAM(_Source, sampler_Source), float2 uv, float2 texelSize)
BLUR_AXIS(_Source, sampler_Source, uv, float2(texelSize.x, 0), k_gaussianWeights_9, k_gaussianOffsets_9, 9)

float4 Blur_GaussianV_9(TEXTURE2D_PARAM(_Source, sampler_Source), float2 uv, float2 texelSize)
BLUR_AXIS(_Source, sampler_Source, uv, float2(0, texelSize.y), k_gaussianWeights_9, k_gaussianOffsets_9, 9)

float4 Blur_Gaussian_9(TEXTURE2D_PARAM(_Source, sampler_Source), float2 uv, float2 texelSize)
BLUR_COMBINED(_Source, sampler_Source, uv, texelSize, k_gaussianWeights_9, k_gaussianOffsets_9, 9)

// 5-tap 9 texel gaussian blur with bilinear sampling [Horizontal]
float4 Blur_GaussianH_9_Bilinear(TEXTURE2D_PARAM(_Source, sampler_Source), float2 uv, float2 texelSize)
BLUR_AXIS(_Source, sampler_Source, uv, float2(texelSize.x, 0), k_gaussianWeights_9_bilinear, k_gaussianOffsets_9_bilinear, 5)

// 5-tap 9 texel gaussian blur with bilinear sampling [Vertical]
float4 Blur_GaussianV_9_Bilinear(TEXTURE2D_PARAM(_Source, sampler_Source), float2 uv, float2 texelSize)
BLUR_AXIS(_Source, sampler_Source, uv, float2(0, texelSize.y), k_gaussianWeights_9_bilinear, k_gaussianOffsets_9_bilinear, 5)

// 5-tap 9 texel gaussian blur with bilinear sampling [Combined]
float4 Blur_Gaussian_9_Bilinear(TEXTURE2D_PARAM(_Source, sampler_Source), float2 uv, float2 texelSize)
BLUR_COMBINED(_Source, sampler_Source, uv, texelSize, k_gaussianWeights_9_bilinear, k_gaussianOffsets_9_bilinear, 5)


//--------------------------------------------------------------------------------------------------------------------