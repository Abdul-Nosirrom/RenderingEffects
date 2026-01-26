using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Animancer;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

// TODO: To get the sub-controllers and all that working, last way you tried it was kind of messy. Instead make another component that inherits from VFXController called 'VFX Director'
// and what that does is allow you to setup multiple VFX in like a timeline
// To do that, we must first figure out how to handle them in looping effects
// Framework for setting that up is making a shared base class perhaps called VFXBase thats an abstract monobehavior and holds the necessary functions for pooling and all that,
// that way we can convert our pool to work with VFXBase instead of VFXController. We then think of VFXController as controlling a 'single vfx prefab', and VFX director of 'directing'
// multiple VFXControllers.
// This'll also allow us to easily extend support for different methods of VFX playback with just 1 VFX.
namespace FS.Rendering
{
    // Controls playing back the VFX & all that shit dw about this part for now
    [ExecuteInEditMode]
    public class VFXController : VFXBase
    {
        [SerializeField, HideInInspector] private List<VFXAnimationPlayer> m_animators = new();
        [SerializeField, HideInInspector] private List<ParticleSystem> m_particleSystems = new();

        protected override bool IsInitialized =>
            !((m_animators.Count == 0 && m_particleSystems.Count == 0) || m_playbackDuration < 0);

        protected override void Start()
        {
            // Disable any VFX Controllers that are children because we'll end up manually playing them via the parent controller
            // TODO: Could be better to keep them to at least contian data like 'start delay' and shit
            foreach (var childVFX in GetComponentsInChildren<VFXController>())
            {
                if (childVFX != this)
                    childVFX.enabled = false;
            }
            
            base.Start();
        }

        public override void InitFXData()
        {
            var prevAnimators = m_animators.ToList();
            var prevParticleSystems = m_particleSystems.ToList();
            
            GetComponentsInChildren(m_animators);
            GetComponentsInChildren(m_particleSystems);
            
            var prevLooping = m_isLooping;
            var prevDuration = m_playbackDuration;

            m_isLooping = false;
            
            // Figure out max duration, technically an expensive Awake but because of pooling we're fine this is only triggered once
            float maxDuration = -Mathf.Infinity;
            foreach (var animator in m_animators)
            {
                if (!animator.Clip) continue;

                var clip = animator.Clip;
                maxDuration = Mathf.Max(maxDuration, clip.length);
                m_isLooping = m_isLooping || animator.IsAnimationLooping;
            }

            foreach (var system in m_particleSystems)
            {
                maxDuration = Mathf.Max(maxDuration, system.main.duration);
                m_isLooping = m_isLooping || system.main.loop;
            }

            m_playbackDuration = maxDuration;
            
#if UNITY_EDITOR
            bool hasChanged = !Mathf.Approximately(prevDuration, m_playbackDuration) || prevLooping != m_isLooping;
            // Check if components changed too
            hasChanged = hasChanged || !prevAnimators.SequenceEqual(m_animators) || !prevParticleSystems.SequenceEqual(m_particleSystems);
            if (!Application.isPlaying && hasChanged)
            {
                Debug.Log($"[VFX Controller] Initialized VFX '{name}' with duration {m_playbackDuration}s (Looping: {m_isLooping})");
                EditorUtility.SetDirty(this);
            }
#endif            
        }

        internal override void Play_Internal(VFXParams playParams)
        {
            if (Application.isPlaying) // Only play anims in playmode, otherwise we use AnimationMode for editor previews and this conflicts with that
            {
                foreach (var animator in m_animators) animator.Play();
            }

            foreach (var system in m_particleSystems) system.Play(false); // w/ children false because technically we're gonna loop through them all & play them
        }

        internal override void Stop_Internal(bool immediate)
        {
            if (immediate)
            {
                foreach (var system in m_particleSystems) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                
                OnStopped();
            }
            else
            {
                foreach (var system in m_particleSystems) system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                StartCoroutine(WaitForParticlesToFinish());
            }
        }
        
        private IEnumerator WaitForParticlesToFinish()
        {
            // Wait until all particles are dead
            bool particlesAlive = true;
            bool animatorsAlive = true;
            while (particlesAlive || animatorsAlive)
            {
                particlesAlive = false;
                foreach (var system in m_particleSystems)
                {
                    if (system.IsAlive(true)) // Check children too
                    {
                        particlesAlive = true;
                        break;
                    }
                }

                animatorsAlive = false;
                foreach (var animator in m_animators)
                {
                    if (animator.IsPlaying)
                    {
                        animatorsAlive = true;
                        break;
                    }
                }
            
                if (particlesAlive || animatorsAlive)
                    yield return null; // Wait one frame and check again
            }
        
            OnStopped();
        }

#if UNITY_EDITOR
        /// <summary>
        /// If it does, we avoid calling Begin/End Sample for animation mode as its expected ocne per-frame and will be
        /// handled by the VFX director. Check is do we have a VFX director parent, and is it active (important as to allow
        /// for sub-vfx to individually preview their fx in the hiearchy)
        /// </summary>
        internal bool m_hasParentVFXDirector = false;

        /// <summary>
        /// Initializes data needed for preview playback in the editor. Must call before beginning preview simulation.
        /// </summary>
        public override void InitPreviewData()
        {
            // Begin particle system playback
            foreach (var system in m_particleSystems)
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.randomSeed = system.randomSeed; // This is important! If the setter is not called it'll randomly get a seed every frame it seems from editor playback
                system.Play(false);
            }
            
            var parentDirector = GetComponentInParent<VFXDirector>(false);
            m_hasParentVFXDirector = parentDirector != null && parentDirector.IsActive;
            
            base.InitPreviewData();
        }
        
        internal override void SimulateEditorPreviewUpdate()
        {
            if (EditorPlaybackTime >= EditorPlaybackDuration)
            {
                StopPreview();
                return;
            }
            
            Simulate(EditorPlaybackTime, EditorDeltaTime);
            
            base.SimulateEditorPreviewUpdate();
        }
        
        public override void Simulate(float playbackTime, float deltaTime)
        {
            if (AnimationMode.InAnimationMode() && !m_hasParentVFXDirector) AnimationMode.BeginSampling();
            
            foreach (var animator in m_animators)
            {
                float sampleTime = playbackTime * animator.Speed;
                
                if (animator.IsAnimationLooping && sampleTime >= animator.LoopEndTime)
                {
                    // Only apply looping after we've passed the loop end point once
                    float loopLength = animator.LoopEndTime - animator.LoopStartTime;
                    float timeIntoLoop = sampleTime - animator.LoopEndTime;  // How far past loop end
                    sampleTime = animator.LoopStartTime + (timeIntoLoop % loopLength);
                }
                
                if (AnimationMode.InAnimationMode())
                {
                    //AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, animator.Clip, sampleTime);
                    //AnimationMode.EndSampling();
                }
                else
                {
                    if (!animator.IsPlaying) animator.Play();
                    animator.Time = sampleTime;
                }
            }
            foreach (var system in m_particleSystems)
            {
                // Subsequent frames - simulate only the delta
                if (deltaTime > 0 && system.main.simulationSpace == ParticleSystemSimulationSpace.World) // Only simulate forward
                {
                    system.Simulate(deltaTime, false, restart: false);
                }
                else // Scrubbed backwards or not worldspace - i think needs restart always
                {
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.randomSeed = system.randomSeed;
                    system.Play(false);
                    system.Simulate(playbackTime, false, restart: true);
                }
                system.time = playbackTime;
            }
            
            if (AnimationMode.InAnimationMode() && !m_hasParentVFXDirector) AnimationMode.EndSampling();
        }
#endif        
    }
}