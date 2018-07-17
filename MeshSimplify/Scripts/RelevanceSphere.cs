using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace UltimateGameTools
{
    namespace MeshSimplifier
    {
        [Serializable]
        public class RelevanceSphere
        {
            public RelevanceSphere()
            {
                m_v3Scale = Vector3.one;
            }

            public void SetDefault(Transform target, float fRelevance)
            {
                m_bExpanded = true;
                m_v3Position = target.position + Vector3.up;
                m_q4Rotation = target.rotation;
                m_v3Scale = Vector3.one;
                m_fRelevance = fRelevance;
            }

            public bool m_bExpanded;
            public Vector3 m_v3Position;
            public Quaternion m_q4Rotation;
            public Vector3 m_v3Scale;
            public float m_fRelevance;
        }
    }
}
