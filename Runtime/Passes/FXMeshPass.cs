using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FS.Rendering
{
    public class FXMeshPass : ScriptableRenderPass
    {
        public FXMeshPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }
        
        public class FXMeshData
        {
            public RendererListHandle m_rendererList;
        }
        
        private readonly List<ShaderTagId> k_ShaderTagId = new (){ new ShaderTagId("VFX") }; 
        
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
                DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(k_ShaderTagId, renderData, camData,
                    lightData, SortingCriteria.CommonTransparent);//camData.defaultOpaqueSortFlags);
                FilteringSettings filterSettings = FilteringSettings.defaultValue;
                filterSettings.renderQueueRange = RenderQueueRange.transparent;
                RendererListParams renderingParams =
                    new RendererListParams(renderData.cullResults, drawSettings, filterSettings);
                passData.m_rendererList = renderGraph.CreateRendererList(renderingParams);
                builder.UseRendererList(passData.m_rendererList);
            }


            //builder.SetRenderAttachment(fxData.m_bloomTexture, 0);
            builder.SetRenderAttachment(camResources.activeColorTexture, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(camResources.activeDepthTexture, AccessFlags.Write); // NOTE: Not attaching a depth texture for write is what fucks up backface rendering
            builder.SetRenderAttachment(fxData.m_bloomTexture, 1);
            
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true); // still dk what this is
            
            
            builder.SetRenderFunc<FXMeshData>(ExecutePass);
        }

        private void ExecutePass(FXMeshData data, RasterGraphContext context)
        {
            context.cmd.DrawRendererList(data.m_rendererList);
        }
    }
}