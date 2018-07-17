using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Chaos;

namespace UltimateGameTools
{
    namespace MeshSimplifier
    {
        /// <summary>
        /// Class that will take a Mesh as input and will build internal data to identify which vertices are repeated due to
        /// different vertex data (UV, vertex colors etc).
        /// </summary>
        [Serializable]
        public class MeshUniqueVertices
        {
            #region Public properties

            /////////////////////////////////////////////////////////////////////////////////////////////////
            // Public properties
            /////////////////////////////////////////////////////////////////////////////////////////////////

            /// <summary>
            /// For each submesh, a list of faces. ListIndices has 3 indices for each face.
            /// Each index is a vertex in ListVertices.
            /// </summary>
            public ListIndices[] SubmeshesFaceList
            {
                get
                {
                    return m_aFaceList;
                }
            }

            /// <summary>
            /// Our list of vertices. Vertices are unique, so no vertex shares the same position in space.
            /// </summary>
            public List<Vector3> ListVertices
            {
                get
                {
                    return m_listVertices;
                }
            }

            /// <summary>
            /// Our list of vertices in world space.
            /// Vertices are unique, so no vertex shares the same position in space.
            /// </summary>
            public List<Vector3> ListVerticesWorld
            {
                get
                {
                    return m_listVerticesWorld;
                }
            }

            /// <summary>
            /// Our list of vertex bone weights
            /// </summary>
            public List<SerializableBoneWeight> ListBoneWeights
            {
                get
                {
                    return m_listBoneWeights;
                }
            }

            #endregion // Public properties
            #region Public methods

            /////////////////////////////////////////////////////////////////////////////////////////////////
            // Public methods
            /////////////////////////////////////////////////////////////////////////////////////////////////

            /// <summary>
            /// Takes a Mesh as input and will build a new list of faces and vertices. The vertex list will
            /// have no vertices sharing position in 3D space. The input mesh may have them, since often
            /// a vertex will have different mapping coordinates for each of the faces that share it.
            /// </summary>
            /// <param name="sourceMesh"></param>
            /// <param name="av3VerticesWorld"</param>
            public void BuildData(Mesh sourceMesh, Vector3[] av3VerticesWorld)
            {
                Vector3[]    av3Vertices  = sourceMesh.vertices;
                BoneWeight[] aBoneWeights = sourceMesh.boneWeights;

                Dictionary<UniqueVertex, RepeatedVertexList> dicUniqueVertex2RepeatedVertexList = new Dictionary<UniqueVertex, RepeatedVertexList>();

                m_listVertices      = new List<Vector3>();
                m_listVerticesWorld = new List<Vector3>();
                m_listBoneWeights   = new List<SerializableBoneWeight>();
                m_aFaceList         = new ListIndices[sourceMesh.subMeshCount];

                for (int nSubMesh = 0; nSubMesh < sourceMesh.subMeshCount; nSubMesh++)
                {
                    m_aFaceList[nSubMesh] = new ListIndices();
                    int[] anFaces = sourceMesh.GetTriangles(nSubMesh);

                    for (int i = 0; i < anFaces.Length; i++)
                    {
                        UniqueVertex vertex = new UniqueVertex(av3Vertices[anFaces[i]]);

                        if (dicUniqueVertex2RepeatedVertexList.ContainsKey(vertex))
                        {
                            dicUniqueVertex2RepeatedVertexList[vertex].Add(new RepeatedVertex(i / 3, anFaces[i]));
                            m_aFaceList[nSubMesh].m_listIndices.Add(dicUniqueVertex2RepeatedVertexList[vertex].UniqueIndex);
                        }
                        else
                        {
                            int nNewUniqueIndex = m_listVertices.Count;
                            dicUniqueVertex2RepeatedVertexList.Add(vertex, new RepeatedVertexList(nNewUniqueIndex, new RepeatedVertex(i / 3, anFaces[i])));
                            m_listVertices.Add(av3Vertices[anFaces[i]]);
                            m_listVerticesWorld.Add(av3VerticesWorld[anFaces[i]]);
                            m_aFaceList[nSubMesh].m_listIndices.Add(nNewUniqueIndex);

                            if(aBoneWeights != null && aBoneWeights.Length > 0)
                            {
                                m_listBoneWeights.Add(new SerializableBoneWeight(aBoneWeights[anFaces[i]]));
                            }
                        }
                    }
                }

                //Debug.Log("In: " + av3Vertices.Length + " vertices. Out: " + m_listVertices.Count + " vertices.");
            }

            #endregion // Public methods

            #region Private vars

            /////////////////////////////////////////////////////////////////////////////////////////////////
            // Private vars
            /////////////////////////////////////////////////////////////////////////////////////////////////

            [SerializeField]
            private List<Vector3> m_listVertices;
            [SerializeField]
            private List<Vector3> m_listVerticesWorld;
            [SerializeField]
            private List<SerializableBoneWeight> m_listBoneWeights;
            [SerializeField]
            private ListIndices[] m_aFaceList;

            #endregion // Private vars
        }
    }
}