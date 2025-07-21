#pragma once
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "../../ShaderLibrary/Math.hlsl"

// ---------- Primary Textures

TEXTURE2D(_BaseTex);
SAMPLER(sampler_BaseTex);

float4 _BaseTex_ST;
float4 _BaseTex_Panning;
float _BaseClampU;
float _BaseClampV;

#define Base_ClampU (_BaseClampU > 0)
#define Base_ClampV (_BaseClampV > 0)

#ifdef _USE_SECONDARY_TEXTURE
TEXTURE2D(_SecondaryTex);
SAMPLER(sampler_SecondaryTex);

float4 _SecondaryTex_ST;
float4 _SecondaryTex_Panning;

float _SecondaryClampU;
float _SecondaryClampV;
#define Secondary_ClampU (_SecondaryClampU > 0)
#define Secondary_ClampV (_SecondaryClampV > 0)
#endif


// ---------- Color

float _ColorCutoff;

#ifdef _USE_GRADIENT_MAP
TEXTURE2D(_GradientMap);
SAMPLER(sampler_GradientMap);
#else
float4 _HighColor;
float4 _LowColor;
#endif
float4 _BackFaceTint;

float _Contrast;
float _ColorPower;

#ifdef _COLOR_BANDING
int _NumColorBands;
#endif

// ---------- Alpha Controls

float _Intensity;
float _AlphaInfluence;
float _AlphaCutoff;
float _AlphaCutoffSmoothness;

// ---------- Emission

float _GlowIntensity;
float4 _GlowColor;

// ---------- Erosion Burn

#ifdef _EROSION_BURN
float4 _BurnColor;
float _BurnSize;
float _BurnSmoothness;
#endif

// ---------- Mask

#ifdef _USE_MASK_TEXTURE
TEXTURE2D(_MaskTex);
SAMPLER(sampler_MaskTex);

float4 _MaskTex_ST;
#endif

// ---------- Distortion

#ifdef _USE_DISTORTION_TEXTURE
TEXTURE2D(_DistortionTex);
SAMPLER(sampler_DistortionTex);

float _DistortionPow;
float4 _DistortionTex_ST;
float4 _DistortionPanning;
#endif

// ---------- Depth Fadeout

#ifdef _DEPTH_FADEOUT
float _DepthFadeoutDistance;
#endif 

struct vertexData
{
	float4 vertex : POSITION;
	float2 uv : TEXCOORD0;
};

struct interpolators
{
	float4 vertex : SV_POSITION;

	float2 uv : TEXCOORD0;

#ifdef _DEPTH_FADEOUT	
	float4 screenPos : TEXCOORD1; // Screen position for depth calculations
#endif	
};

struct pixelOut
{
	float4 color : SV_Target0;
	float4 glow : SV_Target1; // Glow color for emission
};

// Color blend shifts based on alpha cutoff, basically remapping the range from [0, 1] to [_AlphaCutoff, _ColorCutoff].
float GetColorBlend(float grayscaleValue)
{
	float cutoff = lerp(_AlphaCutoff, 2, _ColorCutoff);
	return smoothstep(_AlphaCutoff, cutoff, grayscaleValue);	
}

float DepthSoftBlendFactor(float4 screenPos, float depthIntersectionThreshold)
{
	float depth = LinearEyeDepth(SampleSceneDepth(screenPos.xy), _ZBufferParams);
	return saturate(depthIntersectionThreshold * (depth - screenPos.w));
}

float4 TransformObjectToWorldBillboard(float4 positionOS)
{
	float3 vpos = TransformObjectToWorldDir(positionOS, false);
	float3 worldCoord = GetObjectToWorldMatrix()._m03_m13_m23;
	float3 viewPos = TransformWorldToView(worldCoord) + vpos;
	return TransformWViewToHClip(viewPos);
}

float4 SampleTexture(TEXTURE2D_PARAM(tex, sampler_tex), float4 scaleAndOffset, float2 uv, float2 panning, float2 distortion, bool clampU, bool clampV)
{
	// If polar, convert to polar coordinates
#ifdef _POLAR_COORDS	
	uv = PolarCoordinates(uv);
#endif
	
	// Apply scale & offset
	uv = uv * scaleAndOffset.xy + scaleAndOffset.zw;

	// Apply distortion (if any)
	uv += distortion;

	// Apply panning (if any)
	uv += panning * _Time.y;

	// -1 to 1 instead of 0 to 1 to allow for bidirectionality & inversion
	if (clampU) uv.x = clamp(uv.x, -1, 1);
	if (clampV) uv.y = clamp(uv.y, -1, 1);

	// Sample the texture
	float4 col = SAMPLE_TEXTURE2D(tex, sampler_tex, uv); // usage of dx/dy for polar coordinate sampling needed to avoid artifacts
	col = lerp(0.5, col, _Contrast);
	col = pow(col, _ColorPower);
	return col;
}

