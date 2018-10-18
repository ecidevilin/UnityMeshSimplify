using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Chaos
{
    [Serializable]
    public class RelevanceSphere
    {
        public RelevanceSphere()
        {
            Scale = Vector3.one;
        }

        public void SetDefault(Transform target, float fRelevance)
        {
            Expanded = true;
            Position = target.position + Vector3.up;
            Rotation = target.rotation;
            Scale = Vector3.one;
            Relevance = fRelevance;
        }

        public bool Expanded;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public float Relevance;
    }
}
