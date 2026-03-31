using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace FS.Rendering
{
    [ExecuteAlways]
    [RequireComponent(typeof(TubeRenderer))]
    [Icon("Packages/com.abdulal.rendereffects/Editor/Resources/Icons/TubeTrailEmitter Icon.png")]
    [AddComponentMenu("Free Skies/Effects/TubeTrailEmitter")]
    public class TubeTrailEmitter : MonoBehaviour
    {
        private enum UpdateMode { Update, FixedUpdate }
        private enum EmissionMode { Time, Distance }
        
        [FoldoutGroup("Emission")] 
        [SerializeField, Tooltip("Whether to emit segments based on travel distance or time")]
        private EmissionMode m_emissionMode = EmissionMode.Time;
        
        [FoldoutGroup("Emission")] 
        [SerializeField, Tooltip("Rate at which segments are added, unit is frames/segment. Emitting a point every N frames"), Range(1, 60)]
        [ShowIf("ShowEmissionRate")]
        private uint m_emissionRate = 1;

        [FoldoutGroup("Emission")] 
        [SerializeField, Tooltip("Emit a segment every 'm_emissionDistance' travelled"), Range(0, 5)]
        [ShowIf("ShowEmissionDistance")]
        private float m_emissionDistance = 1;

        private bool ShowEmissionRate => m_emissionMode == EmissionMode.Time;
        private bool ShowEmissionDistance => m_emissionMode == EmissionMode.Distance;
        
        /// <summary>
        /// Flag when Start/Stop Emission is called so we know when to add or not add points (emit points)
        /// </summary>
        private bool m_isEmitting;

        private UpdateMode m_updateMode;

        private Vector3 m_lastEmissionPosition;
        private int m_frameCounter;
        
        private TubeRenderer m_tubeRenderer;
        public TubeRenderer TubeRenderer
        {
            get
            {
                if (m_tubeRenderer == null) m_tubeRenderer = GetComponent<TubeRenderer>();
                return m_tubeRenderer;
            }
        }
        
        [HorizontalGroup("Controls"), Button]
        public void StartEmission()
        {
            if (m_isEmitting) return;
            enabled = true;
        }

        [HorizontalGroup("Controls"), Button]
        public void StopEmission() => StopEmission(false);
        
        public void StopEmission(bool immediate)
        {
            if (!m_isEmitting) return;
            if (immediate) TubeRenderer.ClearSegments();
            enabled = false;
        }

        private void Awake()
        {
            m_updateMode = GetComponentInParent<Rigidbody>() || GetComponentInParent<Rigidbody2D>() 
                ? UpdateMode.FixedUpdate : UpdateMode.Update;
        }

        // On Enable/Disable is essentially equivalent to calling Start/Stop emission
        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorApplication.update += UpdateEmission;
#endif
            m_isEmitting = true;
            m_lastEmissionPosition = transform.position;
            m_frameCounter = 0;
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= UpdateEmission;
#endif
            m_isEmitting = false;
        }
        
        private void LateUpdate() // For animation-syncing on same-frame
        {
            if (Application.isPlaying && m_updateMode == UpdateMode.Update) UpdateEmission();
        }

        private void FixedUpdate()
        {
            if (Application.isPlaying && m_updateMode == UpdateMode.FixedUpdate) UpdateEmission();
        }
        
        private void UpdateEmission()
        {
            if (m_isEmitting)
            {
                switch (m_emissionMode)
                {
                    case EmissionMode.Time: EmitTimeBasedPoints(); break;
                    case EmissionMode.Distance: EmitDistanceBasedPoints(); break;
                }
            }

            m_frameCounter++;
        }

        private void EmitTimeBasedPoints()
        {
            if (m_frameCounter % m_emissionRate != 0) return;
            TubeRenderer.AddPoint(transform.position); // Simple
        }

        private void EmitDistanceBasedPoints()
        {
            if (TubeRenderer.SegmentCount <= 0)
            {
                TubeRenderer.AddPoint(transform.position);
                m_lastEmissionPosition = transform.position;
                return;
            }
            
            // Get previous segments position and compare it to our transform
            Vector3 prevPos = m_lastEmissionPosition;//TubeRenderer.GetSegment(TubeRenderer.SegmentCount - 1).Position;
            Vector3 currentPos = transform.position;
            
            // Early out if distance covered isn't enough
            float distance = Vector3.Distance(prevPos, currentPos);
            if (distance < m_emissionDistance)
            {
                // Set last point to current transform to get it feeling 'smooth'? Still triggers a rebuild tho
                // TODO: This causes our distance checks for emitting new points to be unreliable. We need to cache "New Point Position" or something to check 'where did we last add a point'
                TubeRenderer.SetPoint(TubeRenderer.SegmentCount - 1, transform.position);
                return;
            }
            
            // Reset last point to its emission position, we might've been moving it for the smoothness
            if (TubeRenderer.SegmentCount >= 2)
                TubeRenderer.SetPoint(TubeRenderer.SegmentCount - 1, m_lastEmissionPosition);
            
            // Do we have 3 segments? If so we can subdivide the curve properly if distance >> emissionDistance this frame
            if (TubeRenderer.SegmentCount >= 3 && distance > 2 * m_emissionDistance) // rough number
            {
                Vector3 prevPrevPos = TubeRenderer.GetSegment(TubeRenderer.SegmentCount - 2).Position;
    
                // Tangent at prevPos: derived from the direction we were already traveling
                Vector3 tangentAtPrev = (prevPos - prevPrevPos).normalized * distance;
                // Tangent at currentPos: assume continuing in the same direction
                Vector3 tangentAtCurrent = (currentPos - prevPos).normalized * distance;
    
                int numNewSegments = Mathf.FloorToInt(distance / m_emissionDistance);
                for (int s = 1; s <= numNewSegments; s++)
                {
                    float t = (float)s / numNewSegments;
                    Vector3 pos = CubicHermite(prevPos, tangentAtPrev, currentPos, tangentAtCurrent, t);
                    TubeRenderer.AddPoint(pos);
                }
            }
            else
            {
                TubeRenderer.AddPoint(transform.position); // Simple
            }
            
            m_lastEmissionPosition = currentPos;
        }
        
        private static Vector3 CubicHermite(Vector3 p0, Vector3 t0, Vector3 p1, Vector3 t1, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
    
            float h00 = 2 * t3 - 3 * t2 + 1;  // basis for p0
            float h10 = t3 - 2 * t2 + t;       // basis for t0
            float h01 = -2 * t3 + 3 * t2;      // basis for p1
            float h11 = t3 - t2;               // basis for t1
    
            return h00 * p0 + h10 * t0 + h01 * p1 + h11 * t1;
        }
        
#if UNITY_EDITOR
        public void EmitPreview(float distance)
        {
            int numPoints = Mathf.Max(3, Mathf.CeilToInt(distance / m_emissionDistance));
            var origin = transform.position;
            var dir = -transform.forward;
    
            var points = new Vector3[numPoints];
            for (int i = 0; i < numPoints; i++)
            {
                float t = (float)i / (numPoints - 1);
                points[i] = origin + dir * (distance * t);
            }
            
            TubeRenderer.SetPoints(points);
        }
#endif         
    }
}