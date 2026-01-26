using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine;

namespace FS.Rendering
{
    // Technically meant to be 'abstract', don't inherit from this directly its useless
    public interface ICommandBufferInjector
    {
        public string Name => "Unnamed Command Buffer Injector";

        public bool ShouldRender => true;

        public ScriptableRenderPassInput PassInput => ScriptableRenderPassInput.None;
        
        public void DeclareResources(IUnsafeRenderGraphBuilder builder, RenderGraph renderGraph, ContextContainer frameData) {}
    }
    
    
    /// <example>
    /// Interface to execute a custom command buffer during camera rendering:
    /// <code>
    /// public class MyEffect : MonoBehaviour, ICommandBufferPass
    /// {
    ///     public string Name => "My Custom Effect";
    ///     public ScriptableRenderPassInput PassInput => ScriptableRenderPassInput.Depth;
    /// 
    ///     void OnEnable() => GetComponent&lt;Camera&gt;().AddCameraCommandBuffer(RenderPassEvent.BeforeRenderingPostProcessing, this);
    ///     void OnDisable() => GetComponent&lt;Camera&gt;().RemoveCameraCommandBuffer(this);
    ///     
    ///     public void OnCameraRender(CommandBuffer cmd)
    ///     {
    ///         cmd.DrawMesh(myMesh, myMatrix, myMaterial);
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface ICommandBufferPass : ICommandBufferInjector
    {
        public void OnCameraRender(CommandBuffer cmd);
    }
    
    
    /// <example>
    /// Interface for full screen effects:
    /// <code>
    /// public class MyFullScreenEffect : MonoBehaviour, ICameraRenderPass
    /// {
    ///     public string Name => "My Custom Effect";
    ///     public ScriptableRenderPassInput PassInput => ScriptableRenderPassInput.Depth;
    ///     
    ///     void OnEnable() => GetComponent&lt;Camera&gt;().AddCameraCommandBuffer(RenderPassEvent.BeforeRenderingPostProcessing, this);
    ///     void OnDisable() => GetComponent&lt;Camera&gt;().RemoveCameraCommandBuffer(this);
    ///     
    ///     public void OnRenderImage(CommandBuffer cmd, TextureHandle source, TextureHandle dest)
    ///     {
    ///         Blitter.BlitCameraTexture(cmd, source, dest, m_fxMat, 0);
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface ICameraRenderPass : ICommandBufferInjector
    {
        public void OnCameraRender(CommandBuffer cmd, TextureHandle source, TextureHandle dest);
    }
    
    public class CommandBufferPass : ScriptableRenderPass
    {
        public CommandBufferPass(RenderPassEvent passEvt, Camera camera)
        {
            renderPassEvent = passEvt;
            m_camera = camera;
        }

        private Camera m_camera;
        public int InjectorCount => m_genericPasses.Count + m_blitPasses.Count;
        
        private List<ICommandBufferPass> m_genericPasses = new();
        private List<ICameraRenderPass> m_blitPasses = new();
        
        public bool ContainsInjector(ICommandBufferInjector injector) 
            => injector is ICommandBufferPass cbPass ? m_genericPasses.Contains(cbPass) : injector is ICameraRenderPass renderPass && m_blitPasses.Contains(renderPass);
        
        public void RegisterInjector(ICommandBufferInjector injector)
        {
            if (injector is ICommandBufferPass cbPass)
                m_genericPasses.Add(cbPass);
            else if (injector is ICameraRenderPass renderPass)
                m_blitPasses.Add(renderPass);
            else
                Debug.LogError($"[CommandBufferInjector] Trying to register an injector that does not implement ICommandBufferPass or ICameraRenderPass: {injector.Name}");
        }
        public void UnregisterInjector(ICommandBufferInjector injector)
        {
            if (injector is ICommandBufferPass cbPass)
                m_genericPasses.Remove(cbPass);
            else if (injector is ICameraRenderPass renderPass)
                m_blitPasses.Remove(renderPass);
        }

        private class PassData
        {
            public TextureHandle m_source;
            public TextureHandle m_destination;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using var builder = renderGraph.AddUnsafePass<PassData>($"[{m_camera?.name ?? "GLOBAL"}] Command Buffer Injector_{renderPassEvent}", out var passData);

            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);
            
            if (m_blitPasses.Count > 0)
            {
                passData.m_source = frameData.Get<UniversalResourceData>().cameraColor;
                passData.m_destination = renderGraph.CreateTexture(new TextureDesc(passData.m_source.GetDescriptor(renderGraph))
                {
                    name = $"Command Buffer Injector Intermediate_{m_camera?.name ?? "GLOBAL"}_{renderPassEvent}",
                    enableRandomWrite = false, // Simple intermediate
                    clearBuffer = false,
                });
                
                builder.UseTexture(passData.m_source, AccessFlags.ReadWrite);
                builder.UseTexture(passData.m_destination, AccessFlags.ReadWrite);
            }
            
            var requiredInputs = ScriptableRenderPassInput.None;
            foreach (var injector in m_genericPasses)
            {
                if (!injector.ShouldRender) continue;
                requiredInputs |= injector.PassInput;
                injector.DeclareResources(builder, renderGraph, frameData);
            }

            foreach (var injector in m_blitPasses)
            {
                if (!injector.ShouldRender) continue;
                requiredInputs |= injector.PassInput;
                injector.DeclareResources(builder, renderGraph, frameData);
            }

            ConfigureInput(requiredInputs);
            
            builder.SetRenderFunc<PassData>((data, context) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                
                using (new ProfilingScope(cmd, new ProfilingSampler($"[CommandBufferInjector] {m_camera?.name ?? "GLOBAL"} - {renderPassEvent}")))
                {
                    foreach (var injector in m_genericPasses)
                    {
                        if (!injector.ShouldRender) continue;

                        using var profile = new ProfilingScope(cmd, new ProfilingSampler(injector.Name));
                        injector.OnCameraRender(cmd);
                    }
                }

                if (m_blitPasses.Count > 0)
                {
                    var src = data.m_source;
                    var dest = data.m_destination;

                    int passesExecuted = 0;

                    using (new ProfilingScope(cmd, new ProfilingSampler($"[CommandBufferInjector] {m_camera?.name ?? "GLOBAL"} - {renderPassEvent} Image Passes")))
                    {
                        foreach (var blitPass in m_blitPasses)
                        {
                            if (!blitPass.ShouldRender) continue;

                            using var profile = new ProfilingScope(cmd, new ProfilingSampler(blitPass.Name));
                            blitPass.OnCameraRender(cmd, src, dest);
                            (src, dest) = (dest, src); // Swap
                            passesExecuted++;
                        }


                        // Final blit to destination if needed (even number of FX means src is dest)
                        if (passesExecuted % 2 != 0)
                            Blitter.BlitCameraTexture(cmd, src, dest);
                    }
                }
            });
        }
    }

}