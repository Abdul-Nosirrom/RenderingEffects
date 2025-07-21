#pragma once
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Math.hlsl"

float2 FlipBookUVs(float2 uv, float2 dimensions, int tile)
{
	// Clamp the UVs to the bounds of the current frame
	uv = clamp(uv, 0.01, 0.99);
	
	tile = tile % (dimensions.x * dimensions.y);
	int xF = tile % dimensions.x;
	// Flip y as often flipbooks start at the top left not bottom left (uvs start at bottom left)
	int yF = (dimensions.y - 1) - floor(tile / dimensions.x);// - dimensions.y;//, 0, dimensions.y - 1);
	float2 frameSize = 1/dimensions; // essentially 'texel size' for frames in the flipbook to normalize the UVs to a frame bound
	uv = (uv + float2(xF, yF)) * frameSize;
	return uv;
}

int FlipBookPctToTileVal(float pct, float2 dimensions)
{
	return floor(pct * dimensions.x * dimensions.y);
}

float2 FlipBookUVs(float2 uv, float2 dimensions, float pct)
{
	pct = frac(pct);
	int currentTile = FlipBookPctToTileVal(pct, dimensions);
	return FlipBookUVs(uv, dimensions, currentTile);
}

float4 SampleFlipBook(TEXTURE2D_PARAM(tex, sampler_tex), float2 uv, float2 dimensions, float pct, bool doFrameBlending)
{
	int currentTile = FlipBookPctToTileVal(pct, dimensions);
	float2 currentUV = FlipBookUVs(uv, dimensions, currentTile);

	float4 flipBookSample = SAMPLE_TEXTURE2D(tex, sampler_tex, currentUV);
	
	int numFrames = dimensions.x * dimensions.y;
	if (doFrameBlending && currentTile != numFrames - 1)
	{
		int nextTile = currentTile + 1;
		float2 nextUV = FlipBookUVs(uv, dimensions, nextTile);
		float4 nextFrame = SAMPLE_TEXTURE2D(tex, sampler_tex, nextUV);

		// Blend pct between current tile & next tile
		float blendAlpha = frac(pct * dimensions.x * dimensions.y);
		flipBookSample = lerp(flipBookSample, nextFrame, blendAlpha);
	}

	return flipBookSample;
}

