using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

#if UNITY_EDITOR
using Sirenix.OdinInspector;
using UnityEditor;
#endif

using ReadOnly = Unity.Collections.ReadOnlyAttribute;

namespace FS.Rendering
{
    /// <summary>
    /// If attached to a spline should we care? Or do we set it to 'manual' emission
    /// and then theres another component that adds points based on the spline? I think thats clean
    /// to not over-clutter this component?
    /// </summary>
    [DefaultExecutionOrder(10)]
    [ExecuteAlways]
    [Icon("Packages/com.abdulal.rendereffects/Editor/Resources/Icons/TubeRenderer Icon.png")]
    [AddComponentMenu("Free Skies/Effects/TubeRenderer")]
    public class TubeRenderer : MonoBehaviour
    {
        #region Lifetime

        public enum LifetimeMode { Persistent, Time, Distance }

        private bool IsTubeLifetimePersistent => m_lifetimeMode == LifetimeMode.Persistent;
        private bool IsTubeLifetimeTime => m_lifetimeMode == LifetimeMode.Time;
        private bool IsTubeLifetimeDistance => m_lifetimeMode == LifetimeMode.Distance;
        
        [FoldoutGroup("Lifetime")]
        [SerializeField, Tooltip("Persistent: Lives forever if untouched, essentially static mesh generation\n Time: Rings ages via time like a particle\n Rings age via distance, removing things further back than a specified distance.")]
        private LifetimeMode m_lifetimeMode = LifetimeMode.Persistent;

        [FoldoutGroup("Lifetime")]
        [SerializeField, Tooltip("Lifetime in seconds of an individual ring"), Min(0)]
        [ShowIf("IsTubeLifetimeTime")]
        private float m_lifetime = 1.0f;
        
        [FoldoutGroup("Lifetime")]
        [SerializeField, Tooltip("Maximum length of the tube renderer, after which we start deleting old particles")]
        [ShowIf("IsTubeLifetimeDistance")]
        private float m_lifetimeDistance = 1.0f;
        
        #endregion

        #region Curve Parametrization
        
        private enum CurveParameterization { Position, Age, Distance }

        [FoldoutGroup("Curves")]
        [SerializeField] private bool m_overrideCurveMode;

        [FoldoutGroup("Curves")]
        [SerializeField, ShowIf("m_overrideCurveMode")]
        private CurveParameterization m_curveMode;

        [FoldoutGroup("Curves")]
        [SerializeField, Tooltip("Lifetime in seconds of an individual ring"), Min(0)]
        [ShowIf("ExposeParametrizedLifetime")]
        private float m_parameterizedLifetime = 0.25f;

        private bool ExposeParametrizedLifetime => 
            m_overrideCurveMode && m_curveMode == CurveParameterization.Age && !IsTubeLifetimeTime;
        
        private float ParametrizedLifetime => ExposeParametrizedLifetime ? m_parameterizedLifetime : m_lifetime;
        
        private CurveParameterization ActiveCurveMode => m_overrideCurveMode
            ? m_curveMode
            : m_lifetimeMode switch
            {
                LifetimeMode.Persistent => CurveParameterization.Position,
                LifetimeMode.Time => CurveParameterization.Age,
                LifetimeMode.Distance => CurveParameterization.Distance,
                _ => CurveParameterization.Position
            };
        
        #endregion
        
        #region Geometry

        [FoldoutGroup("Geometry")] 
        [SerializeField, Tooltip("Maximum tube segments allowed, 0 means no limit. We automatically pop oldest segments when exceeded")]
        private int m_maxSegmentCount = 0;

        private int MaxSegmentCount => m_maxSegmentCount < 2 ? 1024 : m_maxSegmentCount;
        
        [FoldoutGroup("Geometry")] 
        [SerializeField, Tooltip("Base radius of an individual ring"), Range(0, 64)]
        private float m_radius = 1;
        
        [FoldoutGroup("Geometry")] 
        [SerializeField, Tooltip("Radius modulation over lifetime/distance of an individual radial segment")]
        private AnimationCurve m_radiusOverTrail = AnimationCurve.Constant(0, 1, 1);

