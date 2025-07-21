#pragma once
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


float4 TransformObjectToBillboardHClip(float4 positionOS)
{
	float3 vpos = TransformObjectToWorldDir(positionOS, false);
	float3 worldCoord = GetObjectToWorldMatrix()._m03_m13_m23;
	float3 viewPos = TransformWorldToView(worldCoord) + vpos;
	return TransformWViewToHClip(viewPos);
}
