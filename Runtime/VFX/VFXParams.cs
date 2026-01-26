using System;
using Sirenix.OdinInspector;
using UnityEngine;

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
        public Vector3 m_localScale;

        [ShowIf("m_shouldParent"), Range(0, 1)]
        public float m_deparentTime;

        public void ConfigureFX(VFXBase fx)
        {
            if (fx == null) return;
            
            Vector3 spawnPos = m_localPositionOffset;
            Quaternion spawnRot = m_localRotationOffset;
            Vector3 spawnScale = Vector3.Scale(fx.gameObject.transform.localScale, m_localScale == Vector3.zero ? Vector3.one : m_localScale);
            
            var parentTransform = m_parent;

            if (m_shouldParent && parentTransform)
            {
                fx.transform.SetParent(parentTransform, false);
                fx.transform.localPosition = spawnPos;
                fx.transform.localRotation = spawnRot;
                fx.transform.localScale = spawnScale;
            }
            else
            {
                fx.transform.SetParent(null, false);
                fx.transform.localScale = spawnScale;

                if (parentTransform) // Use parent as spawn origin
                {
                    fx.transform.position = parentTransform.TransformPoint(spawnPos);
                    fx.transform.rotation = parentTransform.rotation * spawnRot; // BUG: rotation initializer seems broken?
                }
                else // No parent - spawn at world position/rotation
                {
                    fx.transform.position = spawnPos;
                    fx.transform.rotation = spawnRot;
                }
            }
        }
    }
}