Shader "Hidden/PostProcessing/FinalComposite"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"
        }
        ZWrite Off Cull Off
        Pass
        {
            Name "CompositePass"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "../../ShaderLibrary/Tonemapping.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D(_BloomTexture);
            float4 _BloomTexture_TexelSize;

            float _BloomIntensity;
            float _BloomSpread;

            // Out frag function takes as input a struct that contains the screen space coordinate we are going to use to sample our texture. It also writes to SV_Target0, this has to match the index set in the UseTextureFragment(sourceTexture, 0, …) we defined in our render pass script.   
            float4 Frag(Varyings input) : SV_Target0
            {
                // sample the current camera texture
                float2 uv = input.texcoord.xy;
                
                float3 color = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;

                // Composite bloom
                float3 bloomColor = SAMPLE_TEXTURE2D_LOD(_BloomTexture, sampler_LinearClamp, uv, 0);
                color += bloomColor * _BloomIntensity;
                //float3 bloomColor = SampleTexture2DBicubic(_BloomTexture, sampler_LinearClamp, uv, _BloomTexture_TexelSize.zwxy, (1.0).xx, unity_StereoEyeIndex).rgb;
                //color += bloomColor;

                // Apply tonemapping
                //color = Tonemap_GT(color);

                // Final debug display override
                // #if defined(DEBUG_DISPLAY)
                // half4 debugColor = 0;
                //
                // if(CanDebugOverrideOutputColor(half4(color, 1), uv, debugColor))
                // {
                //     return debugColor;
                // }
                // #endif
                
                return float4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Distortion Apply"
            
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D(_DistortionTexture);
            float4 _DistortionTexture_TexelSize;

            void ApplyDistortion(float2 uv, out float2 distortion, out float chromaticAberration)
            {
                float3 res = SAMPLE_TEXTURE2D(_DistortionTexture, sampler_LinearClamp, uv).rgb;
                distortion = res.xy;
                chromaticAberration = res.z;
            }

            // Out frag function takes as input a struct that contains the screen space coordinate we are going to use to sample our texture. It also writes to SV_Target0, this has to match the index set in the UseTextureFragment(sourceTexture, 0, …) we defined in our render pass script.   
            float4 Frag(Varyings input) : SV_Target0
            {
                // sample the current camera texture
                float2 uv = input.texcoord.xy;
                float chromaticStrength;
                float2 distortion;
                ApplyDistortion(uv, distortion, chromaticStrength);
                
                float3 color;
                color.r = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv + distortion * (1 + chromaticStrength * 0.1), 0).r;
                color.g = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv + distortion, 0).g;
                color.b = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearClamp, uv + distortion * (1 - chromaticStrength * 0.1), 0).b;
                
                return float4(color, 1);
            }
            ENDHLSL
        }
    }
}