using FS.Rendering.Utility;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FS.Rendering
{
    public class BloomPass : ScriptableRenderPass
    {
        [System.Serializable]
        public struct BloomSettings
        {
            public bool useCompute;
            [Range(0, 10)] public float bloomThreshold;
            [Range(0, 10)] public float bloomIntensity;
            [Range(0,10)] public float bloomSize;
        }
        
        private static readonly int k_bloomIntensityID = Shader.PropertyToID("_BloomIntensity");
        private static readonly int k_bloomThresholdID = Shader.PropertyToID("_BloomThreshold");
        private static readonly int k_bloomScatterID = Shader.PropertyToID("_BloomSpread");
        
        private static readonly int k_bloomSourceID = Shader.PropertyToID("_BloomSource");
        private static readonly int k_selectiveBloomSourceID = Shader.PropertyToID("_SelectiveBloomSource");
        private static readonly int k_bloomHigherMipSourceID = Shader.PropertyToID("_HigherMipSource");

        private BloomSettings m_bloomSettings;

        private Material m_bloomMaterial;

        private RenderTextureDescriptor[] m_mipDesc;

        private MipBuffer m_downSampleBuffer;
        private MipBuffer m_upSampleBuffer;
        
        public BloomPass(BloomPass.BloomSettings settings)
        {
            // Before AA
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

            m_bloomMaterial = new Material(Shader.Find("Hidden/PostProcess/Bloom"))
                { hideFlags = HideFlags.HideAndDontSave };
            
            m_mipDesc = new RenderTextureDescriptor[MipBuffer.k_maxMipLevels]; // max 15 mips supported
            m_downSampleBuffer = new MipBuffer("Bloom_MipDown_");
            m_upSampleBuffer = new MipBuffer("Bloom_MipUp_");
            
            m_bloomSettings = settings;
        }
        
        public void Dispose()
        {
            Object.DestroyImmediate(m_bloomMaterial);
        }

        private class BloomData
        {
            public Material bloomMaterial;
            public TextureHandle cameraSource;
            public TextureHandle bloomResult;
            public TextureHandle bloomFilter;
            public BloomPass.BloomSettings settings;

            public RenderTextureDescriptor cameraDesc;
            
            public float cameraWidth;
            public float cameraHeight;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var camRes = frameData.Get<UniversalResourceData>();
            var fxData = frameData.Get<FXData>();
            var camData = frameData.Get<UniversalCameraData>();
            
            using var builder = renderGraph.AddUnsafePass("Bloom Setup", out BloomData passData);

            var desc = camData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            // this also serves as the destination of the bloom pass
            //passData.dest = fxData.m_bloomTexture;//UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "Bloom Destination", false, FilterMode.Bilinear);
            // Create mip-chain
            passData.bloomFilter = fxData.m_bloomTexture;
            passData.bloomResult = fxData.m_bloomResult;
            passData.cameraSource = camRes.activeColorTexture;
            passData.cameraDesc = desc;
            passData.bloomMaterial = m_bloomMaterial;
            passData.settings = m_bloomSettings;

            if (camData.camera.allowDynamicResolution)
            {
                passData.cameraWidth = camData.cameraTargetDescriptor.width * ScalableBufferManager.widthScaleFactor;
                passData.cameraHeight = camData.cameraTargetDescriptor.height * ScalableBufferManager.heightScaleFactor;
            }
            else
            {
                passData.cameraWidth = camData.cameraTargetDescriptor.width;
                passData.cameraHeight = camData.cameraTargetDescriptor.height;
            }

            builder.UseTexture(passData.bloomFilter, AccessFlags.ReadWrite);
            builder.UseTexture(passData.bloomResult, AccessFlags.Write);
            builder.UseTexture(passData.cameraSource, AccessFlags.Read);
            
            builder.AllowPassCulling(false);
            
            builder.SetRenderFunc<BloomData>(ExecutePass);
        }

        private int m_mipIterations;
        
        private void ExecutePass(BloomData data, UnsafeGraphContext context)
        {
            var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
            
            cmd.BeginSample("Bloom Prefilter Setup");
            {
                UpdateMipBuffers(data);
                
                cmd.SetGlobalFloat(k_bloomIntensityID, Mathf.GammaToLinearSpace(data.settings.bloomIntensity));
                cmd.SetGlobalFloat(k_bloomThresholdID, Mathf.GammaToLinearSpace(data.settings.bloomThreshold));
                cmd.SetGlobalFloat(k_bloomScatterID, data.settings.bloomSize);
                //data.bloomMaterial.SetFloat(k_bloomIntensityID, data.settings.bloomIntensity);
                //data.bloomMaterial.SetFloat(k_bloomThresholdID, data.settings.bloomThreshold);
                //data.bloomMaterial.SetFloat(k_bloomScatterID, data.settings.bloomSize);
            }
            cmd.EndSample("Bloom Prefilter Setup");

            PreFilter(cmd, data);
            DownSample(cmd, data);
            UpSample(cmd, data);
        }

        private void UpdateMipBuffers(BloomData data)
        {
            PrepareIterations(data.settings.bloomSize, new Vector2(data.cameraWidth, data.cameraHeight), ref m_mipIterations, ref data.settings.bloomSize);

            // start at half res & go down
            var renderWidth = Mathf.CeilToInt(data.cameraWidth / 2);
            var renderHeight = Mathf.CeilToInt(data.cameraHeight / 2);
            for (int l = 0; l <= m_mipIterations; l++) // <= for 1 additional iter
            {
                m_mipDesc[l] = data.cameraDesc;
                ref var rtDesc = ref m_mipDesc[l];
                rtDesc.memoryless = RenderTextureMemoryless.None;
                rtDesc.useDynamicScale = false;
                rtDesc.depthBufferBits = 0;
                rtDesc.width = renderWidth;
                rtDesc.height = renderHeight;

                renderWidth = Mathf.Max(1, renderWidth / 2);
                renderHeight = Mathf.Max(1, renderHeight / 2);
            }
        }

        private void PrepareIterations(float scattering, Vector2 camSize, ref int iterations, ref float spread)
        {
            float mipSize = Mathf.Log(Mathf.FloorToInt(Mathf.Max(camSize.x, camSize.y)), 2.0f) - 1; // num mips
            float scaledIterations = mipSize + Mathf.Clamp(scattering, 1f, 10f) - 10f;
            iterations = Mathf.Max(Mathf.FloorToInt(scaledIterations), 1);
            spread = scaledIterations > 1 ? 0.5f + scaledIterations - iterations : 0.5f;
        }

        private void PreFilter(CommandBuffer cmd, BloomData data)
        {
            cmd.BeginSample("Bloom Prefilter");

            //cmd.SetRenderTarget(data.bloomFilter);
            cmd.SetGlobalTexture(k_bloomSourceID, data.cameraSource);
            cmd.SetGlobalTexture(k_selectiveBloomSourceID, data.bloomFilter);

            m_downSampleBuffer.CreateTemporary(m_mipDesc, 0, cmd);
            cmd.Draw(m_downSampleBuffer.RenderTargets[0].m_RTIdentifier, m_bloomMaterial, 0);
            //Blitter.BlitCameraTexture(cmd, data.cameraSource, data.bloomFilter, m_bloomMaterial, 0);
            
            cmd.EndSample("Bloom Prefilter");
        }

        private void DownSample(CommandBuffer cmd, BloomData data)
        {
            cmd.BeginSample("Bloom Downsample");

            
            for (int l = 0; l < m_mipIterations; l++)
            {
                m_downSampleBuffer.CreateTemporary(m_mipDesc, l+1, cmd);

                cmd.SetGlobalTexture(k_bloomSourceID, m_downSampleBuffer.RenderTargets[l].m_RTIdentifier);
                
                cmd.Draw(m_downSampleBuffer.RenderTargets[l + 1].m_RTIdentifier, m_bloomMaterial, 1);
            }
            
            cmd.EndSample("Bloom Downsample");
        }

        private void UpSample(CommandBuffer cmd, BloomData data)
        {
            cmd.BeginSample("Bloom UpSample");

            for (int l = m_mipIterations; l > 0; l--)
            {
                // write into the next mips upSample buffer
                m_upSampleBuffer.CreateTemporary(m_mipDesc, l - 1, cmd);
                
                // Set the higher mip source texture as the downSample buffer version of the mip we're writing to
                cmd.SetGlobalTexture(k_bloomHigherMipSourceID, m_downSampleBuffer.RenderTargets[l - 1].m_RTIdentifier);
                
                cmd.SetGlobalTexture(k_bloomSourceID, 
                        l >= m_mipIterations 
                        ? m_downSampleBuffer.RenderTargets[l].m_RTIdentifier 
                        : m_upSampleBuffer.RenderTargets[l].m_RTIdentifier);
                
                
                cmd.Draw(m_upSampleBuffer.RenderTargets[l - 1].m_RTIdentifier, m_bloomMaterial, 2);

                if (l >= m_mipIterations)
                    m_downSampleBuffer.ClearTemporary(cmd, l);
                else
                {
                    m_downSampleBuffer.ClearTemporary(cmd, l);
                    m_upSampleBuffer.ClearTemporary(cmd, l);
                }
            }
            
            cmd.EndSample("Bloom UpSample");
            
            cmd.BeginSample("Composite Bloom");

            // Write back into the bloom filter texture
            cmd.SetGlobalTexture(k_bloomSourceID, m_upSampleBuffer.RenderTargets[0].m_RTIdentifier);
            cmd.SetGlobalTexture(k_selectiveBloomSourceID, data.bloomFilter);
            cmd.Draw(data.bloomResult, m_bloomMaterial, 3);
            
            m_downSampleBuffer.ClearTemporary(cmd, 0);
            m_upSampleBuffer.ClearTemporary(cmd, 0);

            cmd.EndSample("Composite Bloom");
        }
    }
}