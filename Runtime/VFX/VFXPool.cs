using UnityEngine;
using UnityEngine.SceneManagement;

namespace FS.Rendering
{
    public class VFXPool
    {
        private UnityEngine.Pool.ObjectPool<VFXController> m_pool;
        private VFXController m_prefab;

        public VFXPool(VFXController prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
        {
            m_prefab = prefab;
            m_pool = new UnityEngine.Pool.ObjectPool<VFXController>(CreatePooledFX, GetPooledFX, ReleasePooledFX, DestroyPooledFX, collectionCheck, defaultCapacity, maxSize);
        }

        public VFXController Get() => m_pool.Get();
        public void Release(VFXController fxInst) => m_pool.Release(fxInst);
        public void Dispose() => m_pool.Dispose();
        public int CountAll => m_pool.CountAll;
        public int CountActive => m_pool.CountActive;
        public int CountInactive => m_pool.CountInactive;
        
        private void DestroyPooledFX(VFXController fxInst)
        {
            GameObject.Destroy(fxInst);
        }

        private void ReleasePooledFX(VFXController fxInst)
        {
            fxInst.gameObject.SetActive(false);
            // set parent back to the manager (tho we're DontDestroyOnLoad so how does reparenting across scenes work? Is that fine or do we need to move it between scenes)
            fxInst.transform.SetParent(VFXManager.PoolContainer, false);
            fxInst.transform.localPosition = Vector3.zero;
            fxInst.transform.localRotation = Quaternion.identity;
            fxInst.StopAllCoroutines();
        }

        private void GetPooledFX(VFXController fxInst)
        {
            fxInst.gameObject.SetActive(true);
        }

        private VFXController CreatePooledFX()
        {
            var fxInst = GameObject.Instantiate(m_prefab);
            fxInst.m_vfxPool = this;
            fxInst.gameObject.SetActive(false);
            return fxInst;
        }
    }

}