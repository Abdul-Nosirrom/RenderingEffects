Shader "VFX/VFXMaster"
{
    // TODO: Random value for PS to add random offset/panning for noise textures to give variation this is important loook at the diablo vid
    Properties
    {
        [SectionHeader(Settings)]
        // Enable bill-board for the material
        [Toggle(_BILLBOARD)] _Billboard("Billboard", Float) = 0
        
        // TEXCOORD1: [x: Intensity, y: AlphaCutoff, z: Color Gradient Offset, w: Emission Intensity]
        // TEXCOORD2: [xy: MainTex UV Offset, zw: MainTex UV Tiling]
        [Toggle(_PARTICLE_SYSTEM)]
        [Tooltip(Enables custom vertex streams from Shuriken particle systems via TEXCOORD1 and TEXCOORD2)]
        _ParticleSystem("Is Used By Particle System", Float) = 0
        
        [SectionHeader(Main Textures)]
        
        [MainTexture] _MainTex("Main Texture", 2D) = "white" {}
        [Tooltip(XY controls UV panning speed. ZW controls contrast and power.)]
        _MainTexProperties("Texture Properties (XY Panning | ZW Contrast/Power)", Vector) = (0, 0, 1, 1)
        [Toggle] _MainTexClampU("Clamp U", Float) = 0
        [Toggle] _MainTexClampV("Clamp V", Float) = 0
        _MainTexRotation("Texture Rotation (Degrees)", Range(0, 360)) = 0
        
        [Header(Flipbook)]
        
        [Tooltip(Disabled when Particle System is enabled as Shuriken handles flipbooks internally)]
        [Toggle(_IS_FLIPBOOK)] _IsFlipBook("Is FlipBook", Float) = 0
        [ShowIf(_IS_FLIPBOOK)] _FlipBookWidth("FlipBook Columns", Int) = 4
        [ShowIf(_IS_FLIPBOOK)] _FlipBookHeight("FlipBook Rows", Int) = 4
        [ShowIf(_IS_FLIPBOOK)] _FlipBookSpeed("FlipBook Speed", Range(0, 10)) = 1
        [ShowIf(_IS_FLIPBOOK, Toggle)] 
        [Tooltip(When enabled use FlipBook Start Frame to manually control the current frame)]
        _ManualFlipBookAnimation("Manual Animation", Float) = 0
        [ShowIf(_IS_FLIPBOOK)] _FlipBookStartFrame("FlipBook Start Frame", Int) = 0
        [ShowIf(_IS_FLIPBOOK, Toggle)] 
        [Tooltip(Smoothly blends between frames instead of hard cuts)]
        _FlipBookFrameBlending("Frame Blending", Float) = 0
        
        [Space(10)]
        
        [Header(Secondary Texture)]
        
        [Toggle(_SECONDARY_TEXTURE)] _UseSecondaryTex("Use Secondary Texture", Float) = 0
        [ShowIf(_SECONDARY_TEXTURE)] _SecondaryTex("Secondary Texture", 2D) = "white" {}
        [ShowIf(_SECONDARY_TEXTURE)] 
        [Tooltip(XY controls UV panning speed. ZW controls contrast and power.)]
        _SecondaryTexProperties("Texture Properties (XY Panning | ZW Contrast/Power)", Vector) = (0, 0, 1, 1)
        [ShowIf(_SECONDARY_TEXTURE)] _SecondaryTexRotation("Texture Rotation (Degrees)", Range(0, 360)) = 0

        [Space(10)]
        
        [ShowIf(_SECONDARY_TEXTURE)]
        [Tooltip(How the main and secondary textures are combined)]
        [Enum(2x Multiply, 0, Add, 1, Min, 2, Max, 3)]
        _TextureBlendingMode("Texture Blending Mode", Int) = 0
        
        // Colors
        [SectionHeader(Colors)]
        [MainColor] [HDR] _ColorTint("Color Tint", Color) = (1, 1, 1, 1)
        [Tooltip(Multiplied with color when rendering back faces. Set to white for no effect.)]
        [HDR] _BackFaceTint("Back Face Tint", Color) = (1, 1, 1, 1)
        // TODO: [Toggle] [Tooltip(If true the backface tint overrides color rather than tints it)] _BackFaceColorOverride("Back Face Tint Overrides Color", Color) = 0
        [Tooltip(Shifts the gradient sample point for Low and High color blending)]
        _ColorBlendOffset("Gradient Offset", Range(-1, 1)) = 0
        
        [Space(10)]
        [Tooltip(Remaps the color gradient based on alpha cutoff so dissolving pixels shift color)]
        [Toggle(_ALPHA_CUTOFF_MODIFIES_COLOR)] _AlphaCutoffInfluencesColor("Shift Color Blend Value Based On Alpha Cutoff", Float) = 0
        [ShowIf(_ALPHA_CUTOFF_MODIFIES_COLOR)] 
        [Tooltip(Controls where the color shift ends. Lower values mean faster color transition.)]
        _ColorCutoff("Color Blend Cutoff", Range(0,1)) = 0.5 // TODO: Improve on this
        [Space(10)]
        
        [Header(Manual Colors)]
        [Tooltip(Color applied to dark regions of the texture)]
        [HideIf(_USE_GRADIENT_MAP)] [HDR] _LowColor("Low Color", Color) = (0, 0, 0, 1)
        [Tooltip(Color applied to bright regions of the texture)]
        [HideIf(_USE_GRADIENT_MAP)] [HDR] _HighColor("High Color", Color) = (1, 1, 1, 1)
        
        [Header(Gradient Map)]
        [Tooltip(Sample colors from a gradient texture instead of Low and High colors)]
        [Toggle(_USE_GRADIENT_MAP)] _UseGradientMap("Use Gradient Map", Float) = 0
        [ShowIf(_USE_GRADIENT_MAP)] [NoScaleOffset] _GradientMap("Gradient Map", 2D) = "white" {}
        [ShowIf(_USE_GRADIENT_MAP)] 
        [Tooltip(Adjusts the gradient lookup curve. Higher values push samples toward bright end.)]
        _ValuePower("Value Power", Range(0, 10)) = 1
        
        [Header(Emission)]
        [Toggle(_EMISSION_TEXTURE)] _UseEmissionTexture("Use Emission Texture", Float) = 0
        [ShowIf(_EMISSION_TEXTURE)] _EmissionTex("Emission Texture", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _EmissionIntensity("Emission Intensity", Range(0, 10)) = 1.0
        [Header(Emission Range)]
        [Tooltip(Luma values below this threshold will not receive glow. 0 means no lower cutoff.)]
        _EmissionRangeMin("Emission Range Min", Range(0, 1)) = 0
        [Tooltip(Luma values above this threshold will not receive glow. 1 means no upper cutoff.)]
        _EmissionRangeMax("Emission Range Max", Range(0, 1)) = 1
        [Tooltip(Softness of the transition at range boundaries. 0 is a hard cutoff.)]
        _EmissionSoftness("Emission Softness", Range(0, 0.5)) = 0.5
        
        // UV Controls
        [SectionHeader(UV Controls)]
        [Tooltip(Converts UVs to polar coordinates for radial effects)]
        [Toggle(_POLAR_COORDS)] _PolarCoords("Polar Coordinates", Float) = 0
        [Tooltip(Creates a spiral distortion effect centered on UV 0.5 0.5)]
        [Toggle(_SWIRL)] _Swirl("Swirl Effect", Float) = 0
        [ShowIf(_SWIRL)] 
        [Tooltip(Amount of rotation. Higher values create tighter spirals.)]
        _SwirlSpin("Swirl Spin", Float) = 1
        [ShowIf(_SWIRL)] 
        [Tooltip(Animates the swirl rotation over time)]
        _SwirlSpeed("Swirl Speed", Float) = 0
        
        [SectionHeader(Distortion UV)]
        [Tooltip(Distorts UVs based on a guide texture for animated effects)]
        [Toggle(_DISTORTION)] _Distortion("Distortion", Float) = 0
        [ShowIf(_DISTORTION)] _DistortionGuide("Distortion Guide", 2D) = "white" {}
        [ShowIf(_DISTORTION)] _DistortionSpeedX("Distortion Speed X", Float) = 0
        [ShowIf(_DISTORTION)] _DistortionSpeedY("Distortion Speed Y", Float) = 0
        [ShowIf(_DISTORTION)] _DistortionAmount("Distortion Amount", Float) = 0
        
        // Alpha Controls
        [SectionHeader(Alpha Controls)]
        [Tooltip(Overall brightness and alpha multiplier)]
        _Intensity("Intensity", Range(0, 10)) = 1
        [Tooltip(Pixels with alpha below this value are discarded)]
        _AlphaCutoff("Alpha Cutoff", Range(0, 1)) = 0
        [Tooltip(Creates a soft edge at the cutoff boundary instead of a hard cut)]
        _AlphaCutoffSmoothness("Alpha Cutoff Smoothness", Range(0, 1)) = 0
        
        [Tooltip(Mode 0 and 1 use RGB for tint. Mode 2 and 3 use Red channel for fade. Dissolve modes use Alpha to drive cutoff.)]
        [Enum(RGB Tint Alpha Fade, 0, RGB Tint Alpha Dissolve, 1, No Tint Red Fade, 2, No Tint Red Fade Alpha Dissolve, 3)]
        _VertexColorMode("Vertex Color Mode", Int) = 2
        
        // Edge burn feature
        [Header(Cutoff Burn Colors)]
        [Tooltip(Applies a color to pixels near the dissolve edge)]
        [Toggle(_BURN_COLORS)] _BurnColors("Burn Colors", Float) = 0
        [ShowIf(_BURN_COLORS)] [HDR] _BurnColor("Burn Color", Color) = (1, 1, 1, 1)
        [ShowIf(_BURN_COLORS)] 
        [Tooltip(How far the burn color extends from the edge. Higher values affect more pixels.)]
        _BurnSize("Burn Size", Range(0, 1)) = 0
        [ShowIf(_BURN_COLORS)] 
        [Tooltip(Softness of the burn color transition)]
        _BurnSoftness("Burn Softness", Range(0, 1)) = 0
        
        // Vertex Offset
        [SectionHeader(Vertex Offset)]
        [Tooltip(Displaces vertices along their normals based on a guide texture)]
        [Toggle(_VERTEX_OFFSET)] _VertexOffset("Vertex Offset", Float) = 0
        [ShowIf(_VERTEX_OFFSET)] _VertexOffsetGuide("Vertex Offset Guide", 2D) = "white" {}
        [ShowIf(_VERTEX_OFFSET)] _VertexOffsetSpeedX("Vertex Offset Speed X", Float) = 0
        [ShowIf(_VERTEX_OFFSET)] _VertexOffsetSpeedY("Vertex Offset Speed Y", Float) = 0
        [ShowIf(_VERTEX_OFFSET)] _VertexOffsetAmount("Vertex Offset Amount", Float) = 0
        [ShowIf(_VERTEX_OFFSET, Toggle)] _InvertVertexOffsetGuide("Invert Vertex Offset Guide", Float) = 0
        
        // Lighting Feature TODO: Fixed light direction w/ cel shading?
        [SectionHeader(Lighting)]
        [Tooltip(Enables simple cel shaded lighting from the main directional light)]
        [Toggle(_LIGHTING)] _Lighting("Lighting", Float) = 0
        [ShowIf(_LIGHTING)] 
        [Tooltip(How dark the shadowed areas appear. 1 is fully black shadows.)]
        _ShadowStrength("Shadow Strength", Range(0,1)) = 0
        
        // SS Texture feature
        [SectionHeader(Screen Space Texture)]
        [Tooltip(Adds a texture in screen space. Useful for noise or pattern overlays.)]
        [Toggle(_SCREEN_SPACE_TEXTURE)] _UseScreenSpaceTex("Use Screen Space Texture", Float) = 0
        [ShowIf(_SCREEN_SPACE_TEXTURE)] _ScreenSpaceTex("Screen Space Texture", 2D) = "white" {}
        [ShowIf(_SCREEN_SPACE_TEXTURE)] [HDR] _ScreenSpaceTexTint("Screen Space Texture Tint", Color) = (1, 1, 1, 1)
        
        // Color Banding
        [SectionHeader(Banding)]
        [Tooltip(Posterizes the gradient into discrete color bands)]
        [Toggle(_COLOR_BANDING)] _ColorBanding("Color Banding", Float) = 0
        [ShowIf(_COLOR_BANDING)] [IntRange] _Bands("Bands Count", Range(1, 64)) = 3
        
        // Animation time-step
        [SectionHeader(Stepped Time)]
        [Tooltip(Quantizes time for choppy low framerate animation style)]
        [Toggle(_STEPPED_TIME)] _SteppedTime("Stepped Time", Float) = 0
        [ShowIf(_STEPPED_TIME)] 
        [Tooltip(Time interval in seconds between animation updates)]
        _TimeStep("Time Step", Float) = 0.1
        
        // Additional masks options
        [SectionHeader(Masks)]
        [Header(Mask Settings)]
        [Tooltip(Multiply directly scales alpha. AddSub adds mask value to alpha then multiplies.)]
        [Enum(Multiply, 0, AddSub, 1)] _MaskMode("Mask Mode", Int) = 0
        [Tooltip(Strength of the mask effect)]
        _MaskPower("Mask Power", Range(0, 1)) = 1
        
        [Header(Mask Texture)]
        [Toggle(_MASK_TEXTURE)] _UseMaskTexture("Use Mask Texture", Float) = 0
        [ShowIf(_MASK_TEXTURE)] _MaskTex("Mask Texture", 2D) = "white" {}
        
        [Header(Circle Mask)]
        [Tooltip(Procedural circular mask centered at UV 0.5 0.5)]
        [Toggle(_CIRCLE_MASK)] _CircleMask("Circle Mask", Float) = 0
        [ShowIf(_CIRCLE_MASK)] _CircleOuterRadius("Circle Outer Radius", Range(0, 1)) = 0.5
        [ShowIf(_CIRCLE_MASK)] 
        [Tooltip(Creates a ring shape when greater than zero)]
        _CircleInnerRadius("Circle Inner Radius", Range(-1, 1)) = 0
        [ShowIf(_CIRCLE_MASK)] _CircleMaskSmoothness("Circle Mask Smoothness", Range(0, 1)) = 0.2
        
        [Header(Rect Mask)]
        [Tooltip(Procedural rectangular mask centered at UV 0.5 0.5)]
        [Toggle(_RECT_MASK)] _RectMask("Rect Mask", Float) = 0
        [ShowIf(_RECT_MASK)] _RectWidth("Rect Width", Range(0, 1)) = 0.5
        [ShowIf(_RECT_MASK)] _RectHeight("Rect Height", Range(0, 1)) = 0.5
        [ShowIf(_RECT_MASK)] _RectSmoothness("Rect Smoothness", Range(0, 1)) = 0.5
        [ShowIf(_RECT_MASK)] _RectCutoff("Rect Cutoff", Range(0, 1)) = 0
        
        [Header(Gradient Mask)]
        [Tooltip(Procedural linear gradient mask along X and Y axes)]
        [Toggle(_GRADIENT_MASK)] _GradientMask("Gradient Mask", Float) = 0
        [ShowIf(_GRADIENT_MASK)] 
        [Tooltip(XY controls X axis gradient start and end. ZW controls Y axis.)]
        _GradientMaskProperties("XY: Start/End X | ZW Start/End Y", Vector) = (0, 1, 0, 1)
        
        [Header(Fresnel Mask)]
        [Tooltip(Edge glow effect based on view angle. Brighter at glancing angles.)]
        [Toggle(_FRESNEL_MASK)] _FresnelMask("Fresnel Mask", Float) = 0
        [ShowIf(_FRESNEL_MASK)] 
        [Tooltip(Higher values create a tighter edge effect)]
        _FresnelMaskPower("Fresnel Mask Power", Range(0, 10)) = 1
        
        // Depth soft-blending
        [SectionHeader(Depth Blending)]
        [Tooltip(Fades particles near opaque geometry to avoid hard intersections)]
        [Toggle(_DEPTH_BLENDING)] _DepthBlending("Depth Blending", Float) = 0
        [ShowIf(_DEPTH_BLENDING)] 
        [Tooltip(Distance over which the fade occurs)]
        _DepthBlendDistance("Depth Blend Distance", Range(0, 10)) = 1.0
        
        // Outline
        [SectionHeader(Outline)]
        [Toggle(_ENABLE_OUTLINES)] _EnableOutlines("Enable Outlines", Float) = 0
        [ShowIf(_ENABLE_OUTLINES, Toggle)] [Tooltip(If enabled outlines are forced to be opaque. Otherwise they match blend modes)]
        _OutlinesAreOpaque("Outline Are Opaque", Int) = 1
        [ShowIf(_ENABLE_OUTLINES)] [HDR] _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        [ShowIf(_ENABLE_OUTLINES)] [HDR] _OutlineGlow("Outline Glow", Color) = (0, 0, 0, 1)
        [ShowIf(_ENABLE_OUTLINES)] [Enum(Uniform, 0, Normals, 1)] _OutlinesScaleMode("Outline Scale", Int) = 0
        [ShowIf(_ENABLE_OUTLINES)] _OutlineOffsetScale("Outline Offset (xyz: Local Space, w: scale)", Vector) = (0, 0, 0, 1)
        [ShowIf(_ENABLE_OUTLINES)] [Enum(UnityEngine.Rendering.CullMode)] _OutlineCulling("Outline Cull Mode", Int) = 1

        
        // Culling / Blending options
        [SectionHeader(Culling and Blending)]
        [Enum(UnityEngine.Rendering.CullMode)] _Culling("Cull Mode", Int) = 2
        
        [BlendMode] _BlendMode ("Blend Mode", Int) = 1
        [HideInInspector] _AlphaMultiplicationMode("__alphaMultMode", Int) = 1
        [HideInInspector] _SrcBlend("__src", Int) = 5
        [HideInInspector] _DstBlend("__dst", Int) = 10
        
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
        
        Blend 0 [_SrcBlend] [_DstBlend]
        Blend 1 One One // Glow target is additively blended!!!
        Cull Off // NOTE: We perform manual culling in the frag shader for outlines to have diff culling modes in 1 pass
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
        #pragma shader_feature _PARTICLE_SYSTEM
        
        #pragma shader_feature_local _IS_FLIPBOOK
        
        #pragma shader_feature_local _SECONDARY_TEXTURE
        
        #pragma shader_feature_local _POLAR_COORDS
        #pragma shader_feature_local _SWIRL
        
        #pragma shader_feature_local _DISTORTION

        #pragma shader_feature_local _ALPHA_CUTOFF_MODIFIES_COLOR
        
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

        #pragma shader_feature_local _ENABLE_OUTLINES
        #pragma multi_compile _ _OUTLINES_PASS

#if defined(_ENABLE_OUTLINES) && defined(_OUTLINES_PASS)
        #define _RENDER_OUTLINES
#endif


        // ---------------------------------

CBUFFER_START(UnityPerMaterial)        
        // Main Textures
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_ST;
        float4 _MainTexProperties; // XY Panning | ZW Contrast/Power
        float _MainTexClampU; // (NEW)
        float _MainTexClampV; // (NEW)
        float _MainTexRotation; // Texture Rotation (Degrees)

        // Flipbook Params (NEW)
//#ifndef _PARTICLE_SYSTEM        
        int _FlipBookWidth;
        int _FlipBookHeight;
        float _FlipBookSpeed;
        float _ManualFlipBookAnimation;
        int _FlipBookStartFrame;
//#endif
        float _FlipBookFrameBlending;

        // Secondary texture blending
        TEXTURE2D(_SecondaryTex);
        SAMPLER(sampler_SecondaryTex);
        float4 _SecondaryTex_ST;
        float4 _SecondaryTexProperties; // XY Panning | ZW Contrast/Power
        float _SecondaryTexRotation; // Texture Rotation (Degrees)

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
        float _ColorBlendOffset; // Gradient Offset

        float _ColorCutoff;

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
        float _EmissionRangeMin;
        float _EmissionRangeMax;
        float _EmissionSoftness;
        
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
        float _AlphaCutoffInfluencesColor;

        int _VertexColorMode; // Vertex Color Usage Mode

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

        // Outline Settings
        int _OutlinesAreOpaque;
        float4 _OutlineColor;
        float4 _OutlineGlow;
        float4 _OutlineOffsetScale;
        int _OutlinesScaleMode;
        int _OutlineCulling;

        // Culling option
        int _Culling;
        
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

        float4 ProcessColor(float4 col, float contrast, float power)
        {
            col = saturate(lerp(0.5, col, contrast)) * power;
            return col;
        }

        // Color blend shifts based on alpha cutoff, basically remapping the range from [0, 1] to [_AlphaCutoff, _ColorCutoff].
        float GetColorBlend(float grayscaleValue)
        {
	        float cutoff = lerp(_AlphaCutoff, 2, _ColorCutoff);
	        return smoothstep(_AlphaCutoff, cutoff, grayscaleValue);	
        }

        float2 RotateUVs(in float2 uv, float rotationDegrees)
        {
            float cosAngle = cos(radians(rotationDegrees));
            float sinAngle = sin(radians(rotationDegrees));
            float2x2 rotationMatrix = float2x2(
                cosAngle, -sinAngle,
                sinAngle, cosAngle
            );
            // translate uv to be centered
            uv = uv - 0.5;
            uv = mul(rotationMatrix, uv);
            // translate back
            return uv + 0.5;
        }

        // Value in x, alpha in y
        float2 SampleTex(Texture2D tex, SamplerState sampler_tex, float2 uv, float4 tex_ST, float4 props, float2 uvClamp = 0, float rotation = 0)
        {
            float2 timeOffset = float2(GetTime(props.x), GetTime(props.y));
            uv = RotateUVs(uv, rotation);
            uv = TRANSFORM_TEX(uv, tex) + timeOffset;
            uv.x = lerp(uv.x, clamp(uv.x, -1, 1), uvClamp.x);
            uv.y = lerp(uv.y, clamp(uv.y, -1, 1), uvClamp.y);
#ifdef _POLAR_COORDS            
            float4 col = SAMPLE_TEXTURE2D_GRAD(tex, sampler_tex, uv, 0, 0);
#else            
            float4 col = SAMPLE_TEXTURE2D(tex, sampler_tex, uv);
#endif
            col = ProcessColor(col, props.z, props.w);
            return float2(col.r, col.a);
        }

        float4 SampleTex(Texture2D tex, SamplerState sampler_tex, float2 uv, float4 tex_ST)
        {
            uv = TRANSFORM_TEX(uv, tex);
            return SAMPLE_TEXTURE2D(tex, sampler_tex, uv);
        }

        float2 SampleTextureFlipBook(TEXTURE2D_PARAM(tex, sampler_tex), float2 uv, float4 props)
        {
#ifdef _DISTORTION            
            uv = DistortionUV(uv, TEXTURE2D_ARGS(_DistortionGuide, sampler_DistortionGuide), _DistortionGuide_ST, _DistortionAmount, float2(GetTime(_DistortionSpeedX), GetTime(_DistortionSpeedY)));
#endif

            float frameCount = _FlipBookWidth * _FlipBookHeight;
            float animPct = lerp(_Time.y * _FlipBookSpeed, 0, _ManualFlipBookAnimation) + _FlipBookStartFrame / (frameCount);

            float4 col = SampleFlipBook(TEXTURE2D_ARGS(tex, sampler_tex), uv, float2(_FlipBookWidth, _FlipBookHeight), animPct, _FlipBookFrameBlending);
            col = ProcessColor(col, props.z, props.w);
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

        // Vertex color interpretation based on mode
        // Mode 0: Tint and Fade         - RGB tint, A transparency
        // Mode 1: Tint and Dissolve     - RGB tint, A dissolve, no transparency control
        // Mode 2: Transparency Control  - No tint, R transparency, no dissolve
        // Mode 3: Transparency and Dissolve - No tint, R transparency, A dissolve
        void GetVertexColorContributions(float4 color, int mode, out float3 tint, out float transparency, out float dissolveOffset)
        {
            bool useRedForTransparency = (mode >= 2);
            bool useAlphaForDissolve = (mode == 1 || mode == 3);
            
            tint = useRedForTransparency ? float3(1,1,1) : color.rgb;
            transparency = useRedForTransparency ? color.r : (useAlphaForDissolve ? 1.0 : color.a);
            dissolveOffset = useAlphaForDissolve ? (1.0 - color.r) : 0.0;
        }
        
        ENDHLSL
        
        Pass
        {
            Name "VFX Main"
            Tags
            {
                "LightMode"="VFX"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

#ifdef _PARTICLE_SYSTEM
            #define _PS_AGE_PERCENT(input) input.uv.z
            #define _PS_INTENSITY(input) input.customData1.x
            #define _PS_ALPHA_CUTOFF(input) input.customData1.y
            #define _PS_COLOR_GRADIENT_OFFSET(input) input.customData1.z
            #define _PS_EMISSION_INTENSITY(input) input.customData1.w
            #define _PS_MAINTEX_ST(input) input.customData2.xyzw
#else
            #define _PS_AGE_PERCENT(input) 0
            #define _PS_INTENSITY(input) 1 // multiply
            #define _PS_ALPHA_CUTOFF(input) 0 // add
            #define _PS_COLOR_GRADIENT_OFFSET(input) 0 // add 
            #define _PS_EMISSION_INTENSITY(input) 1 // multiply
            #define _PS_MAINTEX_ST(input) float4(1,1,0,0)
#endif

            // CustomData1.xyzw
            #define _INTENSITY(input) _Intensity * _PS_INTENSITY(input)
            #define _ALPHA_CUTOFF(input) _AlphaCutoff + _PS_ALPHA_CUTOFF(input)
            #define _COLOR_GRADIENT_OFFSET(input) _ColorBlendOffset + _PS_COLOR_GRADIENT_OFFSET(input)
            #define _EMISSION_INTENSITY(input) _EmissionIntensity * _PS_EMISSION_INTENSITY(input)

            // CustomData2.xyzw TODO: Some particle system stuff might need some work and isnt fully finished, not well tested atm and might change HEADS UP!
            #define _MAINTEX_ST(input) float4(_MainTex_ST.xy * _PS_MAINTEX_ST(input).xy, _MainTex_ST.zw + _PS_MAINTEX_ST(input).zw)
            
            struct VertexData
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR; // Vertex color for additional effects
                float4 uv : TEXCOORD0;

            #ifdef _PARTICLE_SYSTEM
                float4 customData1 : TEXCOORD1; // Color/Alpha Controls (x: Intensity / y: Alpha Cutoff / z: Color Offset / w: EmissionIntensity)
                float4 customData2 : TEXCOORD2; // UV Main Tex Controls (ST [xyzw])
            #endif
            };

            struct Interpolators
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float3 normalWS : NORMAL;
                float4 uv : TEXCOORD0;

            #ifdef _PARTICLE_SYSTEM
                float4 customData1 : TEXCOORD1; // Color/Alpha Controls (x: Intensity / y: Alpha Cutoff / z: Color Offset / w: EmissionIntensity)
                float4 customData2 : TEXCOORD2; // UV Main Tex Controls (ST [xyzw])
            #endif

                float4 screenPos : TEXCOORD3;
                float3 viewDir : TEXCOORD4; // View direction for lighting calculations & fresnel
            };

            struct PixelOut
            {
                float4 color : SV_Target0;
                float4 glow : SV_Target1;
            };

#if !defined(_ENABLE_OUTLINES) && defined(_OUTLINES_PASS)
            Interpolators vert(VertexData i) { return (Interpolators)0; }
            PixelOut frag(Interpolators i) { discard; return (PixelOut)0; }
#else
            Interpolators vert(VertexData i)
            {
                Interpolators o = (Interpolators)0;

#ifdef _RENDER_OUTLINES
                float scale = _OutlineOffsetScale.w;
                if (_OutlinesScaleMode == 0) i.positionOS.xyz = mul(float3x3(scale, 0, 0, 0, scale, 0, 0, 0, scale), i.positionOS.xyz);
                else i.positionOS.xyz += normalize(i.normalOS) * scale;
                i.positionOS.xyz += _OutlineOffsetScale;
#endif
                
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
                o.screenPos = ComputeScreenPos(o.vertex);
                o.viewDir = GetWorldSpaceViewDir(TransformObjectToWorld(i.positionOS));

#ifdef _PARTICLE_SYSTEM
                o.customData1 = i.customData1; // Pass custom data through
                o.customData2 = i.customData2;
#endif
                
                return o;
            }

            PixelOut frag(Interpolators i, bool isFrontFace : SV_IsFrontFace)
            {
                ////////////////////////////////////////
                /// Manual Culling (So Outlines & Regular can have different culling modes
                ////////////////////////////////////////
#ifdef _RENDER_OUTLINES
                    // Manual cull for outlines: 0=Off, 1=Front, 2=Back
                    if (_OutlineCulling == 1 && isFrontFace) discard;
                    if (_OutlineCulling == 2 && !isFrontFace) discard;
#else
                    // Manual cull for main: 0=Off, 1=Front, 2=Back  
                    if (_Culling == 1 && isFrontFace) discard;
                    if (_Culling == 2 && !isFrontFace) discard;
#endif
                
                PixelOut o = (PixelOut)0;

                i.normalWS = normalize(i.normalWS);
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // Prepare UV for primary texture sampling
                float2 uv = i.uv.xy;

                float3 vertexTint;
                float vertexTransparency, vertexDissolveOffset;
                GetVertexColorContributions(i.color, _VertexColorMode, vertexTint, vertexTransparency, vertexDissolveOffset);

                float4 colorTint = _ColorTint * lerp(_BackFaceTint, 1, isFrontFace);
                
#if defined(_IS_FLIPBOOK) && !defined(_PARTICLE_SYSTEM)
                float2 flipBookUVs = uv;
                ManipulateUVs(uv); // UVs werent manipulated for
                float2 texVal = SampleTextureFlipBook(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), flipBookUVs, float4(0, 0, _MainTexProperties.zw));
#else
                ManipulateUVs(uv);
                float2 texVal = SampleTex(_MainTex, sampler_MainTex, uv, _MAINTEX_ST(i), _MainTexProperties, float2(_MainTexClampU, _MainTexClampV), _MainTexRotation);
#endif                 


#ifdef _SECONDARY_TEXTURE
                texVal = BlendTextures(texVal, SampleTex(_SecondaryTex, sampler_SecondaryTex, uv, _SecondaryTex_ST, _SecondaryTexProperties, 0, _SecondaryTexRotation));
#endif

                float value = texVal.x;
                float alpha = saturate(texVal.y);

                // Masking
                float mask = 1;
                float2 rawUV = i.uv.xy;
#ifdef _MASK_TEXTURE
                float2 maskUV = TRANSFORM_TEX(rawUV, _MaskTex);
                mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV).a;
                ApplyMask(alpha, mask);
#endif

#ifdef _CIRCLE_MASK
                float circle = distance(frac(rawUV), float2(0.5, 0.5));
                mask = 1 - smoothstep(_CircleOuterRadius, _CircleOuterRadius + _CircleMaskSmoothness, circle);
                mask *= smoothstep(_CircleInnerRadius, _CircleInnerRadius + _CircleMaskSmoothness, circle);
                ApplyMask(alpha, mask);
#endif

#ifdef _RECT_MASK
                float2 rectUV = (frac(rawUV) * 2 - 1);
                float rect = max(abs(rectUV.x / _RectWidth), abs(rectUV.y / _RectHeight));
                mask = 1 - smoothstep(_RectCutoff, _RectCutoff + _RectSmoothness, rect);
                ApplyMask(alpha, mask);
#endif

#ifdef _GRADIENT_MASK
                mask = smoothstep(_GradientMaskProperties.y, _GradientMaskProperties.x, frac(rawUV).x);
                mask *= smoothstep(_GradientMaskProperties.w, _GradientMaskProperties.z, frac(rawUV).y);
                ApplyMask(alpha, mask);
#endif                 
                
#ifdef _FRESNEL_MASK
                mask = pow(saturate(dot(normalize(i.viewDir), i.normalWS)), _FresnelMaskPower);
                ApplyMask(alpha, mask);
#endif

                float cutoff = saturate(_ALPHA_CUTOFF(i) + vertexDissolveOffset);// + _PS_AGE_PERCENT(i));
                if (alpha <= cutoff) discard;

                float alphaShape = smoothstep(cutoff, saturate(cutoff + _AlphaCutoffSmoothness), alpha);

                // Declare early just for render outlines compilation
                float3 col = vertexTint * colorTint.rgb;
                float4 colorMapVal = 0;

#ifndef _RENDER_OUTLINES                
                // Banding
                value += _COLOR_GRADIENT_OFFSET(i); // Need an option to shift value based on cutoff
    #ifdef _COLOR_BANDING
                value = round(value * _Bands) / _Bands;
    #endif

                value = pow(value, _ValuePower);

    #ifdef _ALPHA_CUTOFF_MODIFIES_COLOR                
                float gradientVal = GetColorBlend(saturate(value));
    #else                
                float gradientVal = saturate(value);// + _ColorBlendOffset);
    #endif                

    #ifdef _USE_GRADIENT_MAP
                colorMapVal = SAMPLE_TEXTURE2D(_GradientMap, sampler_GradientMap, float2(gradientVal, 0));
    #else
                // Color value sample shifts based on cutoff
                colorMapVal = lerp(_LowColor, _HighColor, gradientVal);
    #endif

                col.rgb *= colorMapVal.rgb;

    #ifdef _LIGHTING
                Light L = GetMainLight();
                float NoL = saturate(dot(i.normalWS, normalize(L.direction)));
                NoL = step(0.5, NoL);
                col.rgb *= max(1 - _ShadowStrength, NoL);
    #endif
#endif
                
#ifdef _DEPTH_BLENDING
                float depth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float depthDelta = saturate(_DepthBlendDistance * (depth - i.screenPos.w));
                alphaShape *= depthDelta;
                alpha *= depthDelta; // Preserve for burn edge calculation at depth intersections
#endif                

#ifndef _RENDER_OUTLINES                
    #ifdef _BURN_COLORS
                float edgeDistance = alpha - cutoff;
                float burnMask = smoothstep(_BurnSize, _BurnSize - _BurnSoftness, edgeDistance);
                col.rgb = lerp(col.rgb, _BurnColor, burnMask);
    #endif

    #ifdef _SCREEN_SPACE_TEXTURE
                screenUV.x *= _ScreenParams.x / _ScreenParams.y; // Maintain aspect ratio
                col.rgb += SAMPLE_TEXTURE2D(_ScreenSpaceTex, sampler_ScreenSpaceTex, screenUV * _ScreenSpaceTex_ST.xy).rgb * _ScreenSpaceTexTint.rgb;
    #endif
#endif
                
                // ============================================================================
                // FINAL ALPHA & OUTPUT
                // ============================================================================
                // 
                // Premultiplied Approach (One, OneMinusSrcAlpha):
                //   - RGB is multiplied by shapeAlpha only (texture/dissolve edge)
                //   - fadeAlpha (vertex color, tint alpha) only affects the alpha channel
                //   - Result: colors stay bright while fading, edges blend correctly
                //
                // Standard Alpha Approach (SrcAlpha, OneMinusSrcAlpha):
                //   - RGB is output as-is, blend hardware does the multiplication
                //   - Mathematically equivalent to multiplying RGB by full finalAlpha as we previously did for premultiplied
                //     with (finalColor.rgb * finalColor.a, finalColor.a), now we just multiply by textures alpha and colors control
                //     additive/traditional blending
                //   - Result: traditional transparency, colors darken as they fade
                // ============================================================================
                
                float shapeAlpha = alphaShape;
                float fadeAlpha = vertexTransparency * colorTint.a * colorMapVal.a;
                // Make sure Alpha is [0,1] otherwise the blending causes bad color distortion in the BG (i.e negative colors)
                float finalAlpha = saturate(shapeAlpha * fadeAlpha * _INTENSITY(i));

#ifdef _RENDER_OUTLINES
                // Outline pass - flat color
                float3 outlineRGB = _OutlineColor.rgb * _INTENSITY(i);
                if (_OutlinesAreOpaque == 1) o.color = float4(outlineRGB, 1);
                else
                {
                    if (IS_PREMULTIPLIED)
                    {
                        outlineRGB *= shapeAlpha;
                    }
                    o.color = float4(outlineRGB, finalAlpha * _OutlineColor.a);
                }
                o.glow = _OutlineGlow; // NOTE: multiply by final alpha?
                return o;
#endif
                
                float3 finalRGB = col.rgb * _INTENSITY(i);

                if (IS_PREMULTIPLIED)
                {
                    finalRGB *= shapeAlpha;
                }

                o.color = float4(finalRGB, finalAlpha);
                o.glow = _EmissionColor * _EMISSION_INTENSITY(i) * o.color.a;
                
                // Custom emission to apply either everywhere, to dark regions, or to bright regions
                float colorLuma = saturate(max(o.color.r, max(o.color.g, o.color.b)));
                // _EmissionRangeMin = 0.0 to 1.0 (lower bound)
                // _EmissionRangeMax = 0.0 to 1.0 (upper bound)  
                // _EmissionSoftness = edge falloff
                float glowMask = smoothstep(_EmissionRangeMin - _EmissionSoftness, _EmissionRangeMin + _EmissionSoftness, colorLuma)
                               * (1 - smoothstep(_EmissionRangeMax - _EmissionSoftness, _EmissionRangeMax + _EmissionSoftness, colorLuma));
                o.glow *= glowMask;
                
#ifdef _EMISSION_TEXTURE
                o.glow *= SampleTex(_EmissionTex, sampler_EmissionTex, uv, _EmissionTex_ST);
#endif

                return o;
            }
#endif
            
            ENDHLSL
        }
        // TODO: Support depth writing for SSR like effects to work
//        Pass
//        {
//            Name "Depth"
//            Tags {"LightMode"="DepthOnly"}
//            
//            ZWrite [_ZWrite]
//            ColorMask R
//            
//            HLSLPROGRAM
//            #pragma vertex DepthOnlyVertex
//            #pragma fragment DepthOnlyFragment
//            
//            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
//            ENDHLSL
//        }
        Pass
        {
            // Required for proper thumbnail rendering in the editor, otherwise blank (UniversalForward pass needed)
            Name "Preview"
            Tags { "LightMode"="UniversalForward" }
            
            ColorMask 0  // Don't write to any color channels
            Blend Zero One
            
            HLSLPROGRAM
            ENDHLSL
        }
    }
    //CustomEditor "FS.Editor.VFXShaderEditor" TODO: editor needs a bit more work before we can use it reliably, some issues with tooltips not working, values not updating after changed (visually)
}