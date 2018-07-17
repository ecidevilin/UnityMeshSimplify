using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UltimateGameTools
{
    namespace MeshSimplifier
    {

        /// <summary>
        /// Vertex that has the same position in space as another one, but different vertex data (UV, color...).
        /// </summary>
        public class RepeatedVertex
        {
            // Public properties

            /// <summary>
            /// Face it belongs to. This will be the same index in the source mesh as in the internal created face list.
            /// </summary>
            public int FaceIndex
            {
                get
                {
                    return _nFaceIndex;
                }
            }

            /// <summary>
            /// Position in the original vertex array.
            /// </summary>
            public int OriginalVertexIndex
            {
                get
                {
                    return _nOriginalVertexIndex;
                }
            }

            // Constructor

            public RepeatedVertex(int nFaceIndex, int nOriginalVertexIndex)
            {
                _nFaceIndex = nFaceIndex;
                _nOriginalVertexIndex = nOriginalVertexIndex;
            }

            // Private vars

            private int _nFaceIndex;
            private int _nOriginalVertexIndex;
        }
    }
}
