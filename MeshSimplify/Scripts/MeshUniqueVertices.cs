using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
      #region Public types

      /////////////////////////////////////////////////////////////////////////////////////////////////
      // Public types
      /////////////////////////////////////////////////////////////////////////////////////////////////

      /// <summary>
      /// A list of vertex indices. We use this in order to be able to serialize a list of lists.
      /// </summary>
      [Serializable]
      public class ListIndices
      {
        public ListIndices()
        {
          m_listIndices = new List<int>();
        }

        public List<int> m_listIndices;
      }

      /// <summary>
      /// Our serializable version of Unity's BoneWeight
      /// </summary>
      [Serializable]
      public class SerializableBoneWeight
      {
        public SerializableBoneWeight(BoneWeight boneWeight)
        {
          _boneIndex0  = boneWeight.boneIndex0;
          _boneIndex1  = boneWeight.boneIndex1;
          _boneIndex2  = boneWeight.boneIndex2;
          _boneIndex3  = boneWeight.boneIndex3;

          _boneWeight0 = boneWeight.weight0;
          _boneWeight1 = boneWeight.weight1;
          _boneWeight2 = boneWeight.weight2;
          _boneWeight3 = boneWeight.weight3;
        }

        public BoneWeight ToBoneWeight()
        {
          BoneWeight boneWeight = new BoneWeight();

          boneWeight.boneIndex0 = _boneIndex0;
          boneWeight.boneIndex1 = _boneIndex1;
          boneWeight.boneIndex2 = _boneIndex2;
          boneWeight.boneIndex3 = _boneIndex3;

          boneWeight.weight0    = _boneWeight0;
          boneWeight.weight1    = _boneWeight1;
          boneWeight.weight2    = _boneWeight2;
          boneWeight.weight3    = _boneWeight3;

          return boneWeight;
        }

        public int   _boneIndex0;
        public int   _boneIndex1;
        public int   _boneIndex2;
        public int   _boneIndex3;

        public float _boneWeight0;
        public float _boneWeight1;
        public float _boneWeight2;
        public float _boneWeight3;
      }

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
          return m_nFixedX + (m_nFixedY << 2) + (m_nFixedZ << 4);
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

        private int CoordToFixed(float fCoord)
        {
          int nInteger   = Mathf.FloorToInt(fCoord);
          int nRemainder = Mathf.FloorToInt((fCoord - nInteger) * fDecimalMultiplier);

          return nInteger << 16 | nRemainder;
        }

        private float FixedToCoord(int nFixed)
        {
          float fRemainder = (nFixed & 0xFFFF) / fDecimalMultiplier;
          float fInteger = nFixed >> 16;

          return fInteger + fRemainder;
        }

        // Private vars

        private int m_nFixedX, m_nFixedY, m_nFixedZ;
        private const float fDecimalMultiplier = 100000.0f;
      }

      #endregion
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
      #region Private types

      /////////////////////////////////////////////////////////////////////////////////////////////////
      // Private types
      /////////////////////////////////////////////////////////////////////////////////////////////////

      /// <summary>
      /// Vertex that has the same position in space as another one, but different vertex data (UV, color...).
      /// </summary>
      private class RepeatedVertex
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
          _nFaceIndex           = nFaceIndex;
          _nOriginalVertexIndex = nOriginalVertexIndex;
        }

        // Private vars

        private int _nFaceIndex;
        private int _nOriginalVertexIndex;
      }

      /// <summary>
      /// List of vertices that have the same position in space but different vertex data (UV, color...).
      /// </summary>
      private class RepeatedVertexList
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

      #endregion // Private types
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