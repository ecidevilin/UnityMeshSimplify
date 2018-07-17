using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UltimateGameTools
{
    namespace MeshSimplifier
    {


        /// <summary>
        /// List of vertices that have the same position in space but different vertex data (UV, color...).
        /// </summary>
        public class RepeatedVertexList
        {
            // Public properties

            /// <summary>
            /// Unique vertex index in our array for this list.
            /// </summary>
            public int UniqueIndex
            {
                get
                {
                    return m_nUniqueIndex;
                }
            }

            // Public methods

            public RepeatedVertexList(int nUniqueIndex, RepeatedVertex repeatedVertex)
            {
                m_nUniqueIndex = nUniqueIndex;
                m_listRepeatedVertices = new List<RepeatedVertex>();
                m_listRepeatedVertices.Add(repeatedVertex);
            }

            public void Add(RepeatedVertex repeatedVertex)
            {
                m_listRepeatedVertices.Add(repeatedVertex);
            }

            // Private vars

            private int m_nUniqueIndex;
            private List<RepeatedVertex> m_listRepeatedVertices;
        }
    }
}
