#pragma once

struct VertexData
{
	float4 positionOS : POSITION;
	float2 uv : TEXCOORD0;
};

struct Varyings
{
	float4 positionCS : SV_POSITION;
	float2 texcoord : TEXCOORD0;
};

float2 GetTriangleMeshUV(float2 posCS)
{
	float2 uv = (posCS + 1) * 0.5; // Convert from [-1, 1] to [0, 1]
#ifdef UNITY_UV_STARTS_AT_TOP
	uv = uv * float2(1, -1) + float2(0, 1); // Flip Y coordinate if UV starts at the top
#endif
	return uv;
}

Varyings FSVertex(VertexData input)
{
	Varyings o;

	o.positionCS = float4(input.positionOS.xy, 0, 1);
	o.texcoord = GetTriangleMeshUV(input.positionOS.xy);

	return o;
}