using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace FS.Rendering
{
    public class PostFXCompositePass : ScriptableRenderPass, IDisposable
    {
        private readonly Material m_postFXMaterial;
        
        public PostFXCompositePass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            m_postFXMaterial = new Material(Shader.Find("Hidden/PostProcessing/FinalComposite"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        public void Dispose()
        {
            Object.DestroyImmediate(m_postFXMaterial);
        }

        private class PostFXData
        {
            public Material m_postFXMaterial;
            
            public TextureHandle m_cameraTexture;
            public TextureHandle m_bloomTexture;
            public TextureHandle m_distortionTexture;
            public TextureHandle m_intermediate;

            public TextureHandle m_target;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var camInfo = frameData.Get<UniversalCameraData>();
            var camData = frameData.Get<UniversalResourceData>();
            var fxData = frameData.Get<FXData>();
            
            
            using var builder = renderGraph.AddUnsafePass<PostFXData>("PostFX", out var passData);
            
            var desc = camData.activeColorTexture.GetDescriptor(renderGraph);

            desc.name = $"_CameraTarget_PostFX_Intermediate";
            passData.m_intermediate = renderGraph.CreateTexture(desc);
            builder.UseTexture(passData.m_intermediate, AccessFlags.ReadWrite);
            
            passData.m_postFXMaterial = m_postFXMaterial;
            passData.m_cameraTexture = camData.activeColorTexture;
            passData.m_bloomTexture = fxData.m_bloomResult;
            passData.m_distortionTexture = fxData.m_grabDistortionTexture;
            
            //builder.UseTexture(passData.m_target, AccessFlags.Write);
            //builder.SetRenderAttachment(passData.m_target, 0);
            //builder.UseTexture(passData.m_target, AccessFlags.Write);
            builder.UseTexture(passData.m_cameraTexture, AccessFlags.ReadWrite);
            builder.UseTexture(passData.m_bloomTexture, AccessFlags.Read);
            builder.UseTexture(passData.m_distortionTexture, AccessFlags.Read);
            
            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc<PostFXData>(ExecutePass);
        }

        private static void ExecutePass(PostFXData data, UnsafeGraphContext context)
        {
            var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
            
            data.m_postFXMaterial.SetTexture("_BloomTexture", data.m_bloomTexture);
            data.m_postFXMaterial.SetTexture("_DistortionTexture", data.m_distortionTexture);
            
            Blitter.BlitCameraTexture(cmd, data.m_cameraTexture, data.m_intermediate, data.m_postFXMaterial, 0);
            Blitter.BlitCameraTexture(cmd, data.m_intermediate, data.m_cameraTexture, data.m_postFXMaterial, 1);
        }
    }
}