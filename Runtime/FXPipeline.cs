using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FS.Rendering
{
    public class FXPipeline : ScriptableRendererFeature
    {
        [Header("Bloom Settings")]
        public BloomPass.BloomSettings m_bloomSettings; // Todo: Move these to FXData so everything can access it
        
        private FXPipelineInit m_fxInit;

        private HiZGenerationPass m_HiZGenPass;
        
        private FXMeshPass m_fxMeshPass;
        
        private GrabDistortionPass m_grabDistortionPass;
        
        private BloomPass m_bloomPassEx;
        
        private PostFXCompositePass m_postFX;
        
        public override void Create()
        {
            m_fxInit = new FXPipelineInit();
            
            m_HiZGenPass = new HiZGenerationPass();

            m_fxMeshPass = new FXMeshPass();
            
            m_grabDistortionPass = new GrabDistortionPass();
            
            m_postFX = new PostFXCompositePass();
            
            m_bloomPassEx = new BloomPass(m_bloomSettings);
        }

        private void OnDestroy()
        {
            m_bloomPassEx.Dispose();
            m_postFX.Dispose();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_fxInit);
            renderer.EnqueuePass(m_HiZGenPass);
            renderer.EnqueuePass(m_fxMeshPass);
            renderer.EnqueuePass(m_grabDistortionPass);
            renderer.EnqueuePass(m_bloomPassEx);
            renderer.EnqueuePass(m_postFX);
        }
    }
}