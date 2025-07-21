using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FS.Rendering
{
    public class FXData : ContextItem
    {
        public TextureHandle m_bloomTexture;
        public TextureHandle m_bloomResult;

        public TextureHandle m_grabDistortionTexture;
        
        public override void Reset()
        {
            m_bloomTexture = TextureHandle.nullHandle;
            m_bloomResult = TextureHandle.nullHandle;
            m_grabDistortionTexture = TextureHandle.nullHandle;
        }
    }
    
    public class FXPipelineInit : ScriptableRenderPass
    {
        public FXPipelineInit()
        {
            renderPassEvent = RenderPassEvent.BeforeRendering;
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!frameData.Contains<FXData>())
            {
                var fxData = frameData.Create<FXData>();
                var camData = frameData.Get<UniversalResourceData>();
                var desc = camData.activeColorTexture.GetDescriptor(renderGraph);
                desc.width = (desc.width >> 0);
                desc.height = (desc.height >> 0);
                desc.enableRandomWrite = true;
                desc.useMipMap = false;
                desc.autoGenerateMips = false;
                desc.name = "FX_BloomFilter";
                desc.filterMode = FilterMode.Bilinear;
                desc.depthBufferBits = 0;
                desc.clearColor = Color.black;
                    
                fxData.m_bloomTexture = renderGraph.CreateTexture(desc);
                
                desc.name = "FX_BloomResult";
                fxData.m_bloomResult = renderGraph.CreateTexture(desc);

                desc.name = "FX_GrabDistortion";
                desc.format = GraphicsFormat.R16G16B16A16_SNorm ; // Allow negative values as we're encoding vectors
                fxData.m_grabDistortionTexture = renderGraph.CreateTexture(desc);
            }
        }
    }
}