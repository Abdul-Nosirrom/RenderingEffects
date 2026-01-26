using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace FS.Rendering
{
    /// <summary>
    /// Base class for VFX, this is what gets pooled.
    /// </summary>
    [ExecuteInEditMode]
    public abstract class VFXBase : MonoBehaviour
    {
        internal VFXPool m_vfxPool;
        /// <summary>
        /// Keeping track of whether a director is keeping a ref to a pooled effect for its cleanup routine
        /// </summary>
        internal VFXDirector m_parentDirector;
        
        [SerializeField] protected bool m_playOnStart = false;
        [SerializeField, ReadOnly] protected bool m_isLooping;
        [SerializeField, ReadOnly] protected float m_playbackDuration = -1;
        
        public bool IsActive { get; private set; }
        public bool IsStopping { get; private set; }
        public bool IsPooled => m_vfxPool != null;
        public bool IsLooping => m_isLooping;
        public float PlaybackDuration => m_playbackDuration;
        
        
        // Needed to reset transforms of non-pooled fx, we cache these on play and reset on stop
        protected Vector3 m_initialPosition;
        protected Quaternion m_initialRotation;
        protected Vector3 m_initialScale;
        protected IEnumerator m_deparentCoroutine;

        [Button(DirtyOnClick = false)]
        public abstract void InitFXData();
        protected abstract bool IsInitialized { get; }

        protected virtual void Start()
        {
            if (!IsPooled && !IsActive && Application.isPlaying)
            {
                if (m_playOnStart) 
                    Play();
                else
                    gameObject.SetActive(false);
            }
        }

        public void Play(VFXParams playParams = default)
        {
            if (!IsInitialized) InitFXData();
            
            if (IsActive)
            {
                // For non-pooled, restart is valid, for pooled this is a bug
                if (IsPooled)
                    Debug.LogError($"[VFX Controller] Pool returned active VFX {name}! Pooling bug.");
                
                // Allow restart of non-pooled FX
                Stop(immediate: true);
            }

            IsActive = true;
            IsStopping = false;
            
            if (!gameObject.activeSelf && Application.isPlaying)
                gameObject.SetActive(true);
            
            // Cache initial transform for non-pooled FX
            if (!IsPooled && Application.isPlaying)
            {
                m_initialPosition = transform.localPosition;
                m_initialRotation = transform.localRotation;
                m_initialScale = transform.localScale;
            }
            
            // Start up animation either via VFX system, Animator, or particle system callback and get the length of it
            // NOTE: This should be fine for animation preview because there we manually call configure FX and never really go through this function
            if (IsPooled) playParams.ConfigureFX(this); // NOTE: For non-pooled VFX, transforms are already preconfigured

            Play_Internal(playParams);
            
            if (!m_isLooping && Application.isPlaying) 
                StartCoroutine(WaitForFinish(m_playbackDuration));
            if (playParams.m_parent && playParams.m_shouldParent && playParams.m_deparentTime > 0) 
                StartCoroutine((m_deparentCoroutine = DeParent(playParams.m_deparentTime)));
            
#if UNITY_EDITOR
            // For button preview
            if (!Application.isPlaying && m_parentDirector == null)
            {
                if (!AnimationMode.InAnimationMode())
                    AnimationMode.StartAnimationMode();
                
                EditorApplication.update += SimulateEditorPreviewUpdate;
            }
#endif
        }
        internal abstract void Play_Internal(VFXParams playParams);

        public void Stop(bool immediate = false)
        {
            if (!IsActive) return;

            IsStopping = true;
            
            StopAllCoroutines();
            
            // Manually stop the deparent coroutine to trigger the finally block - meaning we deparent when stopping early if we havent hit the deparent marker
            if (m_deparentCoroutine != null) Debug.LogError($"[VFX Controller] Deparenting early due to active deparent coroutine on VFX {name}");
            ((IDisposable)m_deparentCoroutine)?.Dispose();

            // Call this after StopAllCoroutines as we can start another coroutine if non-immediate (wait for particles to finish but stop emission for example)
            Stop_Internal(immediate);
        }
        internal abstract void Stop_Internal(bool immediate);

        /// <summary>
        /// Called when effect finishes (either immediately or naturally). Cleans up.
        /// </summary>
        protected void OnStopped()
        {
            IsActive = false;
            if (IsPooled) m_parentDirector = null;
            if (Application.isPlaying) gameObject.SetActive(false);
            
            // Only return to pool if this VFX is pooled
            if (IsPooled)
                m_vfxPool.Release(this);
            else if (Application.isPlaying)
            {
                // Reset transform for non-pooled FX
                transform.localPosition = m_initialPosition;
                transform.localRotation = m_initialRotation;
                transform.localScale = m_initialScale;
            }
            
#if UNITY_EDITOR
            if (!Application.isPlaying && m_parentDirector == null)
            {
                EditorApplication.update -= SimulateEditorPreviewUpdate;
                if (AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
            }
#endif      
        }
        
        private IEnumerator DeParent(float normalizedTime)
        {
            try
            {
                yield return new WaitForSeconds(normalizedTime * m_playbackDuration);
            }
            finally
            {
                transform.SetParent(null);
                m_deparentCoroutine = null;
            }
        }
        
        private IEnumerator WaitForFinish(float time)
        {
            yield return new WaitForSeconds(time);
            Stop();
        }

        
#if UNITY_EDITOR
        internal float m_editorPreviewStartTime = 0f;
        internal float m_editorPreviewCurrentTime = 0f;
        internal float EditorPlaybackTime => ((float)EditorApplication.timeSinceStartup - m_editorPreviewStartTime);
        /// NOTE: Set EditorPreviewCurrentTime after the end of SimulateEditorPreviewUpdate to get correct delta times
        internal float EditorDeltaTime => EditorPlaybackTime - m_editorPreviewCurrentTime;
        internal float EditorPlaybackDuration => IsLooping ? 5f : m_playbackDuration; // Arbitrary 5s for looping previews
        
        // Slider control would be nice we have the infrastructure for that with the Simulate callback so why not
        [ShowInInspector, Range(0, 1)]
        private float m_playbackPreviewTime
        {
            get => Mathf.Clamp01(m_editorPreviewCurrentTime / EditorPlaybackDuration);
            set
            {
                m_editorPreviewCurrentTime = value * EditorPlaybackDuration;
                m_editorPreviewStartTime = (float)EditorApplication.timeSinceStartup - m_editorPreviewCurrentTime; // Reset start time so we can get correct delta times
            }
        }
        
        [HorizontalGroup("Previews")]
        [Button(DirtyOnClick = false, Icon = SdfIconType.Play)]
        internal void PlayPreview()
        {
            InitPreviewData();
            Play();
        }

        [HorizontalGroup("Previews")]
        [Button(DirtyOnClick = false, Icon = SdfIconType.Stop)]
        internal void StopPreview() => Stop(immediate: true);

        public virtual void InitPreviewData()
        {
            m_editorPreviewStartTime = (float)EditorApplication.timeSinceStartup;
            m_editorPreviewCurrentTime = 0;
        }
        public abstract void Simulate(float playbackTime, float deltaTime);
        internal virtual void SimulateEditorPreviewUpdate()
        {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); // Force repaint of inspector to update slider
            m_editorPreviewCurrentTime = EditorPlaybackTime;
        }
#endif
    }
}