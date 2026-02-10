#pragma once
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

int _HiZMipCount;
float2 _HiZResolution; // full-res dimensions, set from C#
TEXTURE2D(_HiZTexture);

#define HI_Z_MIP_COUNT _HiZMipCount
#define SAMPLE_HI_Z_DEPTH(uv, level) SampleDepthChain(uv, level)

float SampleDepthChain(float2 uv, int level)
{
    level = min(level, _HiZMipCount - 1);
    if (level == 0) return SampleSceneDepth(uv);
    return SAMPLE_TEXTURE2D_LOD(_HiZTexture, sampler_PointClamp, uv, level);
}

// Texel size of a given mip level, say level 4, incrementing UV by 
// texel size makes us step 16 pixels per iteration for example
float2 HiZMipTexelSize(int level)
{
    return exp2(level) / _HiZResolution;
}

// What mip level covers a given pixel footprint
// Useful for choosing the right starting mip for a ray step size
// If we wanna step by size of 16 pixels, we'd check what mip we need here
int MipLevelForPixelSize(float pixelSize)
{
    return clamp(int(ceil(log2(pixelSize))), 0, _HiZMipCount - 1);
}

// Convert between UV and texel coords at a given mip
float2 HiZTexelToUV(float2 texel, int level)
{
    float2 mipRes = _HiZResolution / exp2(level);
    return (texel + 0.5) / mipRes;
}

float2 HiZUVToTexel(float2 uv, int level)
{
    float2 mipRes = _HiZResolution / exp2(level);
    return floor(uv * mipRes);
}

// Linearize depth for distance comparisons during marching
float HiZLinearDepth(float rawDepth)
{
    return LinearEyeDepth(rawDepth, _ZBufferParams);
}