using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace FS.Rendering
{
    [Serializable]
    internal class VFXDirectorEntry
    {
        [Header("Source"), ChildGameObjectsOnly]
        [Tooltip("Child VFX Controller that'll be played back, a copy instance is insantiated and pooled of this configuration." +
                 " Disabled at runtime.")]
        public VFXBase m_template;
     
        [InfoBox("$InstanceID")]
        public int InstanceID => m_template?.GetInstanceID() ?? -1;
        [ShowInInspector] public string InstanceIDStr => InstanceID.ToString();
        
        [Header("Timing")]//, Range(0, 1)]
        [Tooltip("Seconds after director begins playing to play this VFX")]
        public float m_startDelay = 0f;
        [Range(0, 1)]
        public float m_deparentTime;

        [Header("Loop Behavior")]
        public LoopBehavior m_loopBehavior = LoopBehavior.Inherit;
        
        [ShowIf("@m_loopBehavior == LoopBehavior.LoopForDuration || m_loopBehavior == LoopBehavior.RepeatForDuration")]
        [Tooltip("Total duration before stopping (seconds)")]
        public float m_loopDuration;
        [ShowIf("@m_loopBehavior == LoopBehavior.RepeatWithDirector || m_loopBehavior == LoopBehavior.RepeatForDuration")]
        [Tooltip("Interval between re-triggers (seconds)")]
        public float m_repeatInterval;

        [Header("Runtime")] 
        [NonSerialized] public HashSet<VFXBase> m_runtimeInstances = new();
        //[NonSerialized] public VFXBase m_runtimeInstance;
        [NonSerialized] public IEnumerator m_startCoroutine; // Coroutine for handling the start delay
        [NonSerialized] public IEnumerator m_stopCoroutine;

        public bool IsPlaying
        {
            get
            {
                foreach (var instance in m_runtimeInstances) 
                    if (instance != null && instance.IsActive) return true;
                return false;
            }
        }

        public enum LoopBehavior
        {
            Inherit,              // Natural behavior
            LoopForDuration,      // Looping children: loop for m_duration then stop
            RepeatWithDirector,   // One-shot children: re-trigger every m_repeatInterval until director stops
            RepeatForDuration,    // One-shot children: re-trigger every m_repeatInterval for m_duration total
        }

        public void Play(VFXDirector director, VFXParams playParams)
        {
            if (m_template == null || !Application.isPlaying) return;

            m_runtimeInstances ??= new();

            if (m_runtimeInstances.Count > 0)
            {
                Debug.LogError($"[VFX] VFX Director Entry contains stale runtime instances: {m_runtimeInstances.Count} instances still active.");
                foreach (var instance in m_runtimeInstances)
                {
                    if (instance != null && instance.IsActive && instance.m_parentDirector == director)
                        instance.Stop(true);
                }
            }
            director.StartCoroutine(m_startCoroutine = PlayDirectorEntry(director, playParams));
        }

        public void Stop(VFXDirector director, bool immediate)
        {
            foreach (var instance in m_runtimeInstances)
            {
                if (instance != null && instance.IsActive && instance.m_parentDirector == director)
                {
                    //if (!immediate) no need, if director is non-immediate its gonna wait for everyone to finish 
                    //    instance.transform.SetParent(null, true); // Deparent runtime instances early as director may be returned to pool
                    instance.Stop(immediate); // only stop if its looping and active if were non-immediate, tricky thing is ensuring we still own the pool ref
                }
            }
            ((IDisposable)m_startCoroutine)?.Dispose();
            ((IDisposable)m_stopCoroutine)?.Dispose();
            m_startCoroutine = m_stopCoroutine = null;
            
            if (immediate) Clear();
        }

        public void Clear()
        {
            m_runtimeInstances.Clear();
        }

        // TODO: Needs some work esp when previews call Stop, probably works well for animation previews however.
        public float GetEffectivePreviewTime(float directorTime)
        {
            float localTime = directorTime - m_startDelay;
            switch (m_loopBehavior)
            {
                case LoopBehavior.Inherit:
                case LoopBehavior.LoopForDuration:
                    return localTime;
                
                case LoopBehavior.RepeatWithDirector:
                case LoopBehavior.RepeatForDuration:
                    return Mathf.Clamp(localTime % m_repeatInterval, 0f, m_repeatInterval); // Slight offset for editor previewing not to call Stop
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private IEnumerator PlayDirectorEntry(VFXDirector director, VFXParams playParams)
        {
            // Maybe try/finally to stop loops
            yield return new WaitForSeconds(m_startDelay);
            
            playParams.m_shouldParent = true;
            playParams.m_parent = m_template.transform.parent;
            playParams.m_localPositionOffset = m_template.transform.localPosition;
            playParams.m_localRotationOffset = m_template.transform.localRotation;
            playParams.m_localScale = m_template.transform.localScale;
            playParams.m_deparentTime = m_deparentTime;

            switch (m_loopBehavior)
            {
                case LoopBehavior.Inherit:
                    m_runtimeInstances.Add(VFXManager.Instance.PlayVFX(m_template, playParams, director));
                    break;
                case LoopBehavior.LoopForDuration:
                    var instance = VFXManager.Instance.PlayVFX(m_template, playParams, director);
                    m_runtimeInstances.Add(instance);
                    yield return new WaitForSeconds(m_loopDuration);
                    instance.Stop(false);
                    break;
                case LoopBehavior.RepeatWithDirector:
                    while (director.IsActive)
                    {
                        m_runtimeInstances.Add(VFXManager.Instance.PlayVFX(m_template, playParams, director));
                        yield return new WaitForSeconds(m_repeatInterval);
                    }
                    break;
                case LoopBehavior.RepeatForDuration:
                    int repeatCount = 0;
                    while (director.IsActive && (repeatCount * m_repeatInterval) < m_loopDuration)
                    {
                        m_runtimeInstances.Add(VFXManager.Instance.PlayVFX(m_template, playParams, director));
                        repeatCount++;
                        yield return new WaitForSeconds(m_repeatInterval);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
    
    public class VFXDirector : VFXBase
    {
        [SerializeField, TableList]
        private List<VFXDirectorEntry> m_subVFX;
        
#if UNITY_EDITOR
        private void OnValidate() => InitFXData();
#endif        
        
        public override void InitFXData()
        {
            // Gather all child VFXBase, excluding self or children of a sub-director
            var childTemplates = GetComponentsInChildren<VFXBase>(includeInactive: true)
                .Where(vfx => 
                {
                    if (vfx == this) return false;
        
                    // Find the closest parent director (excluding self if vfx is a director)
                    var parentDirectors = vfx.GetComponentsInParent<VFXDirector>(includeInactive: true);
        
                    // For a VFXDirector, parentDirectors[0] is itself, so check parentDirectors[1]
                    // For a VFXController, parentDirectors[0] is the immediate parent director
                    var immediateParentDirector = vfx is VFXDirector 
                        ? (parentDirectors.Length > 1 ? parentDirectors[1] : null)
                        : (parentDirectors.Length > 0 ? parentDirectors[0] : null);
            
                    return immediateParentDirector == this;
                })
                .ToList();
            
            // Build lookup of existing entries by template reference
            var existingEntries = new Dictionary<VFXBase, VFXDirectorEntry>();
            foreach (var entry in m_subVFX)
            {
                if (entry.m_template != null)
                    existingEntries.TryAdd(entry.m_template, entry);
            }
            
            // Rebuild list: preserve existing entry data, add new entries for new templates
            var newList = new List<VFXDirectorEntry>();
            foreach (var template in childTemplates)
            {
                if (existingEntries.TryGetValue(template, out var existing))
                {
                    newList.Add(existing);
                    existingEntries.Remove(template);  // Mark as used
                }
                else
                {
                    // New template found, create default entry
                    newList.Add(new VFXDirectorEntry
                    {
                        m_template = template,
                        m_startDelay = 0f,
                        m_loopBehavior = VFXDirectorEntry.LoopBehavior.Inherit
                    });
                }
            }
            
            m_subVFX = newList;
            
            // Disable templates at runtime (keep enabled in editor for preview)
            if (Application.isPlaying)
            {
                foreach (var entry in m_subVFX)
                {
                    if (entry.m_template != null)
                        entry.m_template.gameObject.SetActive(false);
                }
            }
            
            // Calculate duration and looping
            CalculateDuration();
            
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void CalculateDuration()
        {
            float maxEndTime = 0f;
            bool hasLoopingChild = false;
    
            foreach (var entry in m_subVFX)
            {
                if (entry.m_template == null) continue;
                entry.m_template.InitFXData();

                bool entryIsLooping = GetEffectiveLooping(entry);
                hasLoopingChild = hasLoopingChild || entryIsLooping;
        
                if (entryIsLooping) continue;
        
                float entryDuration = GetEffectiveDuration(entry);
                maxEndTime = Mathf.Max(maxEndTime, entry.m_startDelay + entryDuration);
            }
    
            m_isLooping = hasLoopingChild;
            m_playbackDuration = hasLoopingChild ? -1f : maxEndTime;
        }

        private bool GetEffectiveLooping(VFXDirectorEntry entry)
        {
            return entry.m_loopBehavior switch
            {
                VFXDirectorEntry.LoopBehavior.Inherit => entry.m_template.IsLooping,
                VFXDirectorEntry.LoopBehavior.LoopForDuration => false,  // Treated as one-shot
                VFXDirectorEntry.LoopBehavior.RepeatWithDirector => true, // Treated as looping
                VFXDirectorEntry.LoopBehavior.RepeatForDuration => false, // Treated as one-shot
                _ => entry.m_template.IsLooping
            };
        }

        private float GetEffectiveDuration(VFXDirectorEntry entry)
        {
            return entry.m_loopBehavior switch
            {
                VFXDirectorEntry.LoopBehavior.Inherit => entry.m_template.PlaybackDuration,
                VFXDirectorEntry.LoopBehavior.LoopForDuration => entry.m_loopDuration,
                VFXDirectorEntry.LoopBehavior.RepeatWithDirector => -1f,  // Infinite
                VFXDirectorEntry.LoopBehavior.RepeatForDuration => entry.m_loopDuration,
                _ => entry.m_template.PlaybackDuration
            };
        }

        protected override bool IsInitialized => m_subVFX.Count > 0; // could be a 'better' initialization metric but fine for now quick
        internal override void Play_Internal(VFXParams playParams)
        {
            if (Application.isPlaying)
            {
                foreach (var entry in m_subVFX)
                {
                    entry.Play(this, playParams);
                }
            }
#if UNITY_EDITOR // Reenable disabled sub-vfx
            else if (!Application.isPlaying)
            {
                foreach (var entry in m_subVFX)
                {
                    if (entry.m_template != null) entry.m_template.gameObject.SetActive(true);
                }
            }
#endif    
        }

        internal override void Stop_Internal(bool immediate)
        {
            foreach (var entry in m_subVFX)
            {
                if (!Application.isPlaying && entry.m_template != null)
                {
                    entry.m_template.gameObject.SetActive(true);
                    entry.m_template.Stop(immediate);
                }
                else
                {
                    entry.Stop(this, immediate); 
                }
            }
            
            // Can genuinely have this bit in VFXBases stop function as its kinda a repeated case
            if (immediate) OnStopped();
            else
            {
                // Wait for particles to finish
                StartCoroutine(WaitForSubVFXToFinish());
            }
            
#if UNITY_EDITOR // Reenable disabled sub-vfx
            if (!Application.isPlaying)
            {
                foreach (var entry in m_subVFX)
                {
                    if (entry.m_template != null) entry.m_template.gameObject.SetActive(true);
                }
            }
#endif            
        }

        private IEnumerator WaitForSubVFXToFinish()
        {
            bool AreAnyFXPlaying()
            {
                foreach (var entry in m_subVFX)
                {
                    if (entry.IsPlaying)
                        return true;
                }
                return false;
            }
            
            // Wait until all runtime instances are either null or inactive
            while (AreAnyFXPlaying()) yield return null;
            
            foreach (var entry in m_subVFX) entry.Clear(); // Clear runtime instances we dont own them when theyre pooled
            
            OnStopped();
        }

#if UNITY_EDITOR        
        private HashSet<VFXBase> m_previewInstances = new();
        
        public override void InitPreviewData()
        {
            m_previewInstances.Clear();
            
            // Disable all vfxs for previewing purposes
            foreach (var entry in m_subVFX)
            {
                if (entry.m_template != null)
                {
                    entry.m_template.m_parentDirector = this;
                    entry.m_template.gameObject.SetActive(false);
                }
            }
            
            base.InitPreviewData();
        }

        // This is called manually in animation previews, there we aren't in AnimationMode so no worries about begin/end sampling
        public override void Simulate(float playbackTime, float deltaTime)
        {
            foreach (var entry in m_subVFX)
            {
                float entryLocalTime = playbackTime - entry.m_startDelay;
                if (entry.m_template == null) continue;
                entryLocalTime = entry.GetEffectivePreviewTime(playbackTime);

                if (!entry.m_template.IsActive || entryLocalTime < 0 || entryLocalTime > entry.m_template.EditorPlaybackDuration) 
                    entry.m_template.gameObject.SetActive(false);
                else 
                    entry.m_template.Simulate(entryLocalTime, deltaTime);
            }
        }

        internal override void SimulateEditorPreviewUpdate()
        {
            bool hasParentDirector = m_parentDirector != null;
            if (!hasParentDirector) AnimationMode.BeginSampling();
            
            // assumption is they bind to editor stuff with play so we just gotta let em know
            foreach (var entry in m_subVFX)
            {
                if (entry.m_template == null) continue;
                if (entry.m_startDelay <= EditorPlaybackTime && m_previewInstances.Add(entry.m_template))
                {
                    entry.m_template.gameObject.SetActive(true);
                    entry.m_template.PlayPreview();
                }
                
                if (entry.m_template is VFXDirector && entry.m_template.IsActive) 
                    entry.m_template.SimulateEditorPreviewUpdate(); // Let it direct its childrens invocations

                entry.m_template.gameObject.SetActive(entry.m_template.IsActive);
            }
            
            // We're gonna end up calling Simulate on child director so when it enters its 'loop' from above, do not let it call it itself
            if (!hasParentDirector) Simulate(EditorPlaybackTime, EditorDeltaTime);
            
            if (!hasParentDirector) AnimationMode.EndSampling();
            
            if (EditorPlaybackTime >= EditorPlaybackDuration && !hasParentDirector)
                StopPreview();
            
            base.SimulateEditorPreviewUpdate();
        }
#endif    
    }
}