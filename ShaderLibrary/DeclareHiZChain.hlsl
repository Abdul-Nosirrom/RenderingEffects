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

// Given a mip-level, whats the cell count? I.e., resolution of that mip-level
float2 HiZCellCount(int level)
{
    return _HiZResolution / exp2(level);
}

// Given a uv position and cell count, what cell indices does the pos lie in?
float2 HiZGetCellAtUV(float2 uv, float2 cellCount)
{
    return float2(floor(uv * cellCount));
}

// How much do we need to judge the current pos to 'enter' the next cells boundary in a given
// direction?
// TODO: Better understand this function, primarily with cross-step and cross-offset
float3 HiZIntersectCellBoundary(float3 rayOrigin, float3 rayDir, float2 cell, float2 cell_count, float2 crossStep, float2 crossOffset)
{
    float3 intersection = 0;
    
    float2 nextIndex = cell + crossStep;
    float2 boundary = nextIndex / cell_count;
    boundary += crossOffset; // Just sanity of 'sliiiight push' into next boundary to not lie exactly on the boundary, small epsilon
    
    float2 delta = boundary - rayOrigin.xy;
    delta /= rayDir.xy; // This gives us the 't' for our ray(depth) = o + d * t, between [o, o + rayDir * maxDistance]
    float t = min(delta.x, delta.y);
    
    intersection = rayOrigin + rayDir * t; // Our parametrized ray function
    
    return intersection;
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