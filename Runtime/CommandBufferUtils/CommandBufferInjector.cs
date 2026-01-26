using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FS.Rendering
{
    public static class CommandBufferUtils
    {
        /// <summary>
        /// Add a command buffer writer to a specific camera to be executed at the given render pass event.
        /// </summary>
        public static void AddCameraCommandBuffer(this Camera camera, RenderPassEvent renderPassEvent, ICommandBufferInjector injector)
        {
            CommandBufferInjector.RegisterInjector(camera, injector, renderPassEvent);
        }
        
        /// <summary>
        /// Remove a command buffer writer from a specific camera.
        /// </summary>
        public static void RemoveCameraCommandBuffer(this Camera camera, ICommandBufferInjector injector)
        {
            // TODO: This should prolly still take a camera param given that we could stop an effect on one camera but keep it running on another
            CommandBufferInjector.UnregisterInjector(camera, injector);
        }

        /// <summary>
        /// Adds a command buffer writer to be executed for all cameras at the given render pass event.
        /// </summary>
        public static void AddGlobalCommandBuffer(this ICommandBufferInjector injector,
            RenderPassEvent renderPassEvent)
        {
            CommandBufferInjector.RegisterInjector(null, injector, renderPassEvent);
        }

        /// <summary>
        /// Removes a command buffer writer from all cameras.
        /// </summary>
        public static void RemoveGlobalCommandBuffer(this ICommandBufferInjector injector)
        {
            CommandBufferInjector.UnregisterInjector(null, injector);
        }
    }
    
    public class CommandBufferInjector : ScriptableRendererFeature
    {
        public override void Create()
        {}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Cleanup()
        {
            m_passes.Clear();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cam = renderingData.cameraData.camera;
            
            foreach (var kvp in m_passes)
            {
                // Match camera (null = universal for all cameras)
                if ((kvp.Key.camera == null || kvp.Key.camera == cam) && kvp.Value.InjectorCount > 0)
                {
                    renderer.EnqueuePass(kvp.Value);
                }
            }
        }
        
        #region Command Buffer Registration
        
        private static Dictionary<Key, CommandBufferPass> m_passes = new();
        
        /// <summary>
        /// Register a command buffer writer to a given camera to be executed at the given render pass event. If the camera is null,
        /// the command buffer writer will be executed for all cameras.
        /// </summary>
        public static void RegisterInjector(Camera cam, ICommandBufferInjector injector, RenderPassEvent passEvent)
        {
            var key = new Key(cam, passEvent);
            
            if (!m_passes.TryGetValue(key, out var pass))
            {
                pass = new CommandBufferPass(passEvent, cam);
                m_passes[key] = pass;
            }

            if (pass.ContainsInjector(injector))
            {
                Debug.LogWarning($"[CommandBufferInjector] Trying to register an injector that is already registered: {injector.Name}");
                return;
            }
            
            pass.RegisterInjector(injector);
        }
        
        /// <summary>
        /// Remove a command buffer writer from a given camera. If the camera is null, the command buffer writer will be removed from all cameras.
        /// We assume an CommandBufferWriter is intended for only 1 renderPassEvent hence why we don't take that as a param.
        /// </summary>
        public static void UnregisterInjector(Camera cam, ICommandBufferInjector injector)
        {
            List<Key> keysToRemove = null;
            
            foreach (var kvp in m_passes)
            {
                // Match camera (null = remove all instances of this injector)
                if (cam != null && kvp.Key.camera != cam) 
                    continue;
                
                kvp.Value.UnregisterInjector(injector);
                
                if (kvp.Value.InjectorCount == 0)
                {
                    keysToRemove ??= new List<Key>();
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            if (keysToRemove != null)
            {
                foreach (var key in keysToRemove)
                {
                    m_passes.Remove(key);
                }
            }
        }

        /// <summary>
        /// Clears all CommandBufferWriters from this specific camera4
        /// </summary>
        public static void ClearCameraRenderers(Camera camera)
        {
            List<Key> keysToRemove = null;
            
            foreach (var kvp in m_passes)
            {
                if (kvp.Key.camera == camera)
                {
                    keysToRemove ??= new List<Key>();
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            if (keysToRemove != null)
            {
                foreach (var key in keysToRemove)
                {
                    m_passes.Remove(key);
                }
            }
        }

        private struct Key : IEquatable<Key>
        {
            public Camera camera;
            public RenderPassEvent passEvent;
            
            public Key(Camera cam, RenderPassEvent evt)
            {
                camera = cam;
                passEvent = evt;
            }
            
            public override int GetHashCode()
            {
                unchecked
                {
                    int cameraHash = camera != null ? camera.GetHashCode() : 0;
                    return (cameraHash * 397) ^ (int)passEvent;
                }
            }
            
            public bool Equals(Key other) => ReferenceEquals(camera, other.camera) && passEvent == other.passEvent;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
        }
        
        #endregion
    }
}