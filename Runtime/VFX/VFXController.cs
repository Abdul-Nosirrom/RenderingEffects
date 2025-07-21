using System;
using System.Collections;
using System.Collections.Generic;
using Animancer;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FS.Rendering
{
    [Serializable]
    public struct VFXParams
    {
        public bool m_shouldParent;

        // If not parented, we just spawn the FX at this pos & rotation w/ the given local offsets
        public Transform m_parent;
        public Vector3 m_localPositionOffset;
        public Quaternion m_localRotationOffset;

        [ShowIf("m_shouldParent"), Range(0, 1)] public float m_deparentTime;
        
        public void ConfigureFX(VFXController fx)
        {
            if (m_shouldParent && m_parent)
            {
                fx.transform.SetParent(m_parent, false);
            }
            else fx.transform.SetParent(null, false); // No parent in this case
            
            Vector3 spawnPos = m_localPositionOffset;
            Quaternion spawnRot = m_localRotationOffset;

            if (m_parent && !m_shouldParent)
            {
                spawnPos = m_parent.transform.TransformPoint(spawnPos);
                spawnRot = m_parent.transform.rotation * spawnRot;
            }
            
            fx.transform.localPosition = spawnPos;
            fx.transform.localRotation = spawnRot;
        }
    }
    
    // Controls playing back the VFX & all that shit dw about this part for now
    public class VFXController : MonoBehaviour
    {
        public VFXPool m_vfxPool;
        
        [SerializeField, ReadOnly] private bool m_isLooping;
        [SerializeField, ReadOnly] private float m_playbackDuration = -1;
        
        [SerializeField, HideInInspector] private List<SoloAnimation> m_animators = new();
        [SerializeField, HideInInspector] private List<ParticleSystem> m_particleSystems = new();

        private void Awake()
        {
            InitFXData();
        }

        [Button]
        private void InitFXData() // Todo: make this a button in the editor
        {
            GetComponentsInChildren(m_animators);
            GetComponentsInChildren(m_particleSystems);

            m_isLooping = false;
            
            // Figure out max duration, technically an expensive Awake but because of pooling we're fine this is only triggered once
            float maxDuration = -Mathf.Infinity;
            foreach (var animator in m_animators)
            {
                if (!animator.Clip) continue;
                
                maxDuration = Mathf.Max(maxDuration, animator.Length);
                m_isLooping = m_isLooping || animator.IsLooping;
            }

            foreach (var system in m_particleSystems)
            {
                maxDuration = Mathf.Max(maxDuration, system.main.duration);
                m_isLooping = m_isLooping || system.main.loop;
            }

            m_playbackDuration = maxDuration;
            
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(this);
#endif            
        }

        public void Play(VFXParams playParams = default)
        {
            StopAllCoroutines();
            
            // Start up animation either via VFX system, Animator, or particle system callback and get the length of it
            playParams.ConfigureFX(this);
            
            foreach (var animator in m_animators) animator.Play();
            foreach (var system in m_particleSystems) system.Play(false); // w/ children false because technically we're gonna loop through them all & play them
            
            StartCoroutine(Destroy(m_playbackDuration));
            StartCoroutine(DeParent(playParams.m_deparentTime));
        }

        private IEnumerator DeParent(float normalizedTime)
        {
            yield return new WaitForSeconds(normalizedTime * m_playbackDuration);
            transform.SetParent(null);
        }

        private IEnumerator Destroy(float time)
        {
            yield return new WaitForSeconds(time);
            VFXManager.Instance.StopFX(this);
        }
    }
}