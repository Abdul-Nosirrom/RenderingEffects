using UnityEngine;
using UnityEngine.SceneManagement;

namespace FS.Rendering
{
    public class VFXPool
    {
        private UnityEngine.Pool.ObjectPool<VFXBase> m_pool;
        private VFXBase m_prefab;

        public VFXPool(VFXBase prefab, bool collectionCheck = true, int defaultCapacity = 10, int maxSize = 10000)
        {
            m_prefab = prefab;
            m_pool = new UnityEngine.Pool.ObjectPool<VFXBase>(CreatePooledFX, GetPooledFX, ReleasePooledFX, DestroyPooledFX, collectionCheck, defaultCapacity, maxSize);
        }

        public VFXBase Get() => m_pool.Get();
        public void Release(VFXBase fxInst) => m_pool.Release(fxInst);
        public void Dispose() => m_pool.Dispose();
        public int CountAll => m_pool.CountAll;
        public int CountActive => m_pool.CountActive;
        public int CountInactive => m_pool.CountInactive;
        
        private void DestroyPooledFX(VFXBase fxInst)
        {
            GameObject.Destroy(fxInst);
        }

        private void ReleasePooledFX(VFXBase fxInst)
        {
            //fxInst.gameObject.SetActive(false); VFX Controller now sets this OnStopped
            // set parent back to the manager (tho we're DontDestroyOnLoad so how does reparenting across scenes work? Is that fine or do we need to move it between scenes)
            fxInst.transform.SetParent(VFXManager.PoolContainer, false);
            fxInst.transform.localPosition = Vector3.zero;
            fxInst.transform.localRotation = Quaternion.identity;
            fxInst.transform.localScale = m_prefab.transform.localScale;
            fxInst.StopAllCoroutines(); // Maybe redundant but safety
        }

        private void GetPooledFX(VFXBase fxInst)
        {
            //fxInst.gameObject.SetActive(true); VFX Controller now sets this OnPlay
        }

        private VFXBase CreatePooledFX()
        {
            var fxInst = GameObject.Instantiate(m_prefab);
            fxInst.m_vfxPool = this;
            fxInst.gameObject.SetActive(false);
            return fxInst;
        }
    }

}