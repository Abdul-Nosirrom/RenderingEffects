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
        }

        public void PlayVFX(VFXController fxPrefab, VFXParams playParams = default)
        {
            var fxID = fxPrefab.GetInstanceID();
            if (!m_pool.ContainsKey(fxID))
            {
                var fxPool = new VFXPool(fxPrefab, maxSize: 10);
                m_pool.Add(fxID, fxPool);
            }

            var fx = m_pool[fxID].Get();
            fx.Play(playParams);
        }

        public void StopFX(VFXController fxInstance)
        {
            fxInstance.m_vfxPool?.Release(fxInstance);
        }
    }
}