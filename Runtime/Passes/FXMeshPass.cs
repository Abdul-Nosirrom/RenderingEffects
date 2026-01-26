using System.Collections.Generic;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FS.Rendering
{
    public static class FSRenderUtils
    {
        // Stolen from Unitys RenderUtils because it has this func but its internal so wipiee
        private static ShaderTagId[] s_ShaderTagValues = new ShaderTagId[1];
        private static RenderStateBlock[] s_RenderStateBlocks = new RenderStateBlock[1];
        // Create a RendererList using a RenderStateBlock override is quite common so we have this optimized utility function for it
        public static RendererListHandle CreateRendererListWithRenderStateBlock(RenderGraph renderGraph, ref CullingResults cullResults, DrawingSettings ds, FilteringSettings fs, RenderStateBlock rsb)
        {
            s_ShaderTagValues[0] = ShaderTagId.none;
            s_RenderStateBlocks[0] = rsb;
            
            // NativeArray from managed array doesn't allocate, just wraps
            NativeArray<ShaderTagId> tagValues = new NativeArray<ShaderTagId>(s_ShaderTagValues, Allocator.Temp);
            NativeArray<RenderStateBlock> stateBlocks = new NativeArray<RenderStateBlock>(s_RenderStateBlocks, Allocator.Temp);
            
            var param = new RendererListParams(cullResults, ds, fs)
            {
                tagValues = tagValues,
                stateBlocks = stateBlocks,
                isPassTagName = false
            };
            return renderGraph.CreateRendererList(param);
        }

        public static RendererListHandle CreateRendererListWithRenderStateBlock(RenderGraph renderGraph, ref CullingResults cullResults, DrawingSettings ds, FilteringSettings fs, RenderStateBlock rsb, ShaderTagId shaderTagName, ShaderTagId shaderTagValue)
        {
            s_ShaderTagValues[0] = shaderTagValue;
            s_RenderStateBlocks[0] = rsb;
            
            // NativeArray from managed array doesn't allocate, just wraps
            NativeArray<ShaderTagId> tagValues = new NativeArray<ShaderTagId>(s_ShaderTagValues, Allocator.Temp);
            NativeArray<RenderStateBlock> stateBlocks = new NativeArray<RenderStateBlock>(s_RenderStateBlocks, Allocator.Temp);
            
            var param = new RendererListParams(cullResults, ds, fs)
            {
                tagName = shaderTagName,
                tagValues = tagValues,
                stateBlocks = stateBlocks,
                isPassTagName = true
            };
            return renderGraph.CreateRendererList(param);
        }
        
        public static RendererListHandle CreateRendererListHandle(RenderGraph renderGraph, UniversalCameraData camData, UniversalRenderingData renderData, UniversalLightData lightData, List<ShaderTagId> shaderTagIds, SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent, RenderQueueRange? renderQueue = null)
        {
            renderQueue ??= RenderQueueRange.transparent;
            DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(shaderTagIds, renderData,
                camData,
                lightData, sortingCriteria); //camData.defaultOpaqueSortFlags);
            FilteringSettings filterSettings = FilteringSettings.defaultValue;
            filterSettings.renderQueueRange = renderQueue.Value;
            RendererListParams renderingParams =
                new RendererListParams(renderData.cullResults, drawSettings, filterSettings);

            return renderGraph.CreateRendererList(renderingParams);
        }
    }
    
    public class FXMeshPass : ScriptableRenderPass
    {
        private readonly List<ShaderTagId> k_ShaderTagId = new (){ new ShaderTagId("VFX") }; 
        private readonly GlobalKeyword _OUTLINES_PASS;

        private readonly RenderStateBlock m_outlinesStateBlock;

        public FXMeshPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            _OUTLINES_PASS = GlobalKeyword.Create("_OUTLINES_PASS");
            
            // Init Draw params and shit
            m_outlinesStateBlock = new RenderStateBlock(RenderStateMask.Depth)
            {
                depthState = new DepthState(false, CompareFunction.LessEqual)
            };
        }
        
        public class FXMeshData
        {
            public RendererListHandle m_rendererList;
            public RendererListHandle m_outlineRendererList;
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var renderData = frameData.Get<UniversalRenderingData>();
            var camResources = frameData.Get<UniversalResourceData>();
            var camData = frameData.Get<UniversalCameraData>();
            var fxData = frameData.Get<FXData>();
            var lightData = frameData.Get<UniversalLightData>();

            using var builder = renderGraph.AddRasterRenderPass<FXMeshData>("FX Mesh Pass", out var passData);
            
            // Create renderer list
            {
                DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(k_ShaderTagId, renderData,
                    camData,
                    lightData, SortingCriteria.CommonTransparent); //camData.defaultOpaqueSortFlags);
                FilteringSettings filterSettings = FilteringSettings.defaultValue;
                filterSettings.renderQueueRange = RenderQueueRange.transparent;
                RendererListParams renderingParams =
                    new RendererListParams(renderData.cullResults, drawSettings, filterSettings);

                //renderingParams.tagValues NOTE: This is how we set these for custom shaderlab tags [e.g "SomeCoolFeature": "On"
                passData.m_outlineRendererList = FSRenderUtils.CreateRendererListWithRenderStateBlock(renderGraph, ref renderData.cullResults, drawSettings, filterSettings, m_outlinesStateBlock);
                passData.m_rendererList = renderGraph.CreateRendererList(renderingParams);
                builder.UseRendererList(passData.m_outlineRendererList);
                builder.UseRendererList(passData.m_rendererList);
            }


            //builder.SetRenderAttachment(fxData.m_bloomTexture, 0);
            builder.SetRenderAttachment(camResources.activeColorTexture, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(camResources.activeDepthTexture, AccessFlags.ReadWrite);
            builder.SetRenderAttachment(fxData.m_bloomTexture, 1);

            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true); // still dk what this is


            builder.SetRenderFunc<FXMeshData>(ExecutePass);
        }

        private void ExecutePass(FXMeshData data, RasterGraphContext context)
        {
            // Draw outlines first (behind)
            context.cmd.EnableKeyword(_OUTLINES_PASS);
            context.cmd.DrawRendererList(data.m_outlineRendererList);
            
            // Draw main VFX (on top)
            context.cmd.DisableKeyword(_OUTLINES_PASS);
            context.cmd.DrawRendererList(data.m_rendererList);
        }
    }
}