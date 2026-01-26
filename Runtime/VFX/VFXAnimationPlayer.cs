using System;
using System.Linq;
using Animancer;
using UnityEngine;

namespace FS.Rendering
{
    public class VFXAnimationPlayer : SoloAnimation
    {
        private float m_loopStartTime = -1f;

        private VFXController m_vfxController;
        
        public const string LOOP_START_MARKER = "OnLoopStart";
        public const string LOOP_END_MARKER = "OnLoopEnd";
        
        private void Awake()
        {
            m_vfxController = GetComponent<VFXController>();
            if (!m_vfxController) m_vfxController = GetComponentInParent<VFXController>();
        }
        
        public AnimationEvent LoopStartMarker => Clip == null ? null : Clip.events.FirstOrDefault(e => e.functionName == LOOP_START_MARKER);
        public AnimationEvent LoopEndMarker => Clip == null ? null : Clip.events.FirstOrDefault(e => e.functionName == LOOP_END_MARKER);

        public bool HasLoopMarkers
        {
            get
            {
                var start = LoopStartMarker;
                var end = LoopEndMarker;
                return start != null && end != null && end.time > start.time; // Ensure valid loop markers
            }
        }
        
        public bool IsAnimationLooping  => Clip != null && (Clip.isLooping || HasLoopMarkers);

        public float LoopStartTime
        {
            get
            {
                if (Clip == null) return -1f;
                var loopStartMarker = LoopStartMarker;
                if (loopStartMarker != null)
                    return loopStartMarker.time;
                return 0f;
            }
        }
        
        public float LoopEndTime
        {
            get
            {
                if (Clip == null) return -1f;
                var loopEndMarker = LoopEndMarker;
                if (loopEndMarker != null)
                    return loopEndMarker.time;
                return Clip.length;
            }
        }

        public void OnLoopStart()
        {
            m_loopStartTime = Time;
        }

        public void OnLoopEnd()
        {
            if (m_vfxController.IsStopping) return;
            
            if (m_loopStartTime >= 0f)
                Time = m_loopStartTime;
        }
    }
}