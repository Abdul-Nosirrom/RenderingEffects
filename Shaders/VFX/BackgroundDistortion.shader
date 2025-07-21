Shader "VFX/BackgroundDistortion"
{
    Properties
    {
        [MainTexture] _MaskTex ("Mask", 2D) = "white" {}
        [Toggle(_POLAR_COORDS)] _PolarCoordinates("Polar Coordinates", Float) = 0
        
        [Space(10)]
        
        [Normal] _DistortionGuide("Distortion Guide", 2D) = "bump" {}
        _DistortionAmount("Distortion Amount", Range(0, 10)) = 0
        
        [Space(10)]
        
        [Toggle(_CHROMATIC_ABERRATION)] _ChromaticAberration("Chromatic Aberration", Float) = 0
        [ShowIf(_CHROMATIC_ABERRATION)] _ChromaticAberrationStrength("Chromatic Aberration Strength", Range(0, 10)) = 0
        
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100
        Cull Off
        ZTest LEqual
        
        Pass
        {
            Name "BackgroundDistortion"
            Tags
            {
                "LightMode"="Distortion"
            }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment _POLAR_COORDS
            #pragma shader_feature_local_fragment _CHROMATIC_ABERRATION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../../ShaderLibrary/UVFunctions.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            TEXTURE2D(_DistortionGuide);
            SAMPLER(sampler_DistortionGuide);

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTex_ST;
                float4 _DistortionGuide_ST;

                float _DistortionAmount;
                float _ChromaticAberrationStrength;
            CBUFFER_END

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            // RG is distortion, B is chomatic abberation amount
            float3 frag(v2f i) : SV_Target
            {
                float2 texUV = i.uv;
#ifdef _POLAR_COORDS
                texUV = PolarCoordinates(texUV);
#endif
                float2 maskUV = TRANSFORM_TEX(texUV, _MaskTex);
                float2 distortionUV = TRANSFORM_TEX(texUV, _DistortionGuide);// + float2(0,0.1) * _Time.y;

                float maskVal = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV);
                float2 distortionVec = UnpackNormal(SAMPLE_TEXTURE2D(_DistortionGuide, sampler_DistortionGuide, distortionUV)).xy; // Keep in [0,1]
                distortionVec *= _DistortionAmount * maskVal;
                
                // sample the texture
                float3 col = float3(distortionVec.xy /* i.screenPos.z*/, 0); // Scale by depth
#ifdef _CHROMATIC_ABERRATION
                col.b = _ChromaticAberrationStrength;
#endif                
                return col;
            }
            ENDHLSL
        }
    }
}