Shader "Hidden/PostProcess/Bloom"
{
    SubShader
    {
        Tags {"LightMode" = "Always" "RenderType"="Opaque" "PerformanceChecks"="False"}

		Cull Off ZWrite Off ZTest Always
        
        HLSLINCLUDE

        #define MK_RENDER_PIPELINE_UNIVERSAL
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "../../ShaderLibrary/FullScreenVertex.hlsl"
        #include "../../ShaderLibrary/Filtering.hlsl"

        TEXTURE2D(_SelectiveBloomSource);
        SAMPLER(sampler_SelectiveBloomSource);
        
        TEXTURE2D(_BloomSource);
        SAMPLER(sampler_BloomSource);
        float4 _BloomSource_TexelSize;

        TEXTURE2D(_HigherMipSource);
        SAMPLER(sampler_HigherMipSource);
        float4 _HigherMipSource_TexelSize;
        
        float _BloomThreshold;
        float _BloomSpread;
        float _BloomIntensity;
        

        #pragma vertex FSVertex
        #pragma target 3.5
        
	    #define EPSILON 1.0e-4

        inline half3 BloomThreshold(half3 c, half2 threshold)
	    {		
		    //brightness is defined by the relative luminance combined with the brightest color part to make it nicer to deal with the shader for artists
		    //based on unity builtin brightpass thresholding
		    //if any color part exceeds a value of 10 (builtin HDR max) then clamp it as a normalized vector to keep the color balance
		    c = clamp(c, 0, normalize(c) * threshold.y);
		    c *= 0.909;
		    //picking just the brightest color part isn´t physically correct at all, but gives nices artistic results
		    half brightness = max(c.r, max(c.g, c.b));
		    //forcing a hard threshold to only extract really bright parts
		    half sP = EPSILON;;
		    return max(0, c * max(pow(clamp(brightness - threshold.x + sP, 0, 2 * sP), 2) / (4 * sP + EPSILON), brightness - threshold.x) / max(brightness, EPSILON));
	    }

        float3 Prefilter(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord.xy;
            float3 col = SAMPLE_TEXTURE2D_LOD(_BloomSource, sampler_BloomSource, uv, 0);

            col = BloomThreshold(col, half2(_BloomThreshold, 160));
            col += SAMPLE_TEXTURE2D_LOD(_SelectiveBloomSource, sampler_SelectiveBloomSource, uv, 0);
            
            return col;
        }

        float3 DownSample(Varyings input) : SV_Target
        {
            //return DownsampleLQ(_BloomSource, sampler_BloomSource, input.texcoord, _BloomSource_TexelSize.xy);
            return DownSample13Tap(_BloomSource, input.texcoord, _BloomSource_TexelSize.xy);
        }

        float3 UpSample(Varyings input) : SV_Target
        {
            //float3 bloom = UpsampleLQ(_BloomSource, sampler_BloomSource, input.texcoord, _BloomSource_TexelSize.xy * _BloomSpread);
            float3 bloom = UpSample9Tap(_BloomSource, input.texcoord, _BloomSource_TexelSize.xy * _BloomSpread);
            bloom += SAMPLE_TEXTURE2D_LOD(_HigherMipSource, sampler_HigherMipSource, input.texcoord, 0);
            return bloom;
        }
        
        ENDHLSL
        
        Pass
        {
            Name "Prefilter"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma fragment Prefilter
            ENDHLSL
        }

        Pass
        {
            Name "DownSample"
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma fragment DownSample
            ENDHLSL
        }

        Pass
        {
            Name "UpSample"
            //Blend one one
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma fragment UpSample
            ENDHLSL
        }

        Pass
        {
            Name "UpSampleFinal"
            //Blend one one
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma fragment UpSampleF
            float3 UpSampleF(Varyings input) : SV_Target
            {
                float3 bloomResult = SAMPLE_TEXTURE2D_LOD(_BloomSource, sampler_BloomSource, input.texcoord, 0);
                float3 selectiveBloomSrc = SAMPLE_TEXTURE2D_LOD(_SelectiveBloomSource, sampler_SelectiveBloomSource, input.texcoord, 0);
                //bloomResult = lerp(bloomResult, 0.1f, saturate(selectiveBloomSrc));
                //return bloomResult;
                float mulpFactor = saturate(max(selectiveBloomSrc.x, max(selectiveBloomSrc.y, selectiveBloomSrc.z)));
                return bloomResult;//mulpFactor <= 0 ? bloomResult : bloomResult * max(0.2, (1-mulpFactor));
                return max(0, bloomResult - 10 * selectiveBloomSrc);
                //float4 bloom = UpSampleTent9Tap(_BloomSource, input.texcoord, _BloomSource_TexelSize);
                float3 bloom = SAMPLE_TEXTURE2D_LOD(_BloomSource, sampler_BloomSource, input.texcoord, 0);//UpsampleLQ(_BloomSource, sampler_LinearClamp, input.texcoord, _BloomSource_TexelSize.xy * _BloomSpread);
                return bloom;
            }
            ENDHLSL
        }
    }
}