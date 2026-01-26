Shader "Hidden/PostProcess/SSR"
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
            Name "SSRPass"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Assets/_Project/Shaders/Library/Random.hlsl"
            #include "Assets/_Project/Shaders/Library/Noise/Noise3D.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float _FrameCount;
            TEXTURE2D(_SSRPrev); SAMPLER(sampler_SSRPrev);
            
            void GetRayMarchStartParams(float2 screenUV, out float3 r0, out float3 rd)
            {
                float depth = SampleSceneDepth(screenUV);
                // Unitys lying, says expects NDC but actually means UV [0,1] not [-1, 1]
                r0 = ComputeViewSpacePosition(screenUV, depth, UNITY_MATRIX_I_P);
                float3 viewDirVS = normalize(r0);
                float3 normalVS = normalize(TransformWorldToViewNormal(SampleSceneNormals(screenUV)));
                normalVS.z *= -1;
                rd = reflect(viewDirVS, normalVS);
            }

            float4 ViewPosToFragPos(float3 viewPos)
            {
                viewPos.z *= -1; // Correct this
                //return TransformWViewToHClip(viewPos);
                float4 clipPos = mul(UNITY_MATRIX_P, viewPos);
                clipPos.xyz / clipPos.w;
                clipPos.xy = clipPos.xy * 0.5f + 0.5f;
                return clipPos;
            }

            float2 ViewSpacePosToUVs(float3 viewSpacePos)
            {
                // Undo the z-flip from ComputeViewSpacePosition
                viewSpacePos.z = -viewSpacePos.z;
                
                float4 posCS = mul(UNITY_MATRIX_P, float4(viewSpacePos, 1.0));
                
            #if UNITY_UV_STARTS_AT_TOP
                posCS.y = -posCS.y;
            #endif
                
                float2 posNDC = posCS.xy / posCS.w;  // -1 to 1
                return posNDC * 0.5 + 0.5;            // 0 to 1
            }

            float Vignette(float2 uv)
            {
                float2 k = abs(uv - 0.5) * 1;
                k.x *= _BlitTexture_TexelSize.y * _BlitTexture_TexelSize.z;
                return pow(saturate(1.0 - dot(k, k)), 1);
            }

            #define _MaxDistance 100
            #define _Thickness 0.5

            // Out frag function takes as input a struct that contains the screen space coordinate we are going to use to sample our texture. It also writes to SV_Target0, this has to match the index set in the UseTextureFragment(sourceTexture, 0, …) we defined in our render pass script.   
            float3 Frag(Varyings input) : SV_Target0
            {
                float steps = 5;
                float2 uv = 0;

                float3 positionFrom = ComputeViewSpacePosition(input.texcoord, SampleSceneDepth(input.texcoord), UNITY_MATRIX_I_P);
                float3 mask = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);

                if (mask.r <= 0) return SampleSceneColor(input.texcoord);

                float3 viewDir = normalize(positionFrom);
                float3 normal = TransformWorldToViewNormal(SampleSceneNormals(input.texcoord), true);
                normal.z *= -1;
                float3 reflection = normalize(reflect(viewDir, normal));

                // Jitter reflection direction
                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, SampleSceneDepth(input.texcoord), UNITY_MATRIX_I_VP);
                float3 noise = RandomVector3(worldPos) * RandomFloat(worldPos.y) * 0.02;
                reflection = normalize(noise + reflection);

                float3 positionTo = positionFrom;

                float4 startView = float4(positionFrom + reflection * 0, 1);
                float4 endView = float4(positionFrom + reflection * _MaxDistance, 1);

                float2 startFrag = ViewSpacePosToUVs(startView) * _ScreenSize;
                float2 endFrag = ViewSpacePosToUVs(endView) * _ScreenSize;

                float2 frag = startFrag.xy;
                uv = frag / _ScreenSize;
                
                float2 deltaT = endFrag.xy - startFrag.xy;
                float useX = abs(deltaT.x) >= abs(deltaT.y) ? 1 : 0;
                float delta = lerp(abs(deltaT.y), abs(deltaT.x), useX) * 0.3f;;
                delta = min(delta, 64);
                float2 increment = float2(deltaT.x, deltaT.y) / max(delta, 0.001f);

                float search0 = 0;
                float search1 = 0;

                int hit0 = 0;
                int hit1 = 0;

                float viewDistance = startView.z;
                float depth = _Thickness;

                float i = 0;

                [loop]
                for (i = 0; i < int(delta); ++i)
                {
                    frag += increment;
                    uv = frag / _ScreenSize;
                    positionTo = ComputeViewSpacePosition(uv, SampleSceneDepth(uv), UNITY_MATRIX_I_P);

                    search1 =
                      lerp
                        ( (frag.y - startFrag.y) / deltaT.y
                        , (frag.x - startFrag.x) / deltaT.x
                        , useX
                        );

                    search1 = clamp(search1, 0.0, 1.0);

                    viewDistance = (startView.z * endView.z) / lerp(endView.z, startView.z, search1);
                    depth        = viewDistance - positionTo.z;

                    if (depth > 0 && depth < _Thickness)
                    {
                        hit0 = 1;
                        break;
                    }
                    
                    search0 = search1;
                }

                search1 = search0 + ((search1 - search0)/2.f);
                steps *= hit0;

                [loop]
                for (i = 0; i < steps; ++i)
                {
                    //frag += increment;
                    frag = lerp(startFrag.xy, endFrag.xy, search1);
                    uv = frag / _ScreenSize;
                    positionTo = ComputeViewSpacePosition(uv, SampleSceneDepth(uv), UNITY_MATRIX_I_P);

                    viewDistance = (startView.z * endView.z) / lerp(endView.z, startView.z, search1);
                    depth        = viewDistance - positionTo.z;

                    if (depth > 0 && depth < _Thickness)
                    {
                        hit1 = 1;
                        search1 = search0 + ((search1 - search0) / 2);
                    }
                    else
                    {
                        float temp = search1;
                        search1 = search1 + ((search1 - search0) / 2);
                        search0 = temp;
                    }
                }

                float2 reflectionUV = uv;

                // Visibility masks
                float visibility = 1;
                visibility *= saturate(dot(reflection, normalize(startView))); // pointing bad
                visibility *= (1 - saturate(length(startView)/_MaxDistance)); // fade out
                visibility *= Vignette(input.texcoord); // nice to have

                //return float4(reflectionUV.rg, 0, 1);
                // Any hits if are reflection uvs are in the [0,1] range (also catches boundary breaks)
                bool anyHits = all(reflectionUV >= 0 && reflectionUV <= 1);
                
                float4 result = float4(SampleSceneColor(input.texcoord), 0);
                //result.rgb = propColor;
                //return result;
                if (anyHits)
                {
                    result.rgb += 0.25f * visibility * SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, reflectionUV);
                    result.a = 1;
                }

                //result.a = all(reflectionUV >= 0) ? 1 : 0;
                return result;
            }
            ENDHLSL
        }
        Pass
        {
            Name "SSR Composite"
            
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D(_SSR); SAMPLER(sampler_SSR);

            float3 Frag(Varyings input) : SV_TARGET
            {
                // Sample
                float4 ssrSample = SAMPLE_TEXTURE2D(_SSR, sampler_SSR, input.texcoord);
                return ssrSample.rgb;
                float viz = ssrSample.a;
                float3 sceneColor = SampleSceneColor(input.texcoord);
                return sceneColor + 0.25f * viz * ssrSample.rgb;
            }
            ENDHLSL
        }
    }
}