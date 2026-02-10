using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FS.Rendering
{
    public class HiZGenerationPass : ScriptableRenderPass
    {
        private class HiZPassData
        {
            public TextureHandle m_rawDepthTexture;
            public TextureDesc m_depthDesc;
            public TextureHandle m_zMipChain;
        }
        
        private ComputeShader m_HiZShader;

        public HiZGenerationPass()
        {
            profilingSampler = new ProfilingSampler(nameof(HiZGenerationPass));
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses; // If no prepasses, then after rendering opaques. Dynamic switch

            m_HiZShader = Resources.Load<ComputeShader>("Compute/HiZMipChain");
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_HiZShader == null) return;
            
            using var builder = renderGraph.AddComputePass<HiZPassData>("HiZ Generation", out var passData, profilingSampler);

            var camData = frameData.Get<UniversalResourceData>();
            passData.m_rawDepthTexture = camData.activeDepthTexture;
            builder.UseTexture(passData.m_rawDepthTexture, AccessFlags.Read);
            passData.m_depthDesc = passData.m_rawDepthTexture.GetDescriptor(renderGraph);
            
            var fxData = frameData.Get<FXData>();
            passData.m_zMipChain = fxData.m_HiZMipChain;

            builder.UseTexture(passData.m_zMipChain, AccessFlags.ReadWrite);

            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true); // still dunno what this is but i keep setting it everywhere
            
            builder.SetRenderFunc<HiZPassData>(DoZMipChainCompute);
        }

        private void DoZMipChainCompute(HiZPassData data, ComputeGraphContext context)
        {
            m_HiZShader.GetKernelThreadGroupSizes(0, out var threadGroupSize, out var _, out var _);
            int width = data.m_depthDesc.width;   // mip 0 dimensions
            int height = data.m_depthDesc.height;
            
            int numMips = MipCount(width, height);
            
            // First pass is just a copy from raw depth to 0th mip
            if (false) // 1ms, lets just use the og depth directly
            {
                
                context.cmd.SetComputeTextureParam(m_HiZShader, 0, "_OriginalDepth", data.m_rawDepthTexture);
                context.cmd.SetComputeTextureParam(m_HiZShader, 0, "_ZMip_0", data.m_zMipChain,0);
                
                context.cmd.SetComputeIntParams(m_HiZShader, "_MipDimensions", width, height);

                context.cmd.DispatchCompute(m_HiZShader, 0,
                    Mathf.CeilToInt(width / (float)threadGroupSize),
                    Mathf.CeilToInt(height / (float)threadGroupSize), 1);
            }
            
            // Next iterative downsample from d -> d + 1 on the chain
            for (int d = 0; d < numMips - 1; d++)
            {
                // First iteration reads from actual depth buffer, rest from HiZ chain
                if (d == 0) context.cmd.SetComputeTextureParam(m_HiZShader, 1, "_ZMip_From", data.m_rawDepthTexture);
                else        context.cmd.SetComputeTextureParam(m_HiZShader, 1, "_ZMip_From", data.m_zMipChain, d);
                
                context.cmd.SetComputeTextureParam(m_HiZShader, 1, "_ZMip_To", data.m_zMipChain, d + 1);
                
                int mipWidth = Mathf.Max(1, width >> (d + 1));
                int mipHeight = Mathf.Max(1, height >> (d + 1));
                context.cmd.SetComputeIntParams(m_HiZShader, "_MipDimensions", mipWidth, mipHeight);
                context.cmd.SetComputeIntParam(m_HiZShader, "_SrcMipLevel", d);
                
                context.cmd.DispatchCompute(m_HiZShader, 1,
                    Mathf.CeilToInt(mipWidth / (float)threadGroupSize),
                    Mathf.CeilToInt(mipHeight / (float)threadGroupSize), 1);
            }
            
            // Set global shader data for easy access in DeclareHiZChain.hlsl
            context.cmd.SetGlobalVector("_HiZResolution", new Vector4(width, height));
            context.cmd.SetGlobalInt("_HiZMipCount", numMips);
            context.cmd.SetGlobalTexture("_HiZTexture", data.m_zMipChain);
        }

        // Stop at 32x132, more than course enough and last few mips are useless and just add to dispatch overhead (saving ~0.15ms)
        const int k_minMipSize = 16;
        public static int MipCount(int width, int height) => Mathf.Max(1, Mathf.FloorToInt(Mathf.Log(Mathf.Max(width, height) / (float)k_minMipSize, 2)) + 1);
        
        public static TextureHandle InitMipChain(RenderGraph renderGraph, ContextContainer frameData)
        {
            var camData = frameData.Get<UniversalResourceData>();
            var depthDesc = camData.activeDepthTexture.GetDescriptor(renderGraph);
            var ogDepthDesc = depthDesc;
            
            depthDesc.depthBufferBits = 0;
            depthDesc.colorFormat = GraphicsFormat.R32_SFloat; // or R16_SFloat if precision is fine
            depthDesc.enableRandomWrite = true; // compute route UAV
            depthDesc.filterMode = FilterMode.Point; // you don't want filtering between depth values
            depthDesc.useMipMap = true;
            depthDesc.autoGenerateMips = false;
            
            int numMips = MipCount(depthDesc.width, depthDesc.height);

            depthDesc.name = $"HiZ Depth 1->{numMips}";
            return renderGraph.CreateTexture(depthDesc);

            // TextureHandle[] zMipChain = new TextureHandle[numMips];
            //
            // // Generate desc
            // for (int d = 0; d < numMips; d++)
            // {
            //     // Use og depth directly for mip 0
            //     if (d == 0)
            //     {
            //         zMipChain[d] = camData.activeDepthTexture;
            //         continue;
            //     }
            //     
            //     // Iterative so compounds each iteration by >> 1
            //     depthDesc.width = Mathf.Max(1, ogDepthDesc.width >> d);
            //     depthDesc.height = Mathf.Max(1, ogDepthDesc.height >> d);
            //     
            //     depthDesc.name = $"HiZ_DepthMip_{d}";
            //     
            //     zMipChain[d] = renderGraph.CreateTexture(depthDesc);
            // }
            //
            // return zMipChain;
        }
    }
}