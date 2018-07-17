using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UltimateGameTools
{
    namespace MeshSimplifier
    {
        /// <summary>
        /// Our serializable version of Unity's BoneWeight
        /// </summary>
        [Serializable]
        public class SerializableBoneWeight
        {
            public SerializableBoneWeight(BoneWeight boneWeight)
            {
                _boneIndex0 = boneWeight.boneIndex0;
                _boneIndex1 = boneWeight.boneIndex1;
                _boneIndex2 = boneWeight.boneIndex2;
                _boneIndex3 = boneWeight.boneIndex3;

                _boneWeight0 = boneWeight.weight0;
                _boneWeight1 = boneWeight.weight1;
                _boneWeight2 = boneWeight.weight2;
                _boneWeight3 = boneWeight.weight3;
            }

            public BoneWeight ToBoneWeight()
            {
                return new BoneWeight() { boneIndex0 = _boneIndex0, boneIndex1 = _boneIndex1, boneIndex2 = _boneIndex2, boneIndex3 = _boneIndex3, weight0 = _boneWeight0, weight1 = _boneWeight1, weight2 = _boneWeight2, weight3 = _boneWeight3 };
            }

            public int _boneIndex0;
            public int _boneIndex1;
            public int _boneIndex2;
            public int _boneIndex3;

            public float _boneWeight0;
            public float _boneWeight1;
            public float _boneWeight2;
            public float _boneWeight3;
        }
    }
}