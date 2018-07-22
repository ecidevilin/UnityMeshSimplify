using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UltimateGameTools
{
    namespace MeshSimplifier
    {
        /// <summary>
        /// A list of triangles. We encapsulate this as a class to be able to serialize a List of RunTriangleLists if we need to.
        /// Unity doesn't serialize a list of lists or an array of lists.
        /// </summary>
		public class RuntimeTriangleList
        {
			public RuntimeTriangleList(int capacity)
            {
				m_listTriangles = new List<RuntimeTriangle>(capacity);
            }

			public List<RuntimeTriangle> m_listTriangles;
        }
    }
}
