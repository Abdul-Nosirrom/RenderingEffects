using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS.Rendering
{
    public class VFXManager : MonoBehaviour
    {
        private static VFXManager m_instance;
        private static Transform m_poolContainer;

        public static VFXManager Instance
        {
            get
            {
                if (m_instance == null)
                {
                    var go = new GameObject("VFXManager");
                    DontDestroyOnLoad(go);
                    m_instance = go.AddComponent<VFXManager>();
                    
                    // Pool container
                    m_poolContainer = new GameObject("VFXPoolContainer").transform;
                    m_poolContainer.SetParent(go.transform);
                    m_poolContainer.localPosition = Vector3.zero;
                    m_poolContainer.localRotation = Quaternion.identity;
                    m_poolContainer.localScale = Vector3.one;
                }

                return m_instance;
            }
        }
        public static Transform PoolContainer => m_poolContainer;

        private Dictionary<int, VFXPool> m_pool = new();

        private void OnDestroy()
        {
            foreach (var pool in m_pool.Values) pool?.Dispose();
            m_pool.Clear();
#if UNITY_EDITOR
            m_poolDebug.Clear();
#endif            
        }

        public VFXBase PlayVFX(VFXBase fxPrefab, VFXParams playParams = default, VFXDirector parentDirector = null)
        {
            if (fxPrefab == null)
            {
                Debug.LogWarning($"[VFXManager] Attempted to play a VFX with a null prefab!");
                return null;
            }
            
            if (!m_pool.ContainsKey(fxPrefab.GetInstanceID()))
            {
                var fxPool = new VFXPool(fxPrefab, maxSize: 10);
                m_pool.Add(fxPrefab.GetInstanceID(), fxPool);
#if UNITY_EDITOR
                m_poolDebug.Add(fxPrefab.GetInstanceID(), fxPrefab.name);           
#endif                
            }

            var fx = m_pool[fxPrefab.GetInstanceID()].Get();
            fx.m_parentDirector = parentDirector;
            fx.Play(playParams);
            return fx;
        }
        
#if UNITY_EDITOR
        private readonly Dictionary<int, string> m_poolDebug = new();
        [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        private Dictionary<string, string> PoolStats
        {
            get
            {
                var stats = new Dictionary<string, string>();
                foreach (var kvp in m_pool)
                {
                    var pool = kvp.Value;
                    stats[m_poolDebug[kvp.Key]] = $"Active: {pool.CountActive} | Inactive: {pool.CountInactive} | Total: {pool.CountAll}";
                }
                return stats;
            }
        }
#endif
    }
}