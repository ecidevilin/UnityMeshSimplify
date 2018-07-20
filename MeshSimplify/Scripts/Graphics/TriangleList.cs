using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UltimateGameTools
{
    namespace MeshSimplifier
    {
        /// <summary>
        /// A list of triangles. We encapsulate this as a class to be able to serialize a List of TriangleLists if we need to.
        /// Unity doesn't serialize a list of lists or an array of lists.
        /// </summary>
        public class TriangleList
        {
            public TriangleList()
            {
                m_listTriangles = new List<Triangle>();
            }

            public List<Triangle> m_listTriangles;

            public void RemoveNull()
            {
                int l = m_listTriangles.Count;
                int h = 0;
                int t = l - 1;
                while (h < t)
                {
                    if (m_listTriangles[t] == null)
                    {
                        t--;
                        continue;
                    }
                    if (m_listTriangles[h] != null)
                    {
                        h++;
                        continue;
                    }
                    m_listTriangles[h] = m_listTriangles[t];
                    m_listTriangles[t] = null;
                    h++;
                    t--;
                }
                if (t < l - 1)
                {
                    if (m_listTriangles[t] == null)
                    {
                        m_listTriangles.RemoveRange(t, l - t);
                    }
                    else
                    {
                        m_listTriangles.RemoveRange(t + 1, l - 1 - t);
                    }
                }
            }
        }
    }
}
