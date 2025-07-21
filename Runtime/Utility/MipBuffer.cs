using UnityEngine;
using UnityEngine.Rendering;

namespace FS.Rendering.Utility
{
    public struct RenderTarget
    {
        public RenderTargetIdentifier m_RTIdentifier;
        public int m_identifier;
    }
    
    public sealed class MipBuffer
    {
        public static readonly int k_maxMipLevels = 15; // Prealloc to this
        
        private RenderTarget[] m_renderTargets = new RenderTarget[k_maxMipLevels];
        public RenderTarget[] RenderTargets => m_renderTargets;

        public MipBuffer(string name)
        {
            for (int i = 0; i < k_maxMipLevels; i++)
            {
                m_renderTargets[i].m_identifier = Shader.PropertyToID(name + i);
                m_renderTargets[i].m_RTIdentifier = new RenderTargetIdentifier(m_renderTargets[i].m_identifier, 0, CubemapFace.Unknown, -1);
            }
        }

        public void CreateTemporary(RenderTextureDescriptor[] desc, int mipLevel, CommandBuffer cmd)
        {
            cmd.GetTemporaryRT(RenderTargets[mipLevel].m_identifier, desc[mipLevel], FilterMode.Bilinear);
        }

        public void ClearTemporary(CommandBuffer cmd, int mipLevel)
        {
            cmd.ReleaseTemporaryRT(RenderTargets[mipLevel].m_identifier);
        }
    }
}