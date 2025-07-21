/// ----- MeshFX Shader -----
/// Parameters of this shader and when they are used & what they are:
/// _________________________
/// [ SETTINGS ]
/// - _GRAYSCALE_IS_ALPHA : If enabled, the alpha channel of the BaseTexture is the grayscale value.
/// - _USE_GRADIENT_TEXTURE : If enabled, a custom gradient texture is used instead of blending between Low Color and High Color.
/// - _SEPARATE_EROSION_TEXTURE : If enabled, a separate texture is used for erosion effects.
/// - _USE_MASK_TEXTURE : If enabled, the base texture gets multiplied w/ this mask texture.
/// - _FX_HAS_OUTLINES : If enabled, another shader pass is used to render inverse hull outlines
/// - _VERTEX_DISPLACEMENT : If enabled, vertex displacement is applied to the mesh based on custom vertex displacement data
/// - _COLOR_BANDING : If enabled, we posterize the color values
///
/// [ COORDINATES ]
/// - _POLAR_COORDS: If enabled, the mesh uses polar coordinates for UV mapping.
///
/// [ TEXTURES / SHAPE ] 
/// - BaseTexture: The main texture applied to the mesh, graycale or alpha used as 'alpha' channel based on setting
/// - SecondaryTexture: Optional secondary texture that gets multiplied with the BaseTexture.
/// - BaseTexture_Panning : Controls the panning of the BaseTexture [speedX, speedY]
/// - SecondaryTexture_Panning : Controls the panning of the SecondaryTexture [speedX, speedY]
///
/// [ COLORS ]
/// - Low Color & High Color : Blended between based on grayscale value of BaseTexture
/// - Backface Tint : Tint applied to the backfaces of the mesh.
/// - ColorGradient : Used instead of the above to pass a custom gradient texture [TOGGLE(_USE_GRADIENT_TEXTURE)]
/// - Posterization Steps : Number of color bands [TOGGLE(_COLOR_BANDING)]
///
/// [ ALPHA CONTROLS ]
/// - Intensity : Just multiplies the texture sample by this value (note w/ premul black & zero alpha is not visible)
/// - AlphaInfluence : Lerp factor of lerp(originalAlpha, 1, AlphaInfluence) to control how much the alpha channel affects the final color.
/// - Alpha Cutoff : Threshold for alpha cutoff, below this value the pixel is discarded (0-1 range).
///
/// [ GLOW ]
/// - Glow Color: HDR color used to write to the glow buffer.
/// - Glow Intensity: Controls the intensity of the glow effect.
///
///
///