float3 ErosionBurn(float alpha, float alphaCutoff, float burnSmoothness, float burnSize, float3 burnColor)
{
	float minAlpha = lerp(alphaCutoff, alphaCutoff + burnSize, saturate(1 - burnSmoothness));
	return (1 - smoothstep(minAlpha, alphaCutoff + burnSize, alpha)) * burnColor;
}

interpolators FXVertex(vertexData v)
{
	interpolators i;

#ifdef _BILLBOARD
	i.vertex = TransformObjectToWorldBillboard(v.vertex);
#else	
	i.vertex = TransformObjectToHClip(v.vertex);
#endif	

	i.uv = v.uv;

#ifdef _DEPTH_FADEOUT	
	i.screenPos = ComputeScreenPos(i.vertex);
#endif

	return i;
}

pixelOut FX_Main(interpolators i, bool isFrontFace : SV_IsFrontFace) : SV_Target
{
	pixelOut o;
	
	// usually w/ distortion its not so much that the distortion texture is being panned, but just the main texture moves through a 'river' for example
	float2 distortion = 0;
#ifdef _USE_DISTORTION_TEXTURE
	float2 distUV = i.uv;
	#ifdef _DISTORTION_USES_POLAR_COORDS
		distUV = PolarCoordinates(distUV);
	#endif	
	distUV = TRANSFORM_TEX(i.uv, _DistortionTex) + _DistortionPanning.xy * _Time.y;
	distortion = SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, distUV).xy;
	// remap distortion to [-1, 1] range
	distortion = (distortion * 2 - 1) * _DistortionPow;
#endif	

	float4 col = SampleTexture(_BaseTex, sampler_BaseTex, _BaseTex_ST, i.uv, _BaseTex_Panning.xy, distortion, Base_ClampU, Base_ClampV);
#ifdef _USE_SECONDARY_TEXTURE
	float4 secondaryCol = SampleTexture(_SecondaryTex, sampler_SecondaryTex, _SecondaryTex_ST, i.uv, _SecondaryTex_Panning.xy, distortion, Secondary_ClampU, Secondary_ClampV);
	// Based on "Technical Artist Bootcamp: The VFX Of Diablo)
	col = col * secondaryCol * 2;
	col.a = saturate(col.a);
#endif

	// If your texture has no alpha, unity can generate it from the grayscale values
	float alpha = col.a;
	
	// Posterize RGB
#ifdef _COLOR_BANDING
	col.rgb = round(col.rgb * _NumColorBands) / _NumColorBands;
#endif	
	
	if (alpha <= _AlphaCutoff) discard;

	//alpha = smoothstep(_AlphaCutoff, 2*_AlphaCutoffSmoothness + _AlphaCutoff, alpha);

	// AlphaInflunce = 0 means Alpha = 1, alpha influence > 1 means alpha goes to 0
	if (_AlphaInfluence <= 1) alpha = lerp(1.0, alpha, _AlphaInfluence);
	else alpha = lerp(alpha, 0, min(1, _AlphaInfluence - 1));
	
	//float ogAlpha = alpha;
	//alpha = lerp(1.0, alpha, _AlphaInfluence);
	//alpha = saturate(pow(alpha - 0.01, _AlphaInfluence));
	//alpha = 1 * (1 - saturate(_AlphaInfluence)) + alpha * _AlphaInfluence; 
	//o.color = alpha * 2;
	//return o;

	float colorBlend = GetColorBlend(col.r);
#ifdef _USE_GRADIENT_MAP
	col.rgb = SAMPLE_TEXTURE2D(_GradientMap, sampler_GradientMap, colorBlend).rgb;
#else
	col = lerp(_LowColor, _HighColor, colorBlend);
#endif

	col *= alpha;
	col *= _Intensity;
	col.a = saturate(col.a);
	
#ifdef _USE_MASK_TEXTURE
	float2 maskUV = TRANSFORM_TEX(i.uv, _MaskTex);
	float maskAlpha = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV).a;
	col *= maskAlpha;
#endif	
	
#ifdef _DEPTH_FADEOUT
	i.screenPos.xy /= i.screenPos.w;
	float depth = LinearEyeDepth(SampleSceneDepth(i.screenPos.xy), _ZBufferParams);
	float depthDelta = saturate(_DepthFadeoutDistance * (depth - i.screenPos.w));
	col *= depthDelta;
#endif
	
#ifdef _EROSION_BURN	
	// Erosion burn effect
	col.rgb += ErosionBurn(ogAlpha, _AlphaCutoff, _BurnSmoothness, _BurnSize, _BurnColor.rgb);
#endif	
	
	if (!isFrontFace)
		col.rgb *= _BackFaceTint;

	o.color = col;
	o.glow = float4(_GlowColor.rgb * _GlowIntensity * col.a, col.a);

	return o;
}