        #endregion

        #region Cross-Section
        
        [FoldoutGroup("Cross-Section")] 
        [SerializeField, Tooltip("Cross-Section Resolution"), Range(3, 64)]
        private int m_radialSegments = 8;
        
        [FoldoutGroup("Cross-Section")] 
        [SerializeField, Tooltip("Sweep Angle for the tube, 360 is cylindrical, 180 is half"), MinMaxSlider(0, 360, true)]
        private Vector2 m_angleLimits = new Vector2(0, 360f);
        
        [FoldoutGroup("Cross-Section")] 
        [SerializeField, Range(-180, 180)]
        private float m_angleOffset = 0;

        [FoldoutGroup("Cross-Section")] 
        [SerializeField, Tooltip("Radius modulation over angle as we sweep the tube shape")]
        private AnimationCurve m_radiusOverCrossSection = AnimationCurve.Constant(0, 1, 1);

        private bool AddRadialSeam => m_angleLimits is {x: <= 0, y: >= 360f};
        
        #endregion

        #region Material & Colors

        [FoldoutGroup("Material & Colors")] 
        [SerializeField, Tooltip("Material of the tube-mesh")]
        private Material m_material;
        
        [FoldoutGroup("Material & Colors")] 
        [SerializeField, Tooltip("Vertex Color of the tube-mesh over lifetime/distance of the trail")]
        private Gradient m_vertexColorOverTrail;
        
        #endregion

        #region UV Settings

        private enum UVMode { Stretch, Tile }
        private bool UVTiles => m_uvMode == UVMode.Tile;
        
        [FoldoutGroup("UV Settings")]
        [SerializeField, Tooltip("Stretch: V goes [0,1] along the whole trail\n" +
                                 "Tile: V repeats per-distance")]
        private UVMode m_uvMode = UVMode.Stretch;

        [FoldoutGroup("UV Settings")]
        [SerializeField, Tooltip("Tiling per unity distance. V = segmentDistance / m_uvTilingLength")]
        [Range(0, 64)]
        private float m_uvTilingLength;
        
        #endregion

        #region Data

        [SerializeField, HideInInspector] private GameObject m_meshGO;
        [SerializeField, HideInInspector] private MeshFilter m_meshFilter;
        [SerializeField, HideInInspector] private MeshRenderer m_meshRenderer;
        [ShowInInspector] private Mesh m_mesh; // shouldnt be serialized

        private bool m_isDirty = false;
        private bool m_isInitialized = false;
        private Vector3 m_prevPos;

        public GameObject MeshGO
        {
            get
            {
                if (!m_meshGO) InitMeshComponents();
                return m_meshGO;
            }
        }

        public MeshFilter TubeMeshFilter
        {
            get
            {
                if (!m_meshFilter) InitMeshComponents();
                return m_meshFilter;
            }
        }

        public MeshRenderer TubeMeshRenderer
        {
            get
            {
                if (!m_meshRenderer) InitMeshComponents();
                return m_meshRenderer;
            }
        }

        public Mesh TubeMesh
        {
            get
            {
                if (!m_mesh) InitMeshComponents();
                return m_mesh;
            }
        }
        
#if UNITY_EDITOR
        private static float TIME => Application.isPlaying ? Time.time : (float)EditorApplication.timeSinceStartup;
#else
        private static float TIME => Time.time;
#endif

        #endregion
        
        #region Profiler Markers

        private static readonly ProfilerMarker s_dataBakingMarker =
            new ProfilerMarker("TubeRenderer.BakeOrientationsAndSegmentModulations");

        private static readonly ProfilerMarker s_parallelTransportMarker =
            new ProfilerMarker("TubeRenderer.ComputeParallelTransport");

        private static readonly ProfilerMarker s_meshRebuildMarker = new ProfilerMarker("TubeRenderer.RebuildMesh");
        
        #endregion
        
        #region Init

