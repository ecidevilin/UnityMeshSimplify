using System.Collections;
using System.Collections.Generic;
using Chaos;
using UnityEngine;

namespace UltimateGameTools
{
    namespace MeshSimplifier
    {

        /// <summary>
        /// Vertex that is has a unique position in space.
        /// </summary>
        public class UniqueVertex
        {
            // Overrides from Object

            public override bool Equals(object obj)
            {
                UniqueVertex uniqueVertex = obj as UniqueVertex;
                return (uniqueVertex.m_nFixedX == m_nFixedX) && (uniqueVertex.m_nFixedY == m_nFixedY) && (uniqueVertex.m_nFixedZ == m_nFixedZ);
            }

            public override int GetHashCode()
            {
                return (int)(m_nFixedX + (m_nFixedY << 2) + (m_nFixedZ << 4));
            }

            // Constructor

            public UniqueVertex(Vector3 v3Vertex)
            {
                FromVertex(v3Vertex);
            }

            // Public methods

            public Vector3 ToVertex()
            {
                return new Vector3(FixedToCoord(m_nFixedX), FixedToCoord(m_nFixedY), FixedToCoord(m_nFixedZ));
            }

            // Comparison operators

            public static bool operator ==(UniqueVertex a, UniqueVertex b)
            {
                return a.Equals(b);
            }

            public static bool operator !=(UniqueVertex a, UniqueVertex b)
            {
                return !a.Equals(b);
            }

            // Private methods/vars

            private void FromVertex(Vector3 vertex)
            {
                m_nFixedX = CoordToFixed(vertex.x);
                m_nFixedY = CoordToFixed(vertex.y);
                m_nFixedZ = CoordToFixed(vertex.z);
            }

            private uint CoordToFixed(float fCoord)
            {
                //int nInteger   = Mathf.FloorToInt(fCoord);
                //int nRemainder = Mathf.FloorToInt((fCoord - nInteger) * fDecimalMultiplier);

                //return nInteger << 16 | nRemainder;
                return UnsafeUtil.FloatToUint(fCoord);
            }

            private float FixedToCoord(uint nFixed)
            {
                //float fRemainder = (nFixed & 0xFFFF) / fDecimalMultiplier;
                //float fInteger = nFixed >> 16;

                //return fInteger + fRemainder;
                return UnsafeUtil.UintToFloat(nFixed);
            }

            // Private vars

            private uint m_nFixedX, m_nFixedY, m_nFixedZ;
        }
    }
}
