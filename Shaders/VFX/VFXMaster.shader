Shader "VFX/VFXMaster"
{
    Properties
    {
        [SectionHeader(Settings)]
        [Toggle(_BILLBOARD)] _Billboard("Billboard", Float) = 0
        
        [SectionHeader(Main Textures)]
        
        [MainTexture] _MainTex("Main Texture", 2D) = "white" {}
        _MainTexProperties("Texture Properties (XY Panning | ZW Contrast/Power)", Vector) = (0, 0, 1, 1)
        [Toggle] _MainTexGrayscaleIsAlpha("Use Grayscale For Alpha", Float) = 0
        [Toggle] _MainTexClampU("Clamp U", Float) = 0
        [Toggle] _MainTexClampV("Clamp V", Float) = 0
        
        [Header(Flipbook)]
        [Toggle(_IS_FLIPBOOK)] _IsFlipBook("Is FlipBook", Float) = 0
        [ShowIf(_IS_FLIPBOOK)] _FlipBookWidth("FlipBook Columns", Int) = 4
        [ShowIf(_IS_FLIPBOOK)] _FlipBookHeight("FlipBook Rows", Int) = 4
        [ShowIf(_IS_FLIPBOOK)] _FlipBookSpeed("FlipBook Speed", Range(0, 10)) = 1
        [ShowIf(_IS_FLIPBOOK, Toggle)] _ManualFlipBookAnimation("Manual Animation", Float) = 0
        [ShowIf(_IS_FLIPBOOK)] _FlipBookStartFrame("FlipBook Start Frame", Int) = 0
        [ShowIf(_IS_FLIPBOOK, Toggle)] _FlipBookFrameBlending("Frame Blending", Float) = 0
        
        [Space(10)]
        
        [Header(Secondary Texture)]
        
        [Toggle(_SECONDARY_TEXTURE)] _UseSecondaryTex("Use Secondary Texture", Float) = 0
        [ShowIf(_SECONDARY_TEXTURE)] _SecondaryTex("Secondary Texture", 2D) = "white" {}
        [ShowIf(_SECONDARY_TEXTURE)] _SecondaryTexProperties("Texture Properties (XY Panning | ZW Contrast/Power)", Vector) = (0, 0, 1, 1)
        [ShowIf(_SECONDARY_TEXTURE, Toggle)] _SecondaryTexGrayscaleIsAlpha("Use Grayscale For Alpha", Float) = 0
        
        [Space(10)]
        
        [ShowIf(_SECONDARY_TEXTURE)]
        [Enum(2x Multiply, 0, Add, 1, Min, 2, Max, 3)]
        _TextureBlendingMode("Texture Blending Mode", Int) = 0
        
        [Space(10)]
        [Toggle(_EXTRA_PANNING)] _ExtraPanning("Extra Panning", Float) = 0
        
        // Colors
        [SectionHeader(Colors)]
        [MainColor] [HDR] _ColorTint("Color Tint", Color) = (1, 1, 1, 1)
        [HDR] _BackFaceTint("Back Face Tint", Color) = (1, 1, 1, 1)
        [Toggle] _MultiplyByAlpha("Multiply by Alpha", Float) = 0
        _ColorBlendOffset("Gradient Offset", Range(-1, 1)) = 0
        
        [Header(Manual Colors)]
        [HideIf(_USE_GRADIENT_MAP)] [HDR] _HighColor("High Color", Color) = (1, 1, 1, 1)
        [HideIf(_USE_GRADIENT_MAP)] [HDR] _LowColor("Low Color", Color) = (0, 0, 0, 1)
        
        [Header(Gradient Map)]
        [Toggle(_USE_GRADIENT_MAP)] _UseGradientMap("Use Gradient Map", Float) = 0
        [ShowIf(_USE_GRADIENT_MAP)] [NoScaleOffset] _GradientMap("Gradient Map", 2D) = "white" {}
        [ShowIf(_USE_GRADIENT_MAP)] _ValuePower("Value Power", Range(0, 10)) = 1
        
        [Header(Emission)]
        [Toggle(_EMISSION_TEXTURE)] _UseEmissionTexture("Use Emission Texture", Float) = 0
        [ShowIf(_EMISSION_TEXTURE)] _EmissionTex("Emission Texture", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _EmissionIntensity("Emission Intensity", Range(0, 10)) = 1.0
        _EmissionColorInfluence("Emission Color Influence", Range(-1, 1)) = 0
        
        // UV Controls
        [SectionHeader(UV Controls)]
        [Toggle(_POLAR_COORDS)] _PolarCoords("Polar Coordinates", Float) = 0
        [Toggle(_SWIRL)] _Swirl("Swirl Effect", Float) = 0
        [ShowIf(_SWIRL)] _SwirlSpin("Swirl Spin", Float) = 1
        [ShowIf(_SWIRL)] _SwirlSpeed("Swirl Speed", Float) = 0
        
        [SectionHeader(Distortion UV)]
        [Toggle(_DISTORTION)] _Distortion("Distortion", Float) = 0
        [ShowIf(_DISTORTION)] _DistortionGuide("Distortion Guide", 2D) = "white" {}
        [ShowIf(_DISTORTION)] _DistortionSpeedX("Distortion Speed X", Float) = 0
        [ShowIf(_DISTORTION)] _DistortionSpeedY("Distortion Speed Y", Float) = 0
        [ShowIf(_DISTORTION)] _DistortionAmount("Distortion Amount", Float) = 0
        
        // Alpha Controls
        [SectionHeader(Alpha Controls)]
        _Intensity("Intensity", Range(0, 10)) = 1
        _AlphaCutoff("Alpha Cutoff", Range(0, 1)) = 0
        _AlphaCutoffSmoothness("Alpha Cutoff Smoothness", Range(0, 1)) = 0
        
        [Toggle] _UseAlphaForDissolve("Use Vertex Alpha for Dissolve", Float) = 0
        
        // Edge burn feature
        [Header(Cutoff Burn Colors)]
        [Toggle(_BURN_COLORS)] _BurnColors("Burn Colors", Float) = 0
        [ShowIf(_BURN_COLORS)] [HDR] _BurnColor("Burn Color", Color) = (1, 1, 1, 1)
        [ShowIf(_BURN_COLORS)] _BurnSize("Burn Size", Range(0, 1)) = 0
        [ShowIf(_BURN_COLORS)] _BurnSoftness("Burn Softness", Range(0, 1)) = 0
        
        // Vertex Offset
        [SectionHeader(Vertex Offset)]
        [Toggle(_VERTEX_OFFSET)] _VertexOffset("Vertex Offset", Float) = 0
        [ShowIf(_VERTEX_OFFSET)] _VertexOffsetGuide("Vertex Offset Guide", 2D) = "white" {}
        [ShowIf(_VERTEX_OFFSET)] _VertexOffsetSpeedX("Vertex Offset Speed X", Float) = 0
        [ShowIf(_VERTEX_OFFSET)] _VertexOffsetSpeedY("Vertex Offset Speed Y", Float) = 0
        [ShowIf(_VERTEX_OFFSET)] _VertexOffsetAmount("Vertex Offset Amount", Float) = 0
        [ShowIf(_VERTEX_OFFSET, Toggle)] _InvertVertexOffsetGuide("Invert Vertex Offset Guide", Float) = 0
        
        // Lighting Feature
        [SectionHeader(Lighting)]
        [Toggle(_LIGHTING)] _Lighting("Lighting", Float) = 0
        [ShowIf(_LIGHTING)] _ShadowStrength("Shadow Strength", Range(0,1)) = 0
        
        // SS Texture feature
        [SectionHeader(Screen Space Texture)]
        [Toggle(_SCREEN_SPACE_TEXTURE)] _UseScreenSpaceTex("Use Screen Space Texture", Float) = 0
        [ShowIf(_SCREEN_SPACE_TEXTURE)] _ScreenSpaceTex("Screen Space Texture", 2D) = "white" {}
        [ShowIf(_SCREEN_SPACE_TEXTURE)] [HDR] _ScreenSpaceTexTint("Screen Space Texture Tint", Color) = (1, 1, 1, 1)
        
        // Color Banding
        [SectionHeader(Banding)]
        [Toggle(_COLOR_BANDING)] _ColorBanding("Color Banding", Float) = 0
        [ShowIf(_COLOR_BANDING)] [IntRange] _Bands("Bands Count", Range(1, 64)) = 3
        
        // Animation time-step
        [SectionHeader(Stepped Time)]
        [Toggle(_STEPPED_TIME)] _SteppedTime("Stepped Time", Float) = 0
        [ShowIf(_STEPPED_TIME)] _TimeStep("Time Step", Float) = 0.1
        
        // Additional masks options
        [SectionHeader(Masks)]
        [Header(Mask Settings)]
        [Enum(Multiply, 0, AddSub, 1)] _MaskMode("Mask Mode", Int) = 0
        _MaskPower("Mask Power", Range(0, 1)) = 1
        
        [Header(Mask Texture)]
        [Toggle(_MASK_TEXTURE)] _UseMaskTexture("Use Mask Texture", Float) = 0
        [ShowIf(_MASK_TEXTURE)] _MaskTex("Mask Texture", 2D) = "white" {}
        
        [Header(Circle Mask)]
        [Toggle(_CIRCLE_MASK)] _CircleMask("Circle Mask", Float) = 0
        [ShowIf(_CIRCLE_MASK)] _CircleOuterRadius("Circle Outer Radius", Range(0, 1)) = 0.5
        [ShowIf(_CIRCLE_MASK)] _CircleInnerRadius("Circle Inner Radius", Range(-1, 1)) = 0
        [ShowIf(_CIRCLE_MASK)] _CircleMaskSmoothness("Circle Mask Smoothness", Range(0, 1)) = 0.2
        
        [Header(Rect Mask)]
        [Toggle(_RECT_MASK)] _RectMask("Rect Mask", Float) = 0
        [ShowIf(_RECT_MASK)] _RectWidth("Rect Width", Range(0, 1)) = 0.5
        [ShowIf(_RECT_MASK)] _RectHeight("Rect Height", Range(0, 1)) = 0.5
        [ShowIf(_RECT_MASK)] _RectSmoothness("Rect Smoothness", Range(0, 1)) = 0.5
        [ShowIf(_RECT_MASK)] _RectCutoff("Rect Cutoff", Range(0, 1)) = 0
        
        [Header(Gradient Mask)]
        [Toggle(_GRADIENT_MASK)] _GradientMask("Gradient Mask", Float) = 0
        [ShowIf(_GRADIENT_MASK)] _GradientMaskProperties("XY: Start/End X | ZW Start/End Y", Vector) = (0, 1, 0, 1)
        
        [Header(Fresnel Mask)]
        [Toggle(_FRESNEL_MASK)] _FresnelMask("Fresnel Mask", Float) = 0
        [ShowIf(_FRESNEL_MASK)] _FresnelMaskPower("Fresnel Mask Power", Range(0, 10)) = 1
        
        // Depth soft-blending
        [SectionHeader(Depth Blending)]
        [Toggle(_DEPTH_BLENDING)] _DepthBlending("Depth Blending", Float) = 0
        [ShowIf(_DEPTH_BLENDING)] _DepthBlendDistance("Depth Blend Distance", Range(0, 10)) = 1.0
        
        // Culling / Blending options
        [SectionHeader(Culling and Blending)]
        [Enum(UnityEngine.Rendering.CullMode)] _Culling("Cull Mode", Int) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend Mode", Int) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend Mode", Int) = 10
        [Enum(Off, 0, On, 1)] _ZWrite("Z Write", Int) = 0
        
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        
        Blend [_SrcBlend] [_DstBlend]
        Cull [_Culling]
        ZWrite [_ZWrite]
        Offset -1, -1
        LOD 100
        
        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "../../ShaderLibrary/UVFunctions.hlsl"
        #include "../../ShaderLibrary/VertexOperations.hlsl"
        #include "../../ShaderLibrary/Flipbook.hlsl"

        #pragma shader_feature_vertex _BILLBOARD
        
        #pragma shader_feature_local _IS_FLIPBOOK
        
        #pragma shader_feature_local _SECONDARY_TEXTURE
        #pragma shader_feature_local _EXTRA_PANNING
        
        #pragma shader_feature_local _POLAR_COORDS
        #pragma shader_feature_local _SWIRL
        
        #pragma shader_feature_local _DISTORTION
        
        #pragma shader_feature_local _USE_GRADIENT_MAP
        
        #pragma shader_feature_local _EMISSION_TEXTURE
        
        #pragma shader_feature_local _VERTEX_OFFSET
        
        #pragma shader_feature_local _LIGHTING
        
        #pragma shader_feature_local _BURN_COLORS
        #pragma shader_feature_local _SCREEN_SPACE_TEXTURE
        
        #pragma shader_feature_local _COLOR_BANDING
        #pragma shader_feature_local _STEPPED_TIME
        
        #pragma shader_feature_local _MASK_TEXTURE
        #pragma shader_feature_local _CIRCLE_MASK
        #pragma shader_feature_local _RECT_MASK
        #pragma shader_feature_local _GRADIENT_MASK
        #pragma shader_feature_local _FRESNEL_MASK
        
        #pragma shader_feature_local _DEPTH_BLENDING

        // ---------------------------------

CBUFFER_START(UnityPerMaterial)        
        // Main Textures
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_ST;
        float4 _MainTexProperties; // XY Panning | ZW Contrast/Power
        float _MainTexGrayscaleIsAlpha; // (NEW) I think unnecessary w/ "Grayscale is Alpha" setting on texture import
        float _MainTexClampU; // (NEW)
        float _MainTexClampV; // (NEW)

        // Flipbook Params (NEW)
        int _FlipBookWidth;
        int _FlipBookHeight;
        float _FlipBookSpeed;
        float _ManualFlipBookAnimation;
        int _FlipBookStartFrame;
        float _FlipBookFrameBlending;

        // Secondary texture blending
        TEXTURE2D(_SecondaryTex);
        SAMPLER(sampler_SecondaryTex);
        float4 _SecondaryTex_ST;
        float4 _SecondaryTexProperties; // XY Panning | ZW Contrast/Power
        float _SecondaryTexGrayscaleIsAlpha; // (NEW)

        int _TextureBlendingMode; // 0: 2x Multiply, 1: Add, 2: Min, 3: Max


        // UV Controls
        float _SwirlSpin;
        float _SwirlSpeed;

        // Distortion/Distortion
        TEXTURE2D(_DistortionGuide);
        SAMPLER(sampler_DistortionGuide);
        float4 _DistortionGuide_ST;
        float _DistortionSpeedX; // Distortion Speed X
        float _DistortionSpeedY; // Distortion Speed Y
        float _DistortionAmount; // Distortion Amount
        
        // Colors
        float4 _ColorTint; // Color Tint
        float4 _BackFaceTint; // Backface tint
        float _MultiplyByAlpha; // Multiply by Alpha
        float _ColorBlendOffset; // Gradient Offset

        // Manual Colors
        float4 _HighColor; // High Color
        float4 _LowColor; // Low Color
        
        // Gradient Map
        TEXTURE2D(_GradientMap);
        SAMPLER(sampler_GradientMap);
        float _ValuePower; // Value Power

        // Emission
        TEXTURE2D(_EmissionTex);
        SAMPLER(sampler_EmissionTex);
        float4 _EmissionTex_ST;
        float4 _EmissionColor; // Emission Color
        float _EmissionIntensity; // Emission Intensity
        float _EmissionColorInfluence; // Color influence (i.e, factor for multiplying the resulting luminance w/ the emission). Negative means emission occurs at darker spots

        // Vertex Offset
        TEXTURE2D(_VertexOffsetGuide);
        SAMPLER(sampler_VertexOffsetGuide);
        float4 _VertexOffsetGuide_ST;
        float _VertexOffsetSpeedX; // Vertex Offset Speed X
        float _VertexOffsetSpeedY; // Vertex Offset Speed Y
        float _VertexOffsetAmount; // Vertex Offset Amount
        int _InvertVertexOffsetGuide; // Invert Vertex Offset Guide

        // Lighting
        float _ShadowStrength; // Shadow Strength

        // Alpha Controls
        float _Intensity;
        float _AlphaCutoff; // Alpha Cutoff
        float _AlphaCutoffSmoothness; // Alpha Cutoff Smoothness
        int _UseAlphaForDissolve; // Use Vertex Alpha for Dissolve

        // Cutoff Burn Colors
        float4 _BurnColor; // Burn Color
        float _BurnSize; // Burn Size
        float _BurnSoftness; // Burn Softness

        // Screen Space Texture
        TEXTURE2D(_ScreenSpaceTex);
        SAMPLER(sampler_ScreenSpaceTex);
        float4 _ScreenSpaceTex_ST; // Screen Space Texture ST
        float4 _ScreenSpaceTexTint; // Screen Space Texture Tint

        // Banding
        int _Bands; // Bands Count

        // Time Controls
        float _TimeStep; // Time Step

        // Masks
        int _MaskMode; // 0: Multiply, 1: AddSub
        float _MaskPower; // Mask Power

        // Mask Texture
        TEXTURE2D(_MaskTex);
        SAMPLER(sampler_MaskTex);
        float4 _MaskTex_ST; // Mask Texture ST

        // Circle Proc-Mask
        float _CircleOuterRadius; // Circle Outer Radius
        float _CircleInnerRadius; // Circle Inner Radius
        float _CircleMaskSmoothness; // Circle Mask Smoothness

        // Rect Proc-Mask
        float _RectWidth; // Rect Width
        float _RectHeight; // Rect Height
        float _RectSmoothness; // Rect Smoothness
        float _RectCutoff; // Rect Cutoff

        // Gradient Proc-Mask
        float4 _GradientMaskProperties; // XY: Start/End X | ZW Start/End Y

        // Fresnel Proc-Mask
        float _FresnelMaskPower; // Fresnel Mask Power

        // Depth Blending
        float _DepthBlendDistance; // Depth Blend Distance

        // Blending Options
        int _SrcBlend;
        int _DstBlend;
CBUFFER_END        

        #define IS_PREMULTIPLIED (_SrcBlend == 1 && _DstBlend == 10)
        
        // ---------------------------------

        
        float GetTime(float speed)
        {
            float time = _Time.y * speed;
#ifdef _STEPPED_TIME
            time = ceil(_Time.y * (speed / _TimeStep)) * _TimeStep;
#endif
            return time;
        }

        float4 ProcessColor(float4 col, float grayScaleForAlpha, float contrast, float power)
        {
            col.a = lerp(col.a, col.r, grayScaleForAlpha);
            col = saturate(lerp(0.5, col, contrast)) * power;
            col.rgb *= lerp(1, col.a, grayScaleForAlpha * _MultiplyByAlpha); // Multiply by alpha if needed (only if alpha is alpha) Wait, note how we're multiplying by alpha TWICE, check end of shader
            return col;
        }

        // Value in x, alpha in y
        float2 SampleTex(Texture2D tex, SamplerState sampler_tex, float2 uv, float4 tex_ST, float grayScaleForAlpha, float4 props, float4 extraPanning, float2 uvClamp = 0)
        {
            float2 timeOffset = float2(GetTime(props.x), GetTime(props.y));
            uv = TRANSFORM_TEX(uv, tex) + timeOffset + extraPanning.xy + extraPanning.z;
            uv.x = lerp(uv.x, clamp(uv.x, -1, 1), uvClamp.x);
            uv.y = lerp(uv.y, clamp(uv.y, -1, 1), uvClamp.y);
#ifdef _POLAR_COORDS            
            float4 col = SAMPLE_TEXTURE2D_GRAD(tex, sampler_tex, uv, 0, 0);
#else            
            float4 col = SAMPLE_TEXTURE2D(tex, sampler_tex, uv);
#endif
            col = ProcessColor(col, grayScaleForAlpha, props.z, props.w);
            return float2(col.r, col.a);
        }

        float4 SampleTex(Texture2D tex, SamplerState sampler_tex, float2 uv, float4 tex_ST)
        {
            uv = TRANSFORM_TEX(uv, tex);
            return SAMPLE_TEXTURE2D(tex, sampler_tex, uv);
        }

        float2 SampleTextureFlipBook(TEXTURE2D_PARAM(tex, sampler_tex), float2 uv, float grayScaleForAlpha, float4 props)
        {
#ifdef _DISTORTION            
            uv = DistortionUV(uv, TEXTURE2D_ARGS(_DistortionGuide, sampler_DistortionGuide), _DistortionGuide_ST, _DistortionAmount, float2(GetTime(_DistortionSpeedX), GetTime(_DistortionSpeedY)));
#endif

            float frameCount = _FlipBookWidth * _FlipBookHeight;
            float animPct = lerp(_Time.y * _FlipBookSpeed, 0, _ManualFlipBookAnimation) + _FlipBookStartFrame / (frameCount);

            float4 col = SampleFlipBook(TEXTURE2D_ARGS(tex, sampler_tex), uv, float2(_FlipBookWidth, _FlipBookHeight), animPct, _FlipBookFrameBlending);
            col = ProcessColor(col, grayScaleForAlpha, props.z, props.w);
            return float2(col.r, col.a);
        }
        

        void ManipulateUVs(inout float2 uv)
        {
#ifdef _POLAR_COORDS
            uv = PolarCoordinates(uv);
#endif

#ifdef _SWIRL
            uv = SwirlUV(uv, saturate(1 - length((uv * 2) - 1)) * _SwirlSpin + GetTime(_SwirlSpeed) * TWO_PI);
#endif
            
#ifdef _DISTORTION
            uv = DistortionUV(uv, TEXTURE2D_ARGS(_DistortionGuide, sampler_DistortionGuide), _DistortionGuide_ST, _DistortionAmount, float2(GetTime(_DistortionSpeedX), GetTime(_DistortionSpeedY)));
#endif
        }

        float2 BlendTextures(float2 A, float2 B)
        {
            switch (_TextureBlendingMode)
            {
                case 0: // 2x Multiply
                    return 2 * A * B;
                case 1: // Add
                    return A + B;
                case 2: // Min
                    return min(A, B);
                case 3: // Max
                    return max(A, B);
                default:
                    return 2 * A * B; // Default to 2x Multiply
            }
        }

        void ApplyMask(inout float value, float mask)
        {
            switch (_MaskMode)
            {
                case 0: // Multiply
                    value *= mask * _MaskPower;
                    break;
                case 1: // AddSub
                    value = saturate(value + saturate(mask * 2 - 1) * _MaskPower) * mask; // [0,1] -> [-0.5, 0.5]
                    break;
                default: // Multiply
                    value *= mask * _MaskPower; // Default to Multiply
                    break;
            }
        }
        
        ENDHLSL

        Pass
        {
            Name "HVakisVFX"
            Tags
            {
                "LightMode"="VFX" // Glow breaks w/ back-face culling!
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct VertexData
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR; // Vertex color for additional effects
                float4 uv : TEXCOORD0;
                float4 customData : TEXCOORD1; // (Panning) Custom data for additional effects from Shuriken
            };

            struct Interpolators
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float3 normalWS : NORMAL;
                float4 uv : TEXCOORD0;
                float4 customData : TEXCOORD1; // Custom data for additional effects from Shuriken
                float4 screenPos : TEXCOORD2;
                float3 viewDir : TEXCOORD3; // View direction for lighting calculations & fresnel
            };

            struct PixelOut
            {
                float4 color : SV_Target0;
                float4 glow : SV_Target1;
            };

            Interpolators vert(VertexData i)
            {
                Interpolators o = (Interpolators)0;
#ifdef _VERTEX_OFFSET
                float2 uv = TRANSFORM_TEX(i.uv.xy, _VertexOffsetGuide) + float2(GetTime(_VertexOffsetSpeedX), GetTime(_VertexOffsetSpeedY));
                float offset = SAMPLE_TEXTURE2D_LOD(_VertexOffsetGuide, sampler_VertexOffsetGuide, uv, 0).r;
                if (_InvertVertexOffsetGuide == 1) 
                {
                    offset = 1 - offset; // Invert the guide if needed
                }
                offset = (offset * 2 - 1) * _VertexOffsetAmount; // Scale to [-1, 1] range
                i.positionOS.xyz += i.normalOS * offset; // Apply vertex offset
#endif

#ifdef _BILLBOARD
                o.vertex = TransformObjectToBillboardHClip(i.positionOS);
#else                
                o.vertex = TransformObjectToHClip(i.positionOS);
#endif                
                o.color = i.color;
                o.uv = i.uv;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.customData = i.customData; // Pass custom data through
                o.screenPos = ComputeScreenPos(o.vertex);
                o.viewDir = GetWorldSpaceViewDir(TransformObjectToWorld(i.positionOS));
                return o;
            }

            PixelOut frag(Interpolators i, bool isFrontFace : SV_IsFrontFace)
            {
                PixelOut o = (PixelOut)0;

                i.normalWS = normalize(i.normalWS);
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // Prepare UV for primary texture sampling
                float2 uv = i.uv.xy;
                float4 extraPanning = 0;
#ifdef _EXTRA_PANNING
                extraPanning = i.customData;
#endif

#ifdef _IS_FLIPBOOK
                float2 flipBookUVs = uv;
                ManipulateUVs(uv); // UVs werent manipulated for
                float2 texVal = SampleTextureFlipBook(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), flipBookUVs, _MainTexGrayscaleIsAlpha, float4(0, 0, _MainTexProperties.zw));
#else
                ManipulateUVs(uv);
                float2 texVal = SampleTex(_MainTex, sampler_MainTex, uv, _MainTex_ST, _MainTexGrayscaleIsAlpha, _MainTexProperties, extraPanning, float2(_MainTexClampU, _MainTexClampV));
#endif                 


#ifdef _SECONDARY_TEXTURE
                texVal = BlendTextures(texVal, SampleTex(_SecondaryTex, sampler_SecondaryTex, uv, _SecondaryTex_ST, _SecondaryTexGrayscaleIsAlpha, _SecondaryTexProperties, extraPanning));
#endif

                float value = texVal.x;
                float alpha = saturate(texVal.y);

                // Masking
                float mask = 1;
                float2 rawUV = i.uv.xy;
#ifdef _MASK_TEXTURE
                float2 maskUV = TRANSFORM_TEX(rawUV, _MaskTex);
                mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV).r;
                ApplyMask(alpha, mask);
#endif

#ifdef _CIRCLE_MASK
                float circle = distance(rawUV, float2(0.5, 0.5));
                mask = 1 - smoothstep(_CircleOuterRadius, _CircleOuterRadius + _CircleMaskSmoothness, circle);
                mask *= smoothstep(_CircleInnerRadius, _CircleInnerRadius + _CircleMaskSmoothness, circle);
                ApplyMask(alpha, mask);
#endif

#ifdef _RECT_MASK
                float2 rectUV = (rawUV * 2 - 1);
                float rect = max(abs(rectUV.x / _RectWidth), abs(rectUV.y / _RectHeight));
                mask = 1 - smoothstep(_RectCutoff, _RectCutoff + _RectSmoothness, rect);
                ApplyMask(alpha, mask);
#endif

#ifdef _GRADIENT_MASK
                mask = smoothstep(_GradientMaskProperties.y, _GradientMaskProperties.x, rawUV.x);
                mask *= smoothstep(_GradientMaskProperties.w, _GradientMaskProperties.z, rawUV.y);
                ApplyMask(alpha, mask);
#endif                 
                
#ifdef _FRESNEL_MASK
                mask = pow(saturate(dot(normalize(i.viewDir), i.normalWS)), _FresnelMaskPower);
                ApplyMask(alpha, mask);
#endif

                // Cutoff
                float cutoff = saturate(_AlphaCutoff + i.uv.z);
                cutoff += (1 - i.color.a) * _UseAlphaForDissolve;
                if (alpha <= saturate(cutoff)) discard;
                //clip(alpha - saturate(cutoff));
                alpha =
                    smoothstep(cutoff, saturate(cutoff + _AlphaCutoffSmoothness), alpha)
                    * _ColorTint.a * saturate(i.color.a - _UseAlphaForDissolve);

                // Banding
                value += _ColorBlendOffset;
#ifdef _COLOR_BANDING
                value = round(value * _Bands) / _Bands;
#endif

                value = pow(value, _ValuePower);

                float gradientVal = saturate(value);// + _ColorBlendOffset);

                float4 col = float4(i.color.rgb, _UseAlphaForDissolve ? i.color.a : 1) * _ColorTint;
#ifdef _USE_GRADIENT_MAP
                col *= SAMPLE_TEXTURE2D(_GradientMap, sampler_GradientMap, float2(gradientVal, 0));
#else
                // Color value sample shifts based on cutoff
                col *= lerp(_LowColor, _HighColor, gradientVal);
#endif

#ifdef _LIGHTING
                Light L = GetMainLight();
                float NoL = saturate(dot(i.normalWS, normalize(L.direction)));
                NoL = step(0.5, NoL);
                col.rgb *= max(1 - _ShadowStrength, NoL);
#endif

#ifdef _DEPTH_BLENDING
                float depth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depthDelta = saturate(_DepthBlendDistance * (depth - i.screenPos.w));
                alpha *= depthDelta;
#endif                
                
                col.a *= alpha;
#ifdef _BURN_COLORS
                col.rgb = lerp(col.rgb, _BurnColor, smoothstep(alpha - cutoff, saturate(alpha - cutoff + _BurnSoftness), _BurnSize));// * smoothstep(0.001, 0.1, cutoff);
#endif

#ifdef _SCREEN_SPACE_TEXTURE
                screenUV.x *= _ScreenParams.x / _ScreenParams.y; // Maintain aspect ratio
                col.rgb += SAMPLE_TEXTURE2D(_ScreenSpaceTex, sampler_ScreenSpaceTex, screenUV * _ScreenSpaceTex_ST.xy).rgb * _ScreenSpaceTexTint.rgb;
#endif

                col *= _Intensity;
                o.color = (col * lerp(1, alpha, IS_PREMULTIPLIED));
                o.color *= lerp(_BackFaceTint, 1, isFrontFace);
                o.color.a = saturate(o.color.a); // Make sure this is [0,1] otherwise the blending causes bad color distortion in the BG (i.e negative colors)
                o.glow = _EmissionColor * _EmissionIntensity * o.color.a;

                float colorLuma = saturate(max(o.color.r, max(o.color.g, o.color.b)));
                // flip if emission color influence is negative
                colorLuma = lerp(1 - colorLuma, colorLuma, saturate(sign(_EmissionColorInfluence)));
                o.glow *= lerp(1, colorLuma, abs(_EmissionColorInfluence));
                //o.color = lerp(1, colorLuma, abs(_EmissionColorInfluence)); // For Debug
                //o.glow = 0;

#ifdef _EMISSION_TEXTURE
                o.glow *= SampleTex(_EmissionTex, sampler_EmissionTex, uv, _EmissionTex_ST);
#endif

                return o;
            }
            
            ENDHLSL
        }
    }
}