Shader "VFX/MeshFX"
{
    Properties
    {
        [SectionHeader(Settings)]
        
        [Toggle(_BILLBOARD)] _Billboard ("Billboard", Float) = 0
        
        // Blend mode (Opaque, Premultiplied)
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendMode ("Blend Mode", Float) = 0 // 0: Opaque, 1: Alpha, 2: Additive, etc.
        [Enum(UnityEngine.Rendering.CullMode)] _Culling ("Cull Mode", Int) = 2

        // ---------------------------------------------------------------
        
        [SectionHeader(Textures)]

        [MainTexture] _BaseTex("Base Texture", 2D) = "white" {}
        [Toggle(_USE_SECONDARY_TEXTURE)] _UseSecondaryTexture("Use Secondary Texture", Float) = 0
        [ShowIf(_USE_SECONDARY_TEXTURE)] _SecondaryTex("Secondary Texture", 2D) = "white" {}
        
        // IsFlipbook
        // Vector2: X = Rows, Y = Columns
        // Vector2: Z = Start Frame, W = End Frame
        // Toggle: _MANUAL_FLIPBOOK
        // Int: Current Frame
        [Header(Flipbook)]
        [Toggle(_IS_FLIPBOOK)] _IsFlipbook("Is Flipbook", Float) = 0
        [ShowIf(_IS_FLIPBOOK)] _FlipbookRows("Flipbook Rows", Range(1, 16)) = 1
        [ShowIf(_IS_FLIPBOOK)] _FlipbookColumns("Flipbook Columns", Range(1, 16)) = 1
        [ShowIf(_IS_FLIPBOOK)] _FlipbookFrameRate("Flipbook Frame Rate", Range(1, 60)) = 30
        [ShowIf(_IS_FLIPBOOK)] _FlipbookStartFrame("Flipbook Start Frame", Range(0, 255)) = 0
        
        // ---------------------------------------------------------------
        
        [SectionHeader(Coordinates)]
        [Header(Settings)]
        [Toggle(_POLAR_COORDS)] _PolarCoords("Polar Coordinates", Float) = 0

        [Space(10)]
        
        [Toggle] _BaseClampU("Base Texture Clamp U", Float) = 0
        [Toggle] _BaseClampV("Base Texture Clamp V", Float) = 0
        [ShowIf(_USE_SECONDARY_TEXTURE)] [Toggle] _SecondaryClampU("Secondary Texture Clamp U", Float) = 0
        [ShowIf(_USE_SECONDARY_TEXTURE)] [Toggle] _SecondaryClampV("Secondary Texture Clamp V", Float) = 0
        
        [Header(Panning)]
        _BaseTex_Panning("Base Texture Panning", Vector) = (0, 0, 0, 0)
        [ShowIf(_USE_SECONDARY_TEXTURE)] _SecondaryTex_Panning("Secondary Texture Panning", Vector) = (0, 0, 0, 0)
        
        // ---------------------------------------------------------------

        [SectionHeader(Primary Colors)]
        
        _ColorCutoff("Color Cutoff", Range(0, 1)) = 0.5
        [Toggle(_USE_GRADIENT_MAP)] _UseGradientMap("Use Gradient Map", Float) = 0
        [ShowIf(_USE_GRADIENT_MAP)] [NoScaleOffset] _GradientMap("Gradient Map", 2D) = "white" {}
        
        [HideIf(_USE_GRADIENT_MAP)] [HDR] _HighColor("High Color", Color) = (1, 1, 1, 1)
        [HideIf(_USE_GRADIENT_MAP)] [HDR] _LowColor("Low Color", Color) = (0, 0, 0, 1)
        
        _BackFaceTint("Backface Tint", Color) = (1, 1, 1, 1)

        [Header(Color Grading)]
        _ColorPower("Color Power", Range(0, 2)) = 1
        _Contrast("Contrast", Range(0, 1)) = 0
        
        [Header(Color Banding)]
        
        [Toggle(_COLOR_BANDING)] _ColorBanding("Color Banding", Float) = 0
        [ShowIf(_COLOR_BANDING)] [IntRange] _NumColorBands("Bands Count", Range(1, 24)) = 8

        // ---------------------------------------------------------------

        [SectionHeader(Alpha Controls)]
        
        _Intensity("Intensity", Range(0, 10)) = 1.0
        _AlphaInfluence("Alpha Influence", Range(0, 10)) = 1
        _AlphaCutoff("Alpha Cutoff", Range(0, 1)) = 0
        _AlphaCutoffSmoothness("Alpha Cutoff Smoothness", Range(0, 0.5)) = 0.5

        // ---------------------------------------------------------------
        
        [SectionHeader(Emission Colors)]
        
        _GlowIntensity("Glow Intensity", Range(0, 10)) = 1.0
        [HDR] _GlowColor("Glow Color", Color) = (1, 1, 1, 1)
        
        // ---------------------------------------------------------------
        
        [SectionHeader(Burn Colors)]
        
        [Toggle(_EROSION_BURN)] _ErosionBurn("Use Burn Colors", Float) = 0
        [ShowIf(_EROSION_BURN)] [HDR] _BurnColor("Burn Color", Color) = (1, 0.5, 0, 1)
        [ShowIf(_EROSION_BURN)] _BurnSize("Burn Size", Range(0, 1)) = 1.0
        [ShowIf(_EROSION_BURN)] _BurnSmoothness("Burn Smoothness", Range(0, 1)) = 1.0

        // ---------------------------------------------------------------

        [SectionHeader(Mask)]
        
        [Toggle(_USE_MASK_TEXTURE)] _UseMaskTexture("Use Mask Texture", Float) = 0
        [ShowIf(_USE_MASK_TEXTURE)] _MaskTex("Mask Texture", 2D) = "white" {}

        // ---------------------------------------------------------------

        [SectionHeader(Distortion)]
        
        [Toggle(_USE_DISTORTION_TEXTURE)] _UseDistortionTexture("Use Distortion Texture", Float) = 0
        [ShowIf(_USE_DISTORTION_TEXTURE)] [Toggle(_DISTORTION_USES_POLAR_COORDS)] _DistortionUsesPolarCoords("Distortion Uses Polar Coordinates", Float) = 0
        [ShowIf(_USE_DISTORTION_TEXTURE)] _DistortionTex("Distortion Texture", 2D) = "white" {}
        [ShowIf(_USE_DISTORTION_TEXTURE)] _DistortionPow("Distortion Strength", Float) = 0.1
        [ShowIf(_USE_DISTORTION_TEXTURE)] _DistortionPanning("Distortion Panning", Vector) = (0,0,0,0)
        
        // ---------------------------------------------------------------
        
        [SectionHeader(Depth)]
        [Toggle(_DEPTH_FADEOUT)] _DepthFadeout("Depth Fadeout", Float) = 0
        [ShowIf(_DEPTH_FADEOUT)] _DepthFadeoutDistance("Fadeout Distance", Range(0, 10)) = 10.0
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
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "MeshVFX.hlsl"

        #pragma shader_feature_local_vertex _BILLBOARD
        #pragma shader_feature_local_fragment _INVERSE_HULL_OUTLINES

        #pragma shader_feature_local_fragment _USE_SECONDARY_TEXTURE

        #pragma shader_feature_local_fragment _POLAR_COORDS

        #pragma shader_feature_local_fragment _USE_GRADIENT_MAP
        #pragma shader_feature_local_fragment _COLOR_BANDING

        #pragma shader_feature_local_fragment _EROSION_BURN

        #pragma shader_feature_local_fragment _USE_MASK_TEXTURE

        #pragma shader_feature_local_fragment _USE_DISTORTION_TEXTURE
        #pragma shader_feature_local_fragment _DISTORTION_USES_POLAR_COORDS

        #pragma shader_feature_local _DEPTH_FADEOUT

        ENDHLSL
        
//        Pass
//        {
//            Name "DepthOnly"
//            Tags
//            {
//                "LightMode"="DepthOnly"
//            }
//            ZWrite On
//            ColorMask R
//            
//            
//            HLSLPROGRAM
//            #pragma vertex FXVertex
//            #pragma fragment FX_Main
//            ENDHLSL
//        }

        Pass
        {
            Name "MeshFX"
            Tags
            {
                "LightMode"="Glow"
            }

            Cull Off
            //ZWrite Off
            //ZTest LEqual
            Blend One OneMinusSrcAlpha
            //Blend [_BlendMode]

            HLSLPROGRAM
            #pragma vertex FXVertex
            #pragma fragment FX_Main
            ENDHLSL
        }
    }
}