        private void InitMeshComponents()
        {
            if (m_meshGO == null)
            {
                m_meshGO = new GameObject($"[TubeMesh]_{gameObject.name}");
                m_meshGO.transform.SetParent(transform, false);
                m_meshFilter = m_meshGO.AddComponent<MeshFilter>();
                m_meshRenderer = m_meshGO.AddComponent<MeshRenderer>();
            }
            
            m_meshGO.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;
    
            if (m_mesh == null)
            {
                m_mesh = new Mesh()
                {
                    name = $"Tube_{gameObject.name}",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }
        
        private void OnDestroy()
        {
#if UNITY_EDITOR
            EditorApplication.update -= UpdateTubeRenderer;       
#endif            
            if (m_mesh) DestroyImmediate(m_mesh);
            if (m_meshGO) DestroyImmediate(m_meshGO);

            m_meshFilter = null; m_meshRenderer = null; m_mesh = null;
        }

        #endregion

        #region Public API

        public LifetimeMode LifetimeType => m_lifetimeMode;
        public int SegmentCount => m_isInitialized ? m_segments.Count : 0;
        public bool AnySegmentsAlive => SegmentCount > 0;

        public TubeSegment GetSegment(int index) => m_segments[index];

        /// <summary>
        /// Add a new segment to the trail with the given position and rotation
        /// </summary>
        public void AddPoint(Vector3 point)
        {
            if (!m_isInitialized) AllocateArrays();
            
            // Reject points too close to the last one to avoid degenerate tangents
            if (SegmentCount > 0)
            {
                float dist = math.distance(point, m_segments[SegmentCount - 1].Position);
                if (dist < 0.001f) return;
            }
            
            m_segments.PushFront(new TubeSegment(point, 0f)); // distance will be set before mesh generation loop as it depends on adjacent points that we'll iterate anyways

            if (SegmentCount >= 2)
            {
                m_isDirty = true;
                enabled = true;
            }
        }

        /// <summary>
        /// Pops the oldest segment from the trail
        /// </summary>
        public void RemovePoint()
        {
            if (!m_isInitialized) AllocateArrays();
            
            m_segments.PopBack();
            m_isDirty = true;
        }

        /// <summary>
        /// Change the position of a point at a given index
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"></exception>
        public void SetPoint(int index, Vector3 point)
        {
            if (!m_isInitialized) AllocateArrays();
            
            if (index >= SegmentCount) throw  new IndexOutOfRangeException();
            var segment = m_segments[index];
            segment.Position = point;
            m_segments[index] = segment;

            //if (m_lifetimeMode == LifetimeMode.Persistent) maybe not necessary?
            if (SegmentCount >= 2)
            {
                m_isDirty = true;
                enabled = true;
            }
        }

        /// <summary>
        /// Bulk set points for the tube-renderer
        /// </summary>
        /// <param name="points"></param>
        public void SetPoints(params Vector3[] points)
        {
            if (!m_isInitialized) AllocateArrays();
    
            m_segments.Clear();
    
            foreach (var point in points) m_segments.PushFront(new TubeSegment(point, 0f));

            if (SegmentCount >= 2)
            {
                m_isDirty = true;
                enabled = true;
            }
            else
            {
                TubeMesh.Clear();
            }
        }

        /// <summary>
        /// Immediately clears any remaining segments and refreshes the mesh
        /// </summary>
        public void ClearSegments()
        {
            m_segments.Clear();
            TubeMesh.Clear();
        }

        /// <summary>
        /// Total length of the tube so far
        /// </summary>
        public float TotalLength => ComputeTotalLength();

        /// <summary>
        /// Is the data valid to rebuild the mesh (enough segments and mesh has been created)
        /// </summary>
        public bool IsValid => m_segments.NativeArray.IsCreated && SegmentCount >= 2 && m_mesh != null &&
                               (m_angleLimits.y - m_angleLimits.x) >= 1 && TubeMeshFilter != null;

        #endregion

        #region Update Loop

        private void LateUpdate()
        {
            if (Application.isPlaying) UpdateTubeRenderer();
        }

        private void UpdateTubeRenderer()
        {
            if (this == null) return; // Maybe destroyed but not GC'ed yet when subscribed to editor update loop
            if (!m_isInitialized) return; // Incase
            
            // Time/Distance modes: actively manage lifetime
            RemoveDeadSegments();
    
            if (SegmentCount <= 1)
            {
                TubeMesh.Clear();
                enabled = false;
                return;
            }
            
            if (m_lifetimeMode == LifetimeMode.Persistent)
            {
                // Set dirty flag based on if transform has changed too
                if (!transform.position.Equals(m_prevPos)) m_isDirty = true;

                bool shouldRebuild = m_isDirty || ActiveCurveMode == CurveParameterization.Age;
                
                if (shouldRebuild)
                {
                    RebuildMesh();
                    m_isDirty = false;
                }
                else
                {
                    // dont disable, if we wanna disable it (or move to bulk updates), we gotta restructure OnDisable to not clear arrays for this specific case
                    //enabled = false; // No point in wasting updates
                }
            }
            else RebuildMesh();
            
            m_prevPos = transform.position;
        }
        
        private void RemoveDeadSegments()
        {
            if (m_lifetimeMode == LifetimeMode.Time)
            {
                float currentTime = TIME;
                // Segments are ordered oldest (index 0) to newest, so pop from back (oldest)
                while (m_segments.Count > 0 && 
                       (currentTime - m_segments[0].EmitTime) >= m_lifetime)
                {
                    RemovePoint();
                }
            }
            else if (m_lifetimeMode == LifetimeMode.Distance)
            {
                float totalLength = ComputeTotalLength();
                while (m_segments.Count > 0 && totalLength > m_lifetimeDistance)
                {
                    float removedLength = m_segments.Count <= 1 ? totalLength : math.distance(
                        m_segments[0].Position, m_segments[1].Position);
                    RemovePoint();
                    totalLength -= removedLength;
                }
            }
        }

        #endregion
        
        #region Runtime Data & Initialization

        [StructLayout(LayoutKind.Sequential)]
        public struct TubeSegment
        {
            public float3 Position;
            public float EmitTime;
            public float CumulativeDistance; // Distance from first ring to this ring
            //public float Lifetime;
            //public float NormalizedAge => (Time.time - EmitTime) / Lifetime;

            public TubeSegment(Vector3 pos, float distance)
            {
                Position = pos;
                EmitTime = TIME;
                CumulativeDistance = distance;
            }

            public void SetDistance(float dist)
            {
                CumulativeDistance = dist; 
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeRingBuffer<T> where T : struct
        {
            private NativeArray<T> m_internalArray;

            public NativeArray<T> NativeArray => m_internalArray;
            public int Capacity { get; private set; }
            public int Count { get; private set; }

            /// <summary>
            /// Index to front of the buffer
            /// </summary>
            public int Head { get; private set; }
            
            /// <summary>
            /// Index to back of the buffer
            /// </summary>
            public int Tail { get; private set; }
            
            public NativeRingBuffer(int capacity)
            {
                m_internalArray = new NativeArray<T>(capacity, Allocator.Persistent);
                Capacity = capacity; // if capacity < 0, implies no limit, so how do we handle that w/ allocation? Just a huge array of 512 or 1024 elements?
                Count = 0;
                
                Head = 0;
                Tail = 0;
            }
            
            public void Dispose() => m_internalArray.Dispose();

            public void Clear()
            {
                Head = 0;
                Tail = 0;
                Count = 0;
            }
            
            public T this[int index]
            {
                get => m_internalArray[(index + Tail) % Capacity];
                set => m_internalArray[(index + Tail) % Capacity] = value;
            }
            
            public void PushFront(T elem)
            {
                if (Count >= Capacity) PopBack();
                m_internalArray[Head] = elem;
                // When we push, head increments forward
                Head = (Head + 1) % Capacity;
                Count++;
            }

            public void PopBack()
            {
                // When we pop, tail increments forward
                Tail = (Tail + 1) % Capacity;
                Count--;
            }
        }
        
        private NativeRingBuffer<TubeSegment> m_segments;
        private NativeArray<float3> m_radialSegmentCache;
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            InitMeshComponents();
            
            // Check if any size-affecting setting changed
            int expectedMaxVerts = MaxSegmentCount * (m_radialSegments + 1);
            if (!m_isInitialized || m_outputPositions.Length != expectedMaxVerts || 
                m_radialSegmentCache.Length != m_radialSegments)
            {
                AllocateArrays();
                
                m_isDirty = true;
                enabled = true;
            }
            
            // Always rebuild radial segments in case angle limits changed
            RebuildRadialSegmentCache();
        }
#endif

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorApplication.update += UpdateTubeRenderer;
#endif
            
            // Need to allocate m_radialSegmentCache
            if (!m_isInitialized) AllocateArrays(); // In case Add already called this before OnEnable (enabled = true)

            // Ensure meshes are good
            InitMeshComponents();
            
            RebuildRadialSegmentCache();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= UpdateTubeRenderer;
#endif
            
            DisposeArrays();
            m_isInitialized = false;
        }

        private void RebuildRadialSegmentCache()
        {
            // Pre-cache radial segment in local space once, easy optimization
            for (int r = 0; r < m_radialSegments; r++)
            {
                float normalizedAngle = (r * 1f / m_radialSegments);
                float theta = (Mathf.Lerp(m_angleLimits.x, m_angleLimits.y, normalizedAngle) + m_angleOffset) * Mathf.Deg2Rad;//normalizedAngle * 2 * Mathf.PI;
                float radialMod = m_radiusOverCrossSection.Evaluate(normalizedAngle);
                Vector3 radialPos = new float3(Mathf.Cos(theta) * radialMod, Mathf.Sin(theta) * radialMod, normalizedAngle); // Z is for UV so dont multiply it
                m_radialSegmentCache[r] = radialPos;
            }
        }

        #endregion

        #region ParallelTransport & Curve/Gradient Baking

        private NativeArray<float> m_bakedRadii;
        private NativeArray<Color32> m_bakedColors;
        
        private NativeArray<float> m_segmentRadii;
        private NativeArray<Color32> m_segmentColor;
        
        private NativeArray<quaternion> m_orientationFrames;
        private NativeArray<float> m_UVv;

        private const int ARRAY_BAKE_SIZE = 256;
        
        private void BakeOrientationsAndSegmentModulations()
        {
            using var _ = s_dataBakingMarker.Auto();
            
            float currentTime = TIME; // cache outside the loop
            float cumulativeDistance = 0f;
            float totalLength = ComputeTotalLength(); // TODO: Perhaps we can even cache this on every AddPoint?
            
            for (int i = 0; i < SegmentCount; i++)
            {
                if (i > 0)
                    cumulativeDistance += math.distance(m_segments[i].Position, m_segments[i - 1].Position);
                
                // Cache segment modulations
                float t = ActiveCurveMode switch
                {
                    CurveParameterization.Position => SegmentCount > 1 
                        ? (float)i / (SegmentCount - 1f) : 0f,
                    CurveParameterization.Age => math.saturate(
                        (currentTime - m_segments[i].EmitTime) / ParametrizedLifetime),
                    CurveParameterization.Distance => totalLength > 0 
                        ? cumulativeDistance / totalLength : 0f,
                    _ => 0f
                };
                
                // Sample from LUT
                float index = t * (ARRAY_BAKE_SIZE - 1);
                int lower = Mathf.FloorToInt(index);
                int upper = Mathf.Min(Mathf.CeilToInt(index + 1), ARRAY_BAKE_SIZE-1);
                float frac = index - lower;
                m_segmentRadii[i] = Mathf.Lerp(m_bakedRadii[lower], m_bakedRadii[upper], frac);
                m_segmentColor[i] = Color32.Lerp(m_bakedColors[lower], m_bakedColors[upper], frac);
                
                // Compute V component of UV ahead of time since its per-segment not per-vertex
                if (m_uvMode == UVMode.Stretch) m_UVv[i] = (SegmentCount > 1 ? (float)i / (SegmentCount - 1f) : 0f); // Always [0-1] along the whole range
                else m_UVv[i] = cumulativeDistance / m_uvTilingLength;

                // Update segment distance
                var seg = m_segments[i];
                seg.CumulativeDistance = cumulativeDistance;
                m_segments[i] = seg;
                
                ComputeParallelTransport(i);
            }
        }

        private void BakeRadiusAndColorCurves()
        {
            for (int i = 0; i < ARRAY_BAKE_SIZE; i++)
            {
                float t = (float)i / (ARRAY_BAKE_SIZE-1);
                m_bakedRadii[i] = m_radius * m_radiusOverTrail.Evaluate(t);
                m_bakedColors[i] = m_vertexColorOverTrail.Evaluate(t);
            }
        }

        /// <summary>
        /// Computes rotation of a given segment via parallel transport
        /// These are rotation minimizing frames (RMFs) computed via parallel transport
        /// </summary>
        private void ComputeParallelTransport(int idx)
        {
            //using var _ = s_parallelTransportMarker.Auto();
            
            // Compute parallel transport
            // Minimal rotation between adjacent segments to cause no twist
            if (idx == 0)
            {
                // Initial case, just a look rotation along the tangent
                float3 tangent = math.normalizesafe(m_segments[1].Position - m_segments[0].Position);
                float3 up = Mathf.Abs(Vector3.Dot(tangent, new float3(0,1,0))) > 0.99f 
                    ? new float3(1,0,0) 
                    : new float3(0,1,0);
                m_orientationFrames[0] = quaternion.LookRotation(tangent, up);
            }
            else if (idx < SegmentCount - 1)
            {
                // Minimal rotation from prev segment
                Vector3 prevTangent = math.normalizesafe(m_segments[idx].Position - m_segments[idx - 1].Position);
                Vector3 tangent = math.normalizesafe(m_segments[idx+1].Position -  m_segments[idx].Position);
                m_orientationFrames[idx] = Quaternion.FromToRotation(prevTangent, tangent) *  m_orientationFrames[idx-1];
            }
            else
            {
                // Just pick up previous one
                m_orientationFrames[idx] = m_orientationFrames[idx - 1];
            }
        }
        
        private float ComputeTotalLength()
        {
            float total = 0f;
            for (int i = 1; i < SegmentCount; i++)
                total += math.distance(m_segments[i].Position, m_segments[i - 1].Position);
            return total;
        }
        
        #endregion

        #region Mesh Generation Job
        
        [BurstCompile]
        private struct MeshGenerationJob : IJobParallelFor
        {
            // Was gonna go with NativeArray<Vertex>, but then realized ill have to convert them all into individual lists
            // after when setting mesh data - so opting to just do individual arrays for each attribute
            [StructLayout(LayoutKind.Sequential)]
            public struct Vertex
            {
                public float3 Position;
                public float3 Normal;
                public Color32 Color;
                public float2 UV;
            }

            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<int> IndexBuffer;
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float3> Positions;
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float3> Normals;
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<Color32> Colors;
            [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<float2> UVs;

            [ReadOnly] public float4x4 WorldToLocal;
            
            [ReadOnly] public int SegmentCount;
            [ReadOnly] public int RadialSegments;
            [ReadOnly] public bool AddRadialSeam;
            
            [ReadOnly] public NativeArray<TubeSegment> Segments;
            [ReadOnly] public int RingBufferTail;
            [ReadOnly] public int RingBufferCapacity;
            
            [ReadOnly] public NativeArray<quaternion> RotationFrames;
            [ReadOnly] public NativeArray<float> RadiusOverTrail;
            [ReadOnly] public NativeArray<Color32> VertexColorOverTrail;
            [ReadOnly] public NativeArray<float> UV_v;

            [ReadOnly] public NativeArray<float3> RadialPositionCache;

            public void Execute(int index)
            {
                float radius = RadiusOverTrail[index];
                Color32 color = VertexColorOverTrail[index];
                TubeSegment segment = SampleTubeSegment(index);
    
                int vertsPerRing = RadialSegments + (AddRadialSeam ? 1 : 0);
                quaternion worldToLocalRotation = math.quaternion(WorldToLocal);
                quaternion localFrame = math.mul(worldToLocalRotation, RotationFrames[index]);
                float3 localSegPosition = math.transform(WorldToLocal, segment.Position);
                AffineTransform localToWorld = math.AffineTransform(
                    localSegPosition, localFrame, radius);

                for (int r = 0; r < RadialSegments; r++)
                {
                    float3 radialOffset = new float3(RadialPositionCache[r].xy, 0);
                    int vertexIdx = index * vertsPerRing + r;
        
                    Positions[vertexIdx] = math.transform(localToWorld, radialOffset);
                    Normals[vertexIdx] = math.normalizesafe(
                        math.rotate(localFrame, radialOffset));
                    Colors[vertexIdx] = color;
                    UVs[vertexIdx] = new float2(RadialPositionCache[r].z, UV_v[index]);
                    
                    // Index buffer. Skip last segment, it gets added by the segment before it
                    if (index < SegmentCount - 1 && r < RadialSegments - (AddRadialSeam ? 0 : 1))
                    {
                        int quadsPerBand = AddRadialSeam ? RadialSegments : (RadialSegments - 1);
                        int bandIndex = index * quadsPerBand * 6; // Where band i's index starts
                        int quadBase = bandIndex + r * 6;

                        int A = index * vertsPerRing + r;               // current ring, current radial
                        int B = index * vertsPerRing + (r + 1);         // current ring, next radial, when r = R-1, this is the seam vertex (same goes for D)
                        int C = (index + 1) * vertsPerRing + r;         // next ring, current radial  
                        int D = (index + 1) * vertsPerRing + (r + 1);   // next ring, next radial
                        
                        IndexBuffer[quadBase + 0] = A;
                        IndexBuffer[quadBase + 1] = B;
                        IndexBuffer[quadBase + 2] = C;
                        IndexBuffer[quadBase + 3] = B;
                        IndexBuffer[quadBase + 4] = D;
                        IndexBuffer[quadBase + 5] = C;
                    }
                }
    
                // Seam vertex: duplicate of r=0 with U=1
                if (AddRadialSeam)
                {
                    int seamIdx = index * vertsPerRing + RadialSegments;
                    float3 firstOffset = new float3(RadialPositionCache[0].xy, 0);

                    Positions[seamIdx] = math.transform(localToWorld, firstOffset);
                    Normals[seamIdx] = math.normalizesafe(
                        math.rotate(localFrame, firstOffset));
                    Colors[seamIdx] = color;
                    UVs[seamIdx] = new float2(1f, UV_v[index]);
                }
            }

            private TubeSegment SampleTubeSegment(int segIdx) =>
                Segments[(segIdx + RingBufferTail) % RingBufferCapacity];
        }

        private NativeArray<float3> m_outputPositions;
        private NativeArray<float3> m_outputNormals;
        private NativeArray<Color32> m_outputColors;
        private NativeArray<float2> m_outputUVs;
        private NativeArray<int> m_outputIndices;

        private void AllocateArrays()
        {
            // Ensure theyre disposed of first if any arent (safety)
            DisposeArrays();
            
            // TODO: Also need to reallocate during OnValidate (for edit mode previewing if we do ExecuteInEditMode which we'd want)
            m_segments = new NativeRingBuffer<TubeSegment>(MaxSegmentCount);
            m_radialSegmentCache = new NativeArray<float3>(m_radialSegments, Allocator.Persistent);
            
            // Bake this to a fixed size-array then interpolate later
            m_bakedRadii = new NativeArray<float>(ARRAY_BAKE_SIZE, Allocator.Persistent);
            m_bakedColors = new NativeArray<Color32>(ARRAY_BAKE_SIZE, Allocator.Persistent);
            BakeRadiusAndColorCurves();

            m_segmentRadii = new NativeArray<float>(MaxSegmentCount, Allocator.Persistent);
            m_segmentColor =  new NativeArray<Color32>(MaxSegmentCount, Allocator.Persistent);
            
            m_orientationFrames = new NativeArray<quaternion>(MaxSegmentCount, Allocator.Persistent);
            m_UVv  = new NativeArray<float>(MaxSegmentCount, Allocator.Persistent);
            
            int maxVerts = MaxSegmentCount * (m_radialSegments + 1);
            int maxIndices = (MaxSegmentCount - 1) * m_radialSegments * 6;

            m_outputPositions = new NativeArray<float3>(maxVerts, Allocator.Persistent);
            m_outputNormals = new NativeArray<float3>(maxVerts, Allocator.Persistent);
            m_outputColors = new NativeArray<Color32>(maxVerts, Allocator.Persistent);
            m_outputUVs = new NativeArray<float2>(maxVerts, Allocator.Persistent);
            m_outputIndices = new NativeArray<int>(maxIndices, Allocator.Persistent);

            m_isInitialized = true;
        }

        private void DisposeArrays()
        {
            if (m_segments.NativeArray.IsCreated) m_segments.Dispose();
            if (m_radialSegmentCache.IsCreated) m_radialSegmentCache.Dispose();

            if (m_bakedRadii.IsCreated) m_bakedRadii.Dispose();
            if (m_bakedColors.IsCreated) m_bakedColors.Dispose();
            
            if (m_segmentRadii.IsCreated) m_segmentRadii.Dispose();
            if (m_segmentColor.IsCreated) m_segmentColor.Dispose();
            
            if (m_orientationFrames.IsCreated) m_orientationFrames.Dispose();
            if (m_UVv.IsCreated) m_UVv.Dispose();
            
            if (m_outputPositions.IsCreated) m_outputPositions.Dispose();
            if (m_outputNormals.IsCreated) m_outputNormals.Dispose();
            if (m_outputColors.IsCreated) m_outputColors.Dispose();
            if (m_outputUVs.IsCreated) m_outputUVs.Dispose();
            if (m_outputIndices.IsCreated) m_outputIndices.Dispose();

            m_isInitialized = false;
        }
        
        public void RebuildMesh()
        {
            using var _ = s_meshRebuildMarker.Auto();
            
            if (!IsValid) return;
            
            BakeOrientationsAndSegmentModulations();
            
            var meshJob = new MeshGenerationJob()
            {
                // Out results
                IndexBuffer = m_outputIndices,
                Positions = m_outputPositions,
                Normals = m_outputNormals,
                Colors = m_outputColors,
                UVs = m_outputUVs,
                
                // Transform
                WorldToLocal = transform.worldToLocalMatrix,
                
                // Length info
                SegmentCount = SegmentCount,
                RadialSegments = m_radialSegments,
                AddRadialSeam = AddRadialSeam,

                // Segment Data
                Segments = m_segments.NativeArray,
                RingBufferCapacity = m_segments.Capacity,
                RingBufferTail = m_segments.Tail,
                
                RotationFrames = m_orientationFrames,
                RadiusOverTrail = m_segmentRadii,
                VertexColorOverTrail = m_segmentColor,
                RadialPositionCache = m_radialSegmentCache,
                UV_v = m_UVv,
            };
            
            meshJob.Run(SegmentCount);
            
            int vertsPerRing = m_radialSegments + (AddRadialSeam ? 1 : 0);
            int vertexCount = SegmentCount * vertsPerRing;
            int quadsPerBand = AddRadialSeam ? m_radialSegments : (m_radialSegments - 1);
            int indexCount = (SegmentCount - 1) * quadsPerBand * 6;
            
            TubeMesh.Clear();
            TubeMesh.SetVertices(m_outputPositions, 0, vertexCount);
            TubeMesh.SetNormals(m_outputNormals, 0, vertexCount); // TODO: Normals are incorrect if radiusOverCrossSection results in a concave shape
            TubeMesh.SetColors(m_outputColors, 0, vertexCount);
            TubeMesh.SetUVs(0, m_outputUVs, 0, vertexCount);
            TubeMesh.SetIndices(m_outputIndices, 0, indexCount, MeshTopology.Triangles, 0);
            TubeMesh.RecalculateBounds();
            
            TubeMeshFilter.sharedMesh = TubeMesh;
            TubeMeshRenderer.sharedMaterial = m_material;
        }

        #endregion
    }
}