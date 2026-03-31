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
            #include "../../ShaderLibrary/DeclareHiZChain.hlsl"
            
            #pragma vertex Vert
            #pragma fragment Frag
            
            #define MAX_ITERATION 128
            #define STEP_SIZE 2
            #define MAX_THICKNESS 0.00001
            
            float Vignette(float2 uv)
            {
                float2 k = abs(uv - 0.5) * 1;
                k.x *= _BlitTexture_TexelSize.y * _BlitTexture_TexelSize.z;
                return pow(saturate(1.0 - dot(k, k)), 1);
            }
            
            float GetVisibilityMask(float2 uv, float3 r0, float3 rd)
            {
                // Visibility masks
                float visibility = 1;
                visibility *= saturate(dot(rd, normalize(r0))); // pointing bad
                //visibility *= (1 - saturate(length(ssOrigin)/_MaxDistance)); // fade out
                visibility *= Vignette(uv); // nice to have
                return visibility;
            }
            
            float Linear01ToEyeDepth(float linear01)
            {
                return linear01 * _ProjectionParams.z; // _ProjectionParams.z = far plane
            }
            
            void GetRayMarchStartParams(float2 uv, out float3 rayOrigin, out float3 rayDir, out float maxTraceDistance)
            {
                // Get ray marching parameters in screen space
                float3 startWS = ComputeWorldSpacePosition(uv, SampleSceneDepth(uv), UNITY_MATRIX_I_VP);
                float3 normalWS = SampleSceneNormals(uv);
                float3 viewDirWS = normalize(startWS - GetCameraPositionWS());
                float3 reflectDirWS = normalize(reflect(viewDirWS, normalWS)); // view reflects off normal
                float3 endWS = startWS + reflectDirWS * 0.1f;
                
                // Now we've setup our raymarching parameters in WS, we convert to screen space
                float4 startCS = TransformWorldToHClip(startWS);
                startCS.xyz /= startCS.w; // Perspective divide
                float2 startUV = (startCS.xy) * 0.5f + 0.5f;

                float4 endCS = TransformWorldToHClip(endWS);
                endCS.xyz /= endCS.w; // Perspective divide
                float2 endUV = (endCS.xy) * 0.5f + 0.5f;

                #if UNITY_UV_STARTS_AT_TOP
                    startUV.y = 1.0 - startUV.y;
                    endUV.y = 1.0 - endUV.y;
                #endif
                
                // Now we've got our 2 points in screen space, we can get the ray direction in screen space
                rayOrigin = float3(startUV, startCS.z); // Raw depth for z
                rayDir = normalize(endCS.xyz - startCS.xyz);
                
                #if UNITY_UV_STARTS_AT_TOP
                    rayDir.xy *= float2(0.5f, -0.5f);
                #else 
                    rayDir.xy *= 0.5f;
                #endif
                
                // Compute maximum distance to trace before the ray goes outside visible area - via ray-AABB intersection test
                maxTraceDistance = rayDir.x >= 0 ? (1 - rayOrigin.x) / rayDir.x : -rayOrigin.x / rayDir.x; // x
                maxTraceDistance = min(maxTraceDistance, rayDir.y < 0 ? (-rayOrigin.y / rayDir.y) : (1 - rayOrigin.y) / rayDir.y); // y
                maxTraceDistance = min(maxTraceDistance, rayDir.z < 0 ? (-rayOrigin.z / rayDir.z) : (1 - rayOrigin.z) / rayDir.z); // z
            }
            
            bool FindIntersection_Linear(float3 rayOrigin, float3 rayDir, float maxTraceDist, out float3 hitInfo)
            {
                float3 endPos = rayOrigin + rayDir * maxTraceDist;
                float3 stepSize = endPos - rayOrigin;
                int2 startScreenIdx = int2(rayOrigin.xy * _BlitTexture_TexelSize.zw);
                int2 endScreenIdx = int2(endPos.xy * _BlitTexture_TexelSize.zw);
                int2 endScreenDelta = endScreenIdx - startScreenIdx;
                int maxScreenDist = max(abs(endScreenDelta.x), abs(endScreenDelta.y));
                stepSize /= maxScreenDist; // Kind of like hitting it w/ texel size but w.r.t the max distance range
                
                // Prep for starting up the raymarching
                float3 rayPos = rayOrigin + stepSize; // start a bit away from the pixel we're assigned to, avoid self-intersection
                float3 rayStep = stepSize; // Same dir but our magnitudes been squeezed or expanded accordingly
                
                int hitIdx = -1;
                
                UNITY_LOOP
                for (int t = 0; t < maxScreenDist && t < MAX_ITERATION; t++)
                {
                    float depth = SampleSceneDepth(rayPos.xy);
                    
                    #if UNITY_REVERSED_Z
                        float thickness = depth - rayPos.z;
                    #else
                        float thickness = rayPos.z - depth;
                    #endif
                    
                    if (thickness >= 0 && thickness < MAX_THICKNESS)
                    {
                        hitIdx = t;
                        break;
                    }
                    
                    rayPos += rayStep;
                }
                
                bool intersected = hitIdx >= 0;
                hitInfo = rayOrigin + rayStep * (hitIdx+1);
                return intersected;
            }
            
            bool CrossedCellBoundary(float2 oldCellIdx, float2 newCellIdx)
            {
                return (oldCellIdx.x != newCellIdx.x) || (oldCellIdx.y != newCellIdx.y);
            }
            
            bool FindIntersection_HiZ(float3 rayOrigin, float3 rayDir, float maxTraceDist, out float3 hitInfo)
            {
                const int MAX_MIP_LEVEL = HI_Z_MIP_COUNT - 1;
                
                // Next cell we move intos direction
                float2 crossStep = float2(rayDir.x >= 0 ? 1 : -1, rayDir.y >= 0 ? 1 : -1);
                
                // Essentially the min amount needed to nudge a ray into the next cell boundary
                float2 crossInfo = crossStep / _BlitTexture_TexelSize.zw / 128;
                crossStep = saturate(crossStep);
                
                // Initialize the ray to the position of the current sample in screen-space
                float3 rayPos = rayOrigin.xyz;
                float minZ = rayPos.z;
                float maxZ = rayPos.z + rayDir.z * maxTraceDist;
                
                float deltaZ = maxZ - minZ; // No need to flip, parametrization handles it 
                
                // Parameterize ray via ray(depth) = 0 + d * depth, s.t 0 <= depth <= 1, so this is parametrized as such
                float3 o = rayPos;
                float3 d = rayDir * maxTraceDist;
                
                // Step Hi-Z tracing constraints
                int START_MIP = 3;
                int STOP_MIP = 0;
                
                float2 startCellCount = HiZCellCount(START_MIP);
                
                // Which cell are we starting in?
                float2 rayCell = HiZGetCellAtUV(rayPos.xy, startCellCount);
                // x 64 from the article, claims needed to work at res that arent powers of 2
                rayPos = HiZIntersectCellBoundary(o, d, rayCell, startCellCount, crossStep, crossInfo * 64);
                
                // Begin tracing loop
                int curMip = START_MIP;
                uint iteration = 0;
                
                #if UNITY_REVERSED_Z
                    bool isBackwardsRay = rayDir.z > 0; // TODO: flip?
                #else
                    bool isBackwardsRay = rayDir.z < 0;
                #endif
                
                #if UNITY_REVERSED_Z
                    float viewRayDir = isBackwardsRay ? 1 : -1;
                #else
                    float viewRayDir = isBackwardsRay ? -1 : 1;
                #endif             
                
                hitInfo = viewRayDir;
                
                UNITY_LOOP
                while (curMip >= STOP_MIP // Reached most granular mip-level
                    && rayPos.z * viewRayDir <= maxZ * viewRayDir // Exceeded trace distance
                    && iteration < MAX_ITERATION) // Exceeded iteration count
                {
                    float2 cellCount = HiZCellCount(curMip);
                    float2 oldCellIdx = HiZGetCellAtUV(rayPos.xy, cellCount);
                    
                    float cellMinZ = SAMPLE_HI_Z_DEPTH((oldCellIdx + 0.5f)/cellCount, curMip);
                    
                    #if UNITY_REVERSED_Z
                        float3 tmpRay = ((cellMinZ < rayPos.z) && !isBackwardsRay) ? 
                            o + d * (cellMinZ - minZ)/deltaZ : rayPos;
                    #else
                        float3 tmpRay = ((cellMinZ > rayPos.z) && !isBackwardsRay) ? 
                            o + d * (cellMinZ - minZ)/deltaZ : rayPos;
                    #endif
                                        
                    float2 newCellIdx = HiZGetCellAtUV(tmpRay.xy, cellCount);
                    
                    #if UNITY_REVERSED_Z
                        float thickness = curMip == 0 ? (cellMinZ - rayPos.z) : 0;
                        bool crossedCell = (isBackwardsRay && (cellMinZ < rayPos.z) || (thickness > MAX_THICKNESS) || CrossedCellBoundary(oldCellIdx, newCellIdx));
                    #else
                        float thickness = curMip == 0 ? (rayPos.z - cellMinZ) : 0;
                        bool crossedCell = (isBackwardsRay && (cellMinZ > rayPos.z) || (thickness > MAX_THICKNESS) || CrossedCellBoundary(oldCellIdx, newCellIdx));
                    #endif      
                    
                    
                    rayPos = crossedCell ? HiZIntersectCellBoundary(o, d, oldCellIdx, cellCount, crossStep, crossInfo) : tmpRay;
                    curMip = crossedCell ? (min(float(MAX_MIP_LEVEL), curMip + 1)) : curMip - 1;
                    
                    ++iteration;
                }
                
                bool intersected = curMip < STOP_MIP;
                hitInfo = rayPos;
                
                return intersected;
            }

            // Out frag function takes as input a struct that contains the screen space coordinate we are going to use to sample our texture. It also writes to SV_Target0, this has to match the index set in the UseTextureFragment(sourceTexture, 0, …) we defined in our render pass script.   
            float4 Frag(Varyings input) : SV_Target0
            {
                float2 uv = input.texcoord;

                // Skip pixels not marked for ssr
                float mask = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel);
                if (mask == 0) return 0;

                // Get ray march params
                float3 r0, rd;
                float maxTraceDist;
                GetRayMarchStartParams(uv, r0, rd, maxTraceDist);
                
                float3 hitScreenPos;
                if (FindIntersection_HiZ(r0, rd, maxTraceDist, hitScreenPos))
                {
                    float hitDepth = SampleSceneDepth(hitScreenPos.xy);
                    if (hitDepth > 0.0001)
                        return float4(SampleSceneColor(hitScreenPos.xy), 1);
                }
                
                return 0;
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
            #include "../../ShaderLibrary/DeclareHiZChain.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D(_SSR); SAMPLER(sampler_SSR);

            float3 Frag(Varyings input) : SV_TARGET
            {
                // Sample
                float4 ssrSample = SAMPLE_TEXTURE2D(_SSR, sampler_SSR, input.texcoord);
                //return ssrSample.rgb;
                float viz = ssrSample.a;
                float3 sceneColor = SampleSceneColor(input.texcoord);
                return sceneColor + 0.25f * viz * ssrSample.rgb;
            }
            ENDHLSL
        }
    }
}