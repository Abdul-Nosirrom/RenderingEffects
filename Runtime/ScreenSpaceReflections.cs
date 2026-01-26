using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FS.Rendering
{
    public class ScreenSpaceReflections : ScriptableRendererFeature
    {
        [Header("SSR")]
        public SSRPass.Quality m_ssrQuality;

        private SSRPass m_ssrPass;
        
        public override void Create()
        {
            m_ssrPass = new SSRPass();
        }

        private void OnDestroy()
        {
            m_ssrPass?.Dispose();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            m_ssrPass.m_SSRQuality = m_ssrQuality;
            renderer.EnqueuePass(m_ssrPass);
        }
    }
}