using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace FS.Rendering
{
    public class SSRPass : ScriptableRenderPass, IDisposable
    {
        public SSRPass()
        {
            profilingSampler = new ProfilingSampler(nameof(SSRPass));
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents; // After skybox

            m_ssrMaterial = new Material(Shader.Find("Hidden/PostProcess/SSR"))
                { hideFlags = HideFlags.HideAndDontSave };
        }

        public void Dispose()
        {
            m_ssrHistoryTexture?.Release();
            Object.DestroyImmediate(m_ssrMaterial);
        }

        public enum Quality { None, Low, Medium, High }
        public Quality m_SSRQuality = Quality.High;
        
        private static readonly List<ShaderTagId> s_ssrMaskPass = new () { new ShaderTagId("SSRMask") };
        
        public const string k_SSRMaskPassName = "SSRMask";

        private Material m_ssrMaterial;
        private RTHandle m_ssrHistoryTexture;
        public RTHandle SSRTexture => m_ssrHistoryTexture;
        
        private class SSRMaskData
        {
            public RendererListHandle m_rendererList;
            public TextureHandle m_ssrMask;
        }

        private class SSRTextureData
        {
            public TextureHandle m_ssrMask;
            public TextureHandle m_ssrTexture;
            public TextureHandle m_depth;
            public TextureHandle m_normals;
            public TextureHandle m_color;
        }

        void EnsureHistoryTexture(TextureDesc desc)
        {
            if (m_ssrHistoryTexture == null || 
                m_ssrHistoryTexture.rt.width != desc.width || 
                m_ssrHistoryTexture.rt.height != desc.height)
            {
                m_ssrHistoryTexture?.Release();
                m_ssrHistoryTexture = RTHandles.Alloc(
                    desc.width, desc.height,
                    colorFormat: desc.colorFormat,
                    depthBufferBits: DepthBits.None,
                    filterMode: FilterMode.Bilinear,
                    name: "SSR History"
                );
            }
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            //if (m_SSRQuality == Quality.None) return;
            
            var camData = frameData.Get<UniversalCameraData>();
            var camResources = frameData.Get<UniversalResourceData>();
            var renderData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();
            var camTexDesc = camResources.activeColorTexture.GetDescriptor(renderGraph);
            
            var ssrMaskDesc = GetSSRTextureDescriptor("SSR Mask", camTexDesc);
            ssrMaskDesc.width = camTexDesc.width;
            ssrMaskDesc.height = camTexDesc.height; // Can keep mask same res i think... so we attach depth
            ssrMaskDesc.clearColor = Color.black;
            ssrMaskDesc.colorFormat = GraphicsFormat.R8_UNorm; // Single channel mask
            var ssrTexturesDesc = GetSSRTextureDescriptor("SSR Texture", camTexDesc);
            ssrTexturesDesc.autoGenerateMips = true;
            ssrTexturesDesc.useMipMap = true; // Roughness to mips
            
            var ssrMask = renderGraph.CreateTexture(ssrMaskDesc);
            EnsureHistoryTexture(ssrTexturesDesc);
            //var ssrTexture = renderGraph.CreateTexture(ssrTexturesDesc);
            var ssrTexture = renderGraph.ImportTexture(m_ssrHistoryTexture);
            
            var colorTexture = camResources.activeColorTexture;
            var depthTexture = camResources.activeDepthTexture;
            var normalsTexture = camResources.cameraNormalsTexture;
            
            using (var ssrMaskBuilder = renderGraph.AddRasterRenderPass<SSRMaskData>("SSR Mask Pass", out var ssrMaskPassData, profilingSampler))
            {
                ssrMaskPassData.m_ssrMask = ssrMask;
                ssrMaskPassData.m_rendererList = FSRenderUtils.CreateRendererListHandle(renderGraph, camData, renderData, lightData, s_ssrMaskPass, SortingCriteria.CommonOpaque, RenderQueueRange.all);
                ssrMaskBuilder.UseRendererList(ssrMaskPassData.m_rendererList);
                
                ssrMaskBuilder.SetRenderAttachment(ssrMaskPassData.m_ssrMask, 0);
                ssrMaskBuilder.SetRenderAttachmentDepth(depthTexture, AccessFlags.Read); // Res diff so issue binding it 
                
                ssrMaskBuilder.AllowPassCulling(false);
                ssrMaskBuilder.AllowGlobalStateModification(true);
                
                ssrMaskBuilder.SetRenderFunc<SSRMaskData>(((data, context) => context.cmd.DrawRendererList(data.m_rendererList)));
            }
            
            using (var ssrTextureBuilder =
                   renderGraph.AddUnsafePass<SSRTextureData>("SSR Texture Pass", out var ssrTextureData))
            {
                ssrTextureData.m_ssrMask = ssrMask;
                ssrTextureData.m_ssrTexture = ssrTexture;
                ssrTextureData.m_depth = depthTexture;
                ssrTextureData.m_normals = normalsTexture;
                ssrTextureData.m_color = colorTexture;
                
                //ssrTextureBuilder.SetRenderAttachment(ssrTextureData.m_ssrTexture, 0);
                //ssrTextureBuilder.SetRenderAttachment(ssrTextureData.m_color, 0);
                ssrTextureBuilder.UseTexture(ssrTextureData.m_ssrTexture, AccessFlags.ReadWrite);
                ssrTextureBuilder.UseTexture(ssrTextureData.m_color, AccessFlags.ReadWrite);
                ssrTextureBuilder.UseTexture(ssrTextureData.m_ssrMask);
                //ssrTextureBuilder.UseTexture(ssrTextureData.m_color);
                ssrTextureBuilder.UseTexture(ssrTextureData.m_depth);
                ssrTextureBuilder.UseTexture(ssrTextureData.m_normals);
                ssrTextureBuilder.AllowPassCulling(false);
                ssrTextureBuilder.AllowGlobalStateModification(true);
                
                ssrTextureBuilder.SetRenderFunc(((SSRTextureData data, UnsafeGraphContext context) =>
                {
                    m_ssrMaterial.SetFloat("_FrameCount", Time.frameCount % 6);
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    //cmd.SetRenderTarget(data.m_ssrTexture);
                    //cmd.ClearRenderTarget(true, true, Color.clear);
                    //Blitter.BlitCameraTexture(cmd, data.m_ssrMask, data.m_ssrTexture, m_ssrMaterial, 0);
                    Blitter.BlitCameraTexture(cmd, data.m_ssrMask, data.m_ssrTexture, m_ssrMaterial, 0);
                    cmd.SetGlobalTexture("_SSR", data.m_ssrTexture);
                    Blitter.BlitCameraTexture(cmd, data.m_ssrMask, data.m_color, m_ssrMaterial, 1);;
                    //cmd.SetGlobalTexture("_SSRTex", data.m_ssrTexture);
                    //Blitter.BlitCameraTexture(cmd, data.m_ssrTexture, data.m_color, bilinear: true); // uncomment to debug
                }));
            }
        }

        private TextureDesc GetSSRTextureDescriptor(string name, TextureDesc texDesc)
        {
            // Full res, half res, quarter res
            int resShift = m_SSRQuality switch
            {
                Quality.Low => 2,
                Quality.Medium => 1,
                Quality.High => 0,
                Quality.None => 0,
                _ => throw new ArgumentOutOfRangeException()
            };
            
            texDesc.width >>= resShift;
            texDesc.height >>= resShift;

            texDesc.autoGenerateMips = false;
            texDesc.name = name;
            
            return texDesc;
        }
    }
}