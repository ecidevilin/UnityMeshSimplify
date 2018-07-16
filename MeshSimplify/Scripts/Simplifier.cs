using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

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
        m_bExpanded  = true;
        m_v3Position = target.position + Vector3.up;
        m_v3Rotation = target.rotation.eulerAngles;
        m_v3Scale    = Vector3.one;
        m_fRelevance = fRelevance;
      }

      public bool    m_bExpanded;
      public Vector3 m_v3Position;
      public Vector3 m_v3Rotation;
      public Vector3 m_v3Scale;
      public float   m_fRelevance;
    }

    public class Simplifier : MonoBehaviour
    {
      #region Types

      /////////////////////////////////////////////////////////////////////////////////////////////////
      // Types
      /////////////////////////////////////////////////////////////////////////////////////////////////
      public delegate void ProgressDelegate(string strTitle, string strProgressMessage, float fT);

      public static bool Cancelled
      {
        get;
        set;
      }

      public static int CoroutineFrameMiliseconds
      {
        get
        {
          return m_nCoroutineFrameMiliseconds;
        }
        set
        {
          m_nCoroutineFrameMiliseconds = value;
        }
      }

      #endregion // Types

      #region Properties

      public bool CoroutineEnded
      {
        get;
        set;
      }

      public bool UseEdgeLength
      {
        get
        {
          return m_bUseEdgeLength;
        }
        set
        {
          m_bUseEdgeLength = value;
        }
      }

      public bool UseCurvature
      {
        get
        {
          return m_bUseCurvature;
        }
        set
        {
          m_bUseCurvature = value;
        }
      }

      public bool ProtectTexture
      {
        get
        {
          return m_bProtectTexture;
        }
        set
        {
          m_bProtectTexture = value;
        }
      }

      public bool LockBorder
      {
        get
        {
          return m_bLockBorder;
        }
        set
        {
          m_bLockBorder = value;
        }
      }

      #endregion // Properties

      #region Public methods

      /////////////////////////////////////////////////////////////////////////////////////////////////
      // Public methods
      /////////////////////////////////////////////////////////////////////////////////////////////////

      public IEnumerator ProgressiveMesh(GameObject gameObject, Mesh sourceMesh, RelevanceSphere[] aRelevanceSpheres, string strProgressDisplayObjectName = "", ProgressDelegate progress = null)
      {
        m_meshOriginal = sourceMesh;

        Vector3[] aVerticesWorld = GetWorldVertices(gameObject);

        if (aVerticesWorld == null)
        {
          CoroutineEnded = true;
          yield break;
        }

        m_listVertexMap             = new List<int>();
        m_listVertexPermutationBack = new List<int>();
        m_listVertices              = new List<Vertex>();
        m_aListTriangles            = new TriangleList[m_meshOriginal.subMeshCount];

        if (progress != null)
        {
          progress("Preprocessing mesh: " + strProgressDisplayObjectName, "Building unique vertex data", 1.0f);

          if (Simplifier.Cancelled)
          {
            CoroutineEnded = true;
            yield break;
          }
        }

        m_meshUniqueVertices = new MeshUniqueVertices();
        m_meshUniqueVertices.BuildData(m_meshOriginal, aVerticesWorld);

        m_nOriginalMeshVertexCount = m_meshUniqueVertices.ListVertices.Count;
        m_fOriginalMeshSize        = Mathf.Max(m_meshOriginal.bounds.size.x, m_meshOriginal.bounds.size.y, m_meshOriginal.bounds.size.z);

        m_listHeap = new List<Vertex>(m_meshUniqueVertices.ListVertices.Count);

        for (int i = 0; i < m_meshUniqueVertices.ListVertices.Count; i++)
        {
          m_listVertexMap.Add(-1);
          m_listVertexPermutationBack.Add(-1);
        }

        Vector2[] av2Mapping = m_meshOriginal.uv;

        AddVertices(m_meshUniqueVertices.ListVertices, m_meshUniqueVertices.ListVerticesWorld, m_meshUniqueVertices.ListBoneWeights);

        for (int nSubMesh = 0; nSubMesh < m_meshOriginal.subMeshCount; nSubMesh++)
        {
          int[] anIndices = m_meshOriginal.GetTriangles(nSubMesh);
          m_aListTriangles[nSubMesh] = new TriangleList();
          AddFaceListSubMesh(nSubMesh, m_meshUniqueVertices.SubmeshesFaceList[nSubMesh].m_listIndices, anIndices, av2Mapping);
        }

        if(Application.isEditor && !Application.isPlaying)
        {
          IEnumerator enumerator = ComputeAllEdgeCollapseCosts(strProgressDisplayObjectName, gameObject.transform, aRelevanceSpheres, progress);

          while (enumerator.MoveNext())
          {
            if (Simplifier.Cancelled)
            {
              CoroutineEnded = true;
              yield break;
            }
          }
        }
        else
        {
          yield return StartCoroutine(ComputeAllEdgeCollapseCosts(strProgressDisplayObjectName, gameObject.transform, aRelevanceSpheres, progress));
        }

        int nVertices = m_listVertices.Count;

        Stopwatch sw = Stopwatch.StartNew();

        while (m_listVertices.Count > 0)
        {
          if (progress != null && ((m_listVertices.Count & 0xFF) == 0))
          {
            progress("Preprocessing mesh: " + strProgressDisplayObjectName, "Collapsing edges", 1.0f - ((float)m_listVertices.Count / (float)nVertices));

            if(Cancelled)
            {
              CoroutineEnded = true;
              yield break;
            }
          }

          if (sw.ElapsedMilliseconds > CoroutineFrameMiliseconds && CoroutineFrameMiliseconds > 0)
          {
            yield return null;
            sw = Stopwatch.StartNew();
          }

          Vertex mn = MinimumCostEdge();

          m_listVertexPermutationBack[m_listVertices.Count - 1] = mn.m_nID;
          m_listVertexMap[mn.m_nID] = mn.m_collapse != null ? mn.m_collapse.m_nID : -1;
          Collapse(mn, mn.m_collapse, true, gameObject.transform, aRelevanceSpheres);
        }

        UnityEngine.Profiling.Profiler.EndSample();

        m_listHeap.Clear();

        CoroutineEnded = true;
      }

      public IEnumerator ComputeMeshWithVertexCount(GameObject gameObject, Mesh meshOut, int nVertices, string strProgressDisplayObjectName = "", ProgressDelegate progress = null)
      {
        if (GetOriginalMeshUniqueVertexCount() == -1)
        {
          CoroutineEnded = true;
          yield break;
        }

        if (nVertices < 3)
        {
          CoroutineEnded = true;
          yield break;
        }

        if (nVertices >= GetOriginalMeshUniqueVertexCount())
        {
          // Original vertex count requested

          meshOut.triangles    = new int[0];
          meshOut.subMeshCount = m_meshOriginal.subMeshCount;

          meshOut.vertices     = m_meshOriginal.vertices;
          meshOut.normals      = m_meshOriginal.normals;
          meshOut.tangents     = m_meshOriginal.tangents;
          meshOut.uv           = m_meshOriginal.uv;
          meshOut.uv2          = m_meshOriginal.uv2;
          meshOut.colors32     = m_meshOriginal.colors32;
          meshOut.boneWeights  = m_meshOriginal.boneWeights;
          meshOut.bindposes    = m_meshOriginal.bindposes;

          meshOut.triangles    = m_meshOriginal.triangles;
          meshOut.subMeshCount = m_meshOriginal.subMeshCount;

          for (int nSubMesh = 0; nSubMesh < m_meshOriginal.subMeshCount; nSubMesh++)
          {
            meshOut.SetTriangles(m_meshOriginal.GetTriangles(nSubMesh), nSubMesh);
          }

          meshOut.name = gameObject.name + " simplified mesh";

          CoroutineEnded = true;
          yield break;
        }

        m_listVertices   = new List<Vertex>();
        m_aListTriangles = new TriangleList[m_meshOriginal.subMeshCount];

        List<Vertex> listVertices = new List<Vertex>();

        AddVertices(m_meshUniqueVertices.ListVertices, m_meshUniqueVertices.ListVerticesWorld, m_meshUniqueVertices.ListBoneWeights);

        for (int i = 0; i < m_listVertices.Count; i++)
        {
          m_listVertices[i].m_collapse = (m_listVertexMap[i] == -1) ? null : m_listVertices[m_listVertexMap[i]];
          listVertices.Add(m_listVertices[m_listVertexPermutationBack[i]]);
        }

        Vector2[] av2Mapping = m_meshOriginal.uv;

        for (int nSubMesh = 0; nSubMesh < m_meshOriginal.subMeshCount; nSubMesh++)
        {
          int[] anIndices = m_meshOriginal.GetTriangles(nSubMesh);
          m_aListTriangles[nSubMesh] = new TriangleList();
          AddFaceListSubMesh(nSubMesh, m_meshUniqueVertices.SubmeshesFaceList[nSubMesh].m_listIndices, anIndices, av2Mapping);
        }

        int nTotalVertices = listVertices.Count;

        Stopwatch sw = Stopwatch.StartNew();

        while (listVertices.Count > nVertices)
        {
          if (progress != null)
          {
            float fT = 1.0f;
            if (nTotalVertices != nVertices && ((listVertices.Count & 0xFF) == 0))
            {
              fT = 1.0f - ((float)(listVertices.Count - nVertices) / (float)(nTotalVertices - nVertices));
              progress("Simplifying mesh: " + strProgressDisplayObjectName, "Collapsing edges", fT);

              if (Cancelled)
              {
                CoroutineEnded = true;
                yield break;
              }
            }
          }

          Vertex mn = listVertices[listVertices.Count - 1];
          listVertices.RemoveAt(listVertices.Count - 1);
          Collapse(mn, mn.m_collapse, false, null, null);

          if (sw.ElapsedMilliseconds > CoroutineFrameMiliseconds && CoroutineFrameMiliseconds > 0)
          {
            yield return null;
            sw = Stopwatch.StartNew();
          }
        }

        Vector3[] av3Vertices = new Vector3[m_listVertices.Count];

        for (int i = 0; i < m_listVertices.Count; i++)
        {
          m_listVertices[i].m_nID = i;  // reassign id's
          av3Vertices[i] = m_listVertices[i].m_v3Position;
        }

        if (Application.isEditor && !Application.isPlaying)
        {
          IEnumerator enumerator = ConsolidateMesh(gameObject, m_meshOriginal, meshOut, m_aListTriangles, av3Vertices, strProgressDisplayObjectName, progress);

          while (enumerator.MoveNext())
          {
            if (Simplifier.Cancelled)
            {
              CoroutineEnded = true;
              yield break;
            }
          }
        }
        else
        {
          yield return StartCoroutine(ConsolidateMesh(gameObject, m_meshOriginal, meshOut, m_aListTriangles, av3Vertices, strProgressDisplayObjectName, progress));
        }

        CoroutineEnded = true;
      }

      public int GetOriginalMeshUniqueVertexCount()
      {
        return m_nOriginalMeshVertexCount;
      }

      public int GetOriginalMeshTriangleCount()
      {
        return m_meshOriginal.triangles.Length / 3;
      }

      public static Vector3[] GetWorldVertices(GameObject gameObject)
      {
        Vector3[] aVertices = null;

        SkinnedMeshRenderer skin       = gameObject.GetComponent<SkinnedMeshRenderer>();
        MeshFilter          meshFilter = gameObject.GetComponent<MeshFilter>();

        if (skin != null)
        {
          if (skin.sharedMesh == null)
          {
            return null;
          }

          aVertices = skin.sharedMesh.vertices;

          BoneWeight[] aBoneWeights = skin.sharedMesh.boneWeights;
          Matrix4x4[]  aBindPoses   = skin.sharedMesh.bindposes;
          Transform[]  aBones       = skin.bones;

          if (aVertices == null || aBoneWeights == null || aBindPoses == null || aBones == null)
          {
            return null;
          }

          if(aBoneWeights.Length == 0 || aBindPoses.Length == 0 || aBones.Length == 0)
          {
            return null;
          }

          for (int nVertex = 0; nVertex < aVertices.Length; nVertex++)
          {
            BoneWeight bw = aBoneWeights[nVertex];
            Vector3 v3World = Vector3.zero;
            Vector3 v3LocalVertex;

            if (Math.Abs(bw.weight0) > 0.00001f)
            {
              v3LocalVertex = aBindPoses[bw.boneIndex0].MultiplyPoint3x4(aVertices[nVertex]);
              v3World += aBones[bw.boneIndex0].transform.localToWorldMatrix.MultiplyPoint3x4(v3LocalVertex) * bw.weight0;
            }
            if (Math.Abs(bw.weight1) > 0.00001f)
            {
              v3LocalVertex = aBindPoses[bw.boneIndex1].MultiplyPoint3x4(aVertices[nVertex]);
              v3World += aBones[bw.boneIndex1].transform.localToWorldMatrix.MultiplyPoint3x4(v3LocalVertex) * bw.weight1;
            }
            if (Math.Abs(bw.weight2) > 0.00001f)
            {
              v3LocalVertex = aBindPoses[bw.boneIndex2].MultiplyPoint3x4(aVertices[nVertex]);
              v3World += aBones[bw.boneIndex2].transform.localToWorldMatrix.MultiplyPoint3x4(v3LocalVertex) * bw.weight2;
            }
            if (Math.Abs(bw.weight3) > 0.00001f)
            {
              v3LocalVertex = aBindPoses[bw.boneIndex3].MultiplyPoint3x4(aVertices[nVertex]);
              v3World += aBones[bw.boneIndex3].transform.localToWorldMatrix.MultiplyPoint3x4(v3LocalVertex) * bw.weight3;
            }

            aVertices[nVertex] = v3World;
          }
        }
        else if(meshFilter != null)
        {
          if (meshFilter.sharedMesh == null)
          {
            return null;
          }

          aVertices = meshFilter.sharedMesh.vertices;

          if (aVertices == null)
          {
            return null;
          }

          for (int nVertex = 0; nVertex < aVertices.Length; nVertex++)
          {
            aVertices[nVertex] = gameObject.transform.TransformPoint(aVertices[nVertex]);
          }
        }

        return aVertices;
      }

      #endregion // Public methods

      #region Private methods

      /////////////////////////////////////////////////////////////////////////////////////////////////
      // Private methods
      /////////////////////////////////////////////////////////////////////////////////////////////////

      IEnumerator ConsolidateMesh(GameObject gameObject, Mesh meshIn, Mesh meshOut, TriangleList[] aListTriangles, Vector3[] av3Vertices, string strProgressDisplayObjectName = "", ProgressDelegate progress = null)
      {
        Vector3[]    av3NormalsIn  = meshIn.normals;
        Vector4[]    av4TangentsIn = meshIn.tangents;
        Vector2[]    av2Mapping1In = meshIn.uv;
        Vector2[]    av2Mapping2In = meshIn.uv2;
        Color[]      acolColorsIn  = meshIn.colors;
        Color32[]    aColors32In   = meshIn.colors32;

        List<List<int>>  listlistIndicesOut = new List<List<int>>();
        List<Vector3>    listVerticesOut    = new List<Vector3>();
        List<Vector3>    listNormalsOut     = new List<Vector3>();
        List<Vector4>    listTangentsOut    = new List<Vector4>();
        List<Vector2>    listMapping1Out    = new List<Vector2>();
        List<Vector2>    listMapping2Out    = new List<Vector2>();
        List<Color32>    listColors32Out    = new List<Color32>();
        List<BoneWeight> listBoneWeightsOut = new List<BoneWeight>();

        Dictionary<VertexDataHash, int> dicVertexDataHash2Index = new Dictionary<VertexDataHash, int>(new VertexDataHashComparer());

        bool bUV1     = av2Mapping1In != null && av2Mapping1In.Length > 0;
        bool bUV2     = av2Mapping2In != null && av2Mapping2In.Length > 0;
        bool bNormal  = av3NormalsIn  != null && av3NormalsIn.Length  > 0;
        bool bTangent = av4TangentsIn != null && av4TangentsIn.Length > 0;

        Stopwatch sw = Stopwatch.StartNew();

        for(int nSubMesh = 0; nSubMesh < aListTriangles.Length; nSubMesh++)
        {
          List<int> listIndicesOut = new List<int>();

          string strMesh = aListTriangles.Length > 1 ? ("Consolidating submesh " + (nSubMesh + 1)) : "Consolidating mesh";

          for (int i = 0; i < aListTriangles[nSubMesh].m_listTriangles.Count; i++)
          {
            if (progress != null && ((i & 0xFF) == 0))
            {
              float fT = aListTriangles[nSubMesh].m_listTriangles.Count == 1 ? 1.0f : ((float)i / (float)(aListTriangles[nSubMesh].m_listTriangles.Count - 1));

              progress("Simplifying mesh: " + strProgressDisplayObjectName, strMesh, fT);

              if (Cancelled)
              {
                yield break;
              }
            }

            if (sw.ElapsedMilliseconds > CoroutineFrameMiliseconds && CoroutineFrameMiliseconds > 0)
            {
              yield return null;
              sw = Stopwatch.StartNew();
            }

            for (int v = 0; v < 3; v++)
            {
              int nMappingIndex = aListTriangles[nSubMesh].m_listTriangles[i].IndicesUV[v];
              int nVertexIndex  = aListTriangles[nSubMesh].m_listTriangles[i].Indices[v];

              bool bColor = false;

              Vector3 v3Vertex  = aListTriangles[nSubMesh].m_listTriangles[i].Vertices[v].m_v3Position;
              Vector3 v3Normal  = bNormal  ? av3NormalsIn [nVertexIndex]  : Vector3.zero;
              Vector4 v4Tangent = bTangent ? av4TangentsIn[nVertexIndex]  : Vector4.zero;
              Vector2 uv1       = bUV1     ? av2Mapping1In[nMappingIndex] : Vector2.zero;
              Vector2 uv2       = bUV2     ? av2Mapping2In[nVertexIndex]  : Vector2.zero;
              Color32 color32   = new Color32(0, 0, 0, 0);
         
              if(acolColorsIn  != null && acolColorsIn.Length > 0)
              {
                color32 = acolColorsIn[nVertexIndex];
                bColor  = true;
              }
              else if(aColors32In  != null && aColors32In.Length > 0)
              {
                color32 = aColors32In[nVertexIndex];
                bColor  = true;
              }

              VertexDataHash vdata = new VertexDataHash(v3Vertex, v3Normal, uv1, uv2, color32);

              if (dicVertexDataHash2Index.ContainsKey(vdata))
              {
                // Already exists -> Index
                listIndicesOut.Add(dicVertexDataHash2Index[vdata]);
              }
              else
              {
                // Does not exist -> Create + index

                dicVertexDataHash2Index.Add(vdata, listVerticesOut.Count);
                listVerticesOut.Add(vdata.Vertex);

                if (bNormal)     listNormalsOut.Add(v3Normal);
                if (bUV1)        listMapping1Out.Add(uv1);
                if (bUV2)        listMapping2Out.Add(uv2);
                if (bTangent)    listTangentsOut.Add(v4Tangent);
                if (bColor)      listColors32Out.Add(color32);

                if (aListTriangles[nSubMesh].m_listTriangles[i].Vertices[v].m_bHasBoneWeight)
                {
                  listBoneWeightsOut.Add(aListTriangles[nSubMesh].m_listTriangles[i].Vertices[v].m_boneWeight);
                }

                listIndicesOut.Add(listVerticesOut.Count - 1);
              }
            }
          }

          listlistIndicesOut.Add(listIndicesOut);
        }

        meshOut.triangles    = new int[0];
        meshOut.vertices     = listVerticesOut.ToArray();
        meshOut.normals      = listNormalsOut.Count     > 0 ? listNormalsOut.ToArray()     : null;
        meshOut.tangents     = listTangentsOut.Count    > 0 ? listTangentsOut.ToArray()    : null;
        meshOut.uv           = listMapping1Out.Count    > 0 ? listMapping1Out.ToArray()    : null;
        meshOut.uv2          = listMapping2Out.Count    > 0 ? listMapping2Out.ToArray()    : null;
        meshOut.colors32     = listColors32Out.Count    > 0 ? listColors32Out.ToArray()    : null;
        meshOut.boneWeights  = listBoneWeightsOut.Count > 0 ? listBoneWeightsOut.ToArray() : null;
        meshOut.bindposes    = meshIn.bindposes;
        meshOut.subMeshCount = listlistIndicesOut.Count;

        for (int nSubMesh = 0; nSubMesh < listlistIndicesOut.Count; nSubMesh++)
        {
          meshOut.SetTriangles(listlistIndicesOut[nSubMesh].ToArray(), nSubMesh);
        }

        meshOut.name = gameObject.name + " simplified mesh";
        ;

        progress("Simplifying mesh: " + strProgressDisplayObjectName, "Mesh consolidation done", 1.0f);
      }

      int MapVertex(int nVertex, int nMax)
      {
        if (nMax <= 0)
        {
          return 0;
        }

        while (nVertex >= nMax)
        {
          nVertex = m_listVertexMap[nVertex];
        }

        return nVertex;
      }

      float ComputeEdgeCollapseCost(Vertex u, Vertex v, float fRelevanceBias)
      {
        bool bUseEdgeLength  = m_bUseEdgeLength;
        bool bUseCurvature   = m_bUseCurvature;
        bool bProtectTexture = m_bProtectTexture;
        bool bLockBorder     = m_bLockBorder;

        int   i;
        float fEdgeLength = bUseEdgeLength ? (Vector3.Magnitude(v.m_v3Position - u.m_v3Position) / m_fOriginalMeshSize) : 1.0f;
        float fCurvature  = 0.001f;

        List<Triangle> sides = new List<Triangle>();

        for (i = 0; i < u.m_listFaces.Count; i++)
        {
          if (u.m_listFaces[i].HasVertex(v))
          {
            sides.Add(u.m_listFaces[i]);
          }
        }

        if(bUseCurvature)
        {
          for (i = 0; i < u.m_listFaces.Count; i++)
          {
            float fMinCurv = 1.0f;

            for (int j = 0; j < sides.Count; j++)
            {
              float dotprod = Vector3.Dot(u.m_listFaces[i].Normal, sides[j].Normal);
              fMinCurv = Mathf.Min(fMinCurv, (1.0f - dotprod) / 2.0f);
            }

            fCurvature = Mathf.Max(fCurvature, fMinCurv);
          }
        }

        if (u.IsBorder() && sides.Count > 1)
        {
          fCurvature = 1.0f;
        }

        if (bProtectTexture)
        {
          bool bNoMatch = true;

          for (i = 0; i < u.m_listFaces.Count; i++)
          {
            for (int j = 0; j < sides.Count; j++)
            {
              if (u.m_listFaces[i].HasUVData == false)
              {
                bNoMatch = false;
                break;
              }

              if (u.m_listFaces[i].TexAt(u) == sides[j].TexAt(u))
              {
                bNoMatch = false;
              }
            }
          }

          if (bNoMatch)
          {
            fCurvature = 1.0f;
          }
        }

        if (bLockBorder && u.IsBorder())
        {
          fCurvature = MAX_VERTEX_COLLAPSE_COST;
        }

        fCurvature += fRelevanceBias;

        return fEdgeLength * fCurvature;
      }

      void ComputeEdgeCostAtVertex(Vertex v, Transform transform, RelevanceSphere[] aRelevanceSpheres)
      {
        if (v.m_listNeighbors.Count == 0)
        {
          v.m_collapse = null;
          v.m_fObjDist = -0.01f;
          return;
        }

        v.m_fObjDist = MAX_VERTEX_COLLAPSE_COST;
        v.m_collapse = null;

        float fRelevanceBias = 0.0f;

        if (aRelevanceSpheres != null)
        {
          for (int nSphere = 0; nSphere < aRelevanceSpheres.Length; nSphere++)
          {
            Matrix4x4 mtxSphere = Matrix4x4.TRS(aRelevanceSpheres[nSphere].m_v3Position, Quaternion.Euler(aRelevanceSpheres[nSphere].m_v3Rotation), aRelevanceSpheres[nSphere].m_v3Scale);

            Vector3 v3World = v.m_v3PositionWorld;
            Vector3 v3Local = mtxSphere.inverse.MultiplyPoint(v3World);

            if (v3Local.magnitude <= 0.5f)
            {
              // Inside
              fRelevanceBias = aRelevanceSpheres[nSphere].m_fRelevance;
            }
          }
        }

        for (int i = 0; i < v.m_listNeighbors.Count; i++)
        {
          float dist = ComputeEdgeCollapseCost(v, v.m_listNeighbors[i], fRelevanceBias);

          if (v.m_collapse == null || dist < v.m_fObjDist)
          {
            v.m_collapse = v.m_listNeighbors[i];
            v.m_fObjDist = dist;
          }
        }
      }

      IEnumerator ComputeAllEdgeCollapseCosts(string strProgressDisplayObjectName, Transform transform, RelevanceSphere[] aRelevanceSpheres, ProgressDelegate progress = null)
      {
        Stopwatch sw = Stopwatch.StartNew();

        for (int i = 0; i < m_listVertices.Count; i++)
        {
          if (progress != null && ((i & 0xFF) == 0))
          {
            progress("Preprocessing mesh: " + strProgressDisplayObjectName, "Computing edge collapse cost", m_listVertices.Count == 1 ? 1.0f : ((float)i / (m_listVertices.Count - 1.0f)));

            if(Cancelled)
            {
              yield break;
            }
          }

          if (sw.ElapsedMilliseconds > CoroutineFrameMiliseconds && CoroutineFrameMiliseconds > 0)
          {
            yield return null;
            sw = Stopwatch.StartNew();
          }

          ComputeEdgeCostAtVertex(m_listVertices[i], transform, aRelevanceSpheres);
          HeapAdd(m_listVertices[i]);
        }
      }

      void Collapse(Vertex u, Vertex v, bool bRecompute, Transform transform, RelevanceSphere[] aRelevanceSpheres)
      {
        if (v == null)
        {
          u.Destructor(this);
          return;
        }

        int i;
        List<Vertex> tmp = new List<Vertex>();

        for (i = 0; i < u.m_listNeighbors.Count; i++)
        {
          tmp.Add(u.m_listNeighbors[i]);
        }

        List<Triangle> sides = new List<Triangle>();

        for (i = 0; i < u.m_listFaces.Count; i++)
        {
          if (u.m_listFaces[i].HasVertex(v))
          {
            sides.Add(u.m_listFaces[i]);
          }
        }

        // update texture mapping

        for (i = 0; i < u.m_listFaces.Count; i++)
        {
          int j;

          if (u.m_listFaces[i].HasVertex(v))
          {
            continue;
          }

          if (u.m_listFaces[i].HasUVData)
          {
            for (j = 0; j < sides.Count; j++)
            {
              if (u.m_listFaces[i].TexAt(u) == sides[j].TexAt(u))
              {
                u.m_listFaces[i].SetTexAt(u, sides[j].TexAt(v));
                break; // only change tex coords once!
              }
            }
          }

          // Added support for color or 2nd uv here:

          /*

          for (j = 0; j < sides.Count; j++)
          {
            if (u.m_listFaces[i].VertexColorAt(u) == sides[j].VertexColorAt(u))
            {
              u.m_listFaces[i].SetVertexColorAt(u, sides[j].VertexColorAt(v));
              break; // only change tex coords once!
            }
          }

          */
        }

        // Delete triangles on edge uv

        for (i = u.m_listFaces.Count - 1; i >= 0; i--)
        {
          if(i < u.m_listFaces.Count && i >= 0 && u.m_listFaces[i].HasVertex(v))
          {
            u.m_listFaces[i].Destructor(this);
          }
        }

        // Update remaining triangles to have v instead of u

        for (i = u.m_listFaces.Count - 1; i >= 0; i--)
        {
          u.m_listFaces[i].ReplaceVertex(u, v);
        }

        u.Destructor(this);

        // Recompute the edge collapse costs for neighboring vertices

        if (bRecompute)
        {
          for (i = 0; i < tmp.Count; i++)
          {
            ComputeEdgeCostAtVertex(tmp[i], transform, aRelevanceSpheres);
            HeapSortUp(tmp[i].m_nHeapSpot);
            HeapSortDown(tmp[i].m_nHeapSpot);
          }
        }
      }

      void AddVertices(List<Vector3> listVertices, List<Vector3> listVerticesWorld, List<MeshUniqueVertices.SerializableBoneWeight> listBoneWeights)
      {
        bool bHasBoneWeights = listBoneWeights != null && listBoneWeights.Count > 0;

        for (int i = 0; i < listVertices.Count; i++)
        {
          new Vertex(this, listVertices[i], listVerticesWorld[i], bHasBoneWeights, bHasBoneWeights ? listBoneWeights[i].ToBoneWeight() : new BoneWeight(), i);
        }
      }

      void AddFaceListSubMesh(int nSubMesh, List<int> listTriangles, int[] anIndices, Vector2[] v2Mapping)
      {
        bool bUVData = false;

        if (v2Mapping != null)
        {
          if (v2Mapping.Length > 0)
          {
            bUVData = true;
          }
        }

        for (int i = 0; i < listTriangles.Count / 3; i++)
        {
          Triangle tri = new Triangle(this, nSubMesh,
                                      m_listVertices[listTriangles[i * 3]], m_listVertices[listTriangles[i * 3 + 1]], m_listVertices[listTriangles[i * 3 + 2]],
                                      bUVData, anIndices[i * 3], anIndices[i * 3 + 1], anIndices[i * 3 + 2]);
          ShareUV(v2Mapping, tri);
        }
      }

      void ShareUV(Vector2[] aMapping, Triangle t)
      {
        if (t.HasUVData == false)
        {
          return;
        }

        // It so happens that neighboring faces that share vertices
        // sometimes share uv coordinates at those verts but have
        // their own entries in the tex vert list

        if (aMapping == null || aMapping.Length == 0)
        {
          return;
        }

        for (int i = 0; i < 3; i++)
        {
          int nCurrentVert = i;

          for (int j = 0; j < t.Vertices[nCurrentVert].m_listFaces.Count; j++)
          {
            Triangle n = t.Vertices[nCurrentVert].m_listFaces[j];

            if (t == n)
            {
              continue;
            }

            int tx1 = t.TexAt(t.Vertices[nCurrentVert]);
            int tx2 = n.TexAt(t.Vertices[nCurrentVert]);

            if (tx1 == tx2)
            {
              continue;
            }

            Vector2 uv1 = aMapping[tx1];
            Vector2 uv2 = aMapping[tx2];

            if (uv1 == uv2)
            {
              t.SetTexAt(t.Vertices[nCurrentVert], tx2);
            }
          }
        }
      }

      Vertex MinimumCostEdge()
      {
        // Find the edge that when collapsed will affect model the least.
        // This funtion actually returns a Vertex, the second vertex
        // of the edge (collapse candidate) is stored in the vertex data.
        Vertex hp = HeapPop();
        return hp;
      }

      float HeapValue(int i)
      {
        if (i < 0 || i >= m_listHeap.Count)
        {
          return 9999999999999.9f;
        }

        if (m_listHeap [i] == null)
        {
          return 9999999999999.9f;
        }

        return m_listHeap[i].m_fObjDist;
      }

      void HeapSortUp(int k)
      {
        int k2;

        while (HeapValue(k) < HeapValue((k2 = (k - 1) / 2)))
        {
          Vertex tmp = m_listHeap[k];
          m_listHeap[k] = m_listHeap[k2];
          m_listHeap[k].m_nHeapSpot = k;
          m_listHeap[k2] = tmp;
          m_listHeap[k2].m_nHeapSpot = k2;
          k = k2;
        }
      }

      void HeapSortDown(int k)
      {
        int k2;

        if (k == -1)
        {
          return;
        }

        while (HeapValue(k) > HeapValue((k2 = (k + 1) * 2)) || HeapValue(k) > HeapValue((k2 - 1)))
        {
          k2 = (HeapValue(k2) < HeapValue(k2 - 1)) ? k2 : k2 - 1;
          Vertex tmp = m_listHeap[k];
          m_listHeap[k] = m_listHeap[k2];
          m_listHeap[k].m_nHeapSpot = k;
          m_listHeap[k2] = tmp;
          if (tmp != null) m_listHeap[k2].m_nHeapSpot = k2;
          k = k2;
        }
      }

      void HeapAdd(Vertex v)
      {
        int k = m_listHeap.Count;
        m_listHeap.Add(v);
        v.m_nHeapSpot = k;
        HeapSortUp(k);
      }

      Vertex HeapPop()
      {
        Vertex rv = m_listHeap[0];
        rv.m_nHeapSpot = -1;
        m_listHeap[0] = null;
        HeapSortDown(0);
        return rv;
      }

      #endregion Private methods

      #region Private types

      /////////////////////////////////////////////////////////////////////////////////////////////////
      // Private types
      /////////////////////////////////////////////////////////////////////////////////////////////////

      /// <summary>
      /// Stores vertex and mapping information.
      /// </summary>
      private class Triangle
      {
        public Vertex[] Vertices
        {
          get
          {
            return m_aVertices;
          }
        }

        public bool HasUVData
        {
          get
          {
            return m_bUVData;
          }
        }

        public int[] IndicesUV
        {
          get
          {
            return m_aUV;
          }
        }

        public Vector3 Normal
        {
          get
          {
            return m_v3Normal;
          }
        }

        public int[] Indices
        {
          get
          {
            return m_aIndices;
          }
        }

        private Vertex[]     m_aVertices;
        private bool         m_bUVData;
        private int[]        m_aUV;
        private int[]        m_aIndices;
        private Vector3      m_v3Normal;
        private int          m_nSubMesh;

        public Triangle(Simplifier simplifier, int nSubMesh, Vertex v0, Vertex v1, Vertex v2, bool bUVData, int nIndex1, int nIndex2, int nIndex3)
        {
          m_aVertices = new Vertex[3];
          m_aUV       = new int[3];
          m_aIndices  = new int[3];

          m_aVertices[0] = v0;
          m_aVertices[1] = v1;
          m_aVertices[2] = v2;

          m_nSubMesh = nSubMesh;

          m_bUVData = bUVData;

          if (m_bUVData)
          {
            m_aUV[0] = nIndex1;
            m_aUV[1] = nIndex2;
            m_aUV[2] = nIndex3;
          }

          m_aIndices[0] = nIndex1;
          m_aIndices[1] = nIndex2;
          m_aIndices[2] = nIndex3;

          ComputeNormal();

          simplifier.m_aListTriangles[nSubMesh].m_listTriangles.Add(this);

          for (int i = 0; i < 3; i++)
          {
            m_aVertices[i].m_listFaces.Add(this);

            for (int j = 0; j < 3; j++)
            {
              if (i != j)
              {
                if (m_aVertices[i].m_listNeighbors.Contains(m_aVertices[j]) == false)
                {
                  m_aVertices[i].m_listNeighbors.Add(m_aVertices[j]);
                }
              }
            }
          }
        }

        public void Destructor(Simplifier simplifier)
        {
          int i;
          simplifier.m_aListTriangles[m_nSubMesh].m_listTriangles.Remove(this);

          for (i = 0; i < 3; i++)
          {
            if (m_aVertices[i] != null)
            {
              m_aVertices[i].m_listFaces.Remove(this);
            }
          }

          for (i = 0; i < 3; i++)
          {
            int i2 = (i + 1) % 3;

            if (m_aVertices[i] == null || m_aVertices[i2] == null) continue;

            m_aVertices[i].RemoveIfNonNeighbor(m_aVertices[i2]);
            m_aVertices[i2].RemoveIfNonNeighbor(m_aVertices[i]);
          }
        }

        public bool HasVertex(Vertex v)
        {
          return (v == m_aVertices[0] || v == m_aVertices[1] || v == m_aVertices[2]);
        }

        public void ComputeNormal()
        {
          Vector3 v0 = m_aVertices[0].m_v3Position;
          Vector3 v1 = m_aVertices[1].m_v3Position;
          Vector3 v2 = m_aVertices[2].m_v3Position;

          m_v3Normal = Vector3.Cross((v1 - v0), (v2 - v1));

          if (m_v3Normal.magnitude == 0.0f) return;

          m_v3Normal = m_v3Normal.normalized;
        }

        public int TexAt(Vertex vertex)
        {
          for (int i = 0; i < 3; i++)
          {
            if (m_aVertices[i] == vertex)
            {
              return m_aUV[i];
            }
          }

          UnityEngine.Debug.LogError("TexAt(): Vertex not found");
          return 0;
        }

        public int TexAt(int i)
        {
          return m_aUV[i];
        }

        public void SetTexAt(Vertex vertex, int uv)
        {
          for (int i = 0; i < 3; i++)
          {
            if (m_aVertices[i] == vertex)
            {
              m_aUV[i] = uv;
              return;
            }
          }

          UnityEngine.Debug.LogError("SetTexAt(): Vertex not found");
        }

        public void SetTexAt(int i, int uv)
        {
          m_aUV[i] = uv;
        }

        public void ReplaceVertex(Vertex vold, Vertex vnew)
        {
          if (vold == m_aVertices[0])
          {
            m_aVertices[0] = vnew;
          }
          else if (vold == m_aVertices[1])
          {
            m_aVertices[1] = vnew;
          }
          else
          {
            m_aVertices[2] = vnew;
          }

          int i;

          vold.m_listFaces.Remove(this);
          vnew.m_listFaces.Add(this);

          for (i = 0; i < 3; i++)
          {
            vold.RemoveIfNonNeighbor(m_aVertices[i]);
            m_aVertices[i].RemoveIfNonNeighbor(vold);
          }

          for (i = 0; i < 3; i++)
          {
            for (int j = 0; j < 3; j++)
            {
              if (i != j)
              {
                if (m_aVertices[i].m_listNeighbors.Contains(m_aVertices[j]) == false)
                {
                  m_aVertices[i].m_listNeighbors.Add(m_aVertices[j]);
                }
              }
            }
          }

          ComputeNormal();
        }
      };

      /// <summary>
      /// A list of triangles. We encapsulate this as a class to be able to serialize a List of TriangleLists if we need to.
      /// Unity doesn't serialize a list of lists or an array of lists.
      /// </summary>
      private class TriangleList
      {
        public TriangleList()
        {
          m_listTriangles = new List<Triangle>();
        }

        public List<Triangle> m_listTriangles;
      }

      /// <summary>
      /// Stores topology information and edge collapsing information.
      /// </summary>
      private class Vertex
      {
        public Vector3        m_v3Position;
        public Vector3        m_v3PositionWorld;
        public bool           m_bHasBoneWeight;
        public BoneWeight     m_boneWeight;
        public int            m_nID;           // Place of vertex in original list
        public List<Vertex>   m_listNeighbors; // Adjacent vertices
        public List<Triangle> m_listFaces;     // Adjacent triangles
        public float          m_fObjDist;      // Cached cost of collapsing edge
        public Vertex         m_collapse;      // Candidate vertex for collapse
        public int            m_nHeapSpot;     // Heap spot, for optimization purposes.

        public Vertex(Simplifier simplifier, Vector3 v, Vector3 v3World, bool bHasBoneWeight, BoneWeight boneWeight, int nID)
        {
          m_v3Position = v;
          m_v3PositionWorld = v3World;
          m_bHasBoneWeight = bHasBoneWeight;
          m_boneWeight = boneWeight;
          this.m_nID = nID;

          m_listNeighbors = new List<Vertex>();
          m_listFaces = new List<Triangle>();

          simplifier.m_listVertices.Add(this);
        }

        public void Destructor(Simplifier simplifier)
        {
          while (m_listNeighbors.Count > 0)
          {
            m_listNeighbors[0].m_listNeighbors.Remove(this);

            if (m_listNeighbors.Count > 0)
            {
              m_listNeighbors.RemoveAt(0);
            }
          }

          simplifier.m_listVertices.Remove(this);
        }

        public void RemoveIfNonNeighbor(Vertex n)
        {
          if (!m_listNeighbors.Contains(n))
          {
            return;
          }

          for (int i = 0; i < m_listFaces.Count; i++)
          {
            if (m_listFaces[i].HasVertex(n))
            {
              return;
            }
          }

          m_listNeighbors.Remove(n);
        }

        public bool IsBorder()
        {
          int i, j;

          for (i = 0; i < m_listNeighbors.Count; i++)
          {
            int nCount = 0;

            for (j = 0; j < m_listFaces.Count; j++)
            {
              if (m_listFaces[j].HasVertex(m_listNeighbors[i]))
              {
                nCount++;
              }
            }

            if (nCount == 1)
            {
              return true;
            }
          }

          return false;
        }
      };

      class VertexDataHashComparer : IEqualityComparer<VertexDataHash>
      {
        public bool Equals(VertexDataHash a, VertexDataHash b)
        {
          return ((a.UV1 == b.UV1) && (a.UV2 == b.UV2) && (a.Vertex == b.Vertex) && //(a.Normal == b.Normal) &&
                 (a.Color.r == b.Color.r) && (a.Color.g == b.Color.g) && (a.Color.b == b.Color.b) && (a.Color.a == b.Color.a));
        }

        public int GetHashCode(VertexDataHash vdata)
        {
          return vdata.GetHashCode();
        }
      }

      /// <summary>
      /// Stores vertex data information. Also allows to compare two different objects of this type to
      /// know when two vertices share or not the same data.
      /// </summary>
      class VertexDataHash
      {
        public Vector3 Vertex
        {
          get
          {
            return _v3Vertex;
          }
        }

        public Vector3 Normal
        {
          get
          {
            return _v3Normal;
          }
        }

        public Vector2 UV1
        {
          get
          {
            return _v2Mapping1;
          }
        }

        public Vector2 UV2
        {
          get
          {
            return _v2Mapping2;
          }
        }

        public Color32 Color
        {
          get
          {
            return _color;
          }
        }

        public VertexDataHash(Vector3 v3Vertex, Vector3 v3Normal, Vector2 v2Mapping1, Vector2 v2Mapping2, Color32 color)
        {
          _v3Vertex     = v3Vertex;
          _v3Normal     = v3Normal;
          _v2Mapping1   = v2Mapping1;
          _v2Mapping2   = v2Mapping2;
          _color        = color;
          _uniqueVertex = new MeshUniqueVertices.UniqueVertex(v3Vertex);
          //_uniqueNormal = new MeshUniqueVertices.UniqueVertex(v3Normal);
        }

        public override bool Equals(object obj)
        {
          VertexDataHash v = obj as VertexDataHash;

          return ((v._v2Mapping1 == _v2Mapping1) && (v._v2Mapping2 == _v2Mapping2) && (v._v3Vertex == _v3Vertex) && //&& (v._v3Normal == _v3Normal) &&
                  (v._color.r == _color.r) && (v._color.g == _color.g) && (v._color.b == _color.b) && (v._color.a == _color.a));
        }

        public override int GetHashCode()
        {
          return _uniqueVertex.GetHashCode();// +_uniqueNormal.GetHashCode();
        }

        // Public static

        public static bool operator ==(VertexDataHash a, VertexDataHash b)
        {
          return a.Equals(b);
        }

        public static bool operator !=(VertexDataHash a, VertexDataHash b)
        {
          return !a.Equals(b);
        }

        private Vector3 _v3Vertex;
        private Vector3 _v3Normal;
        private Vector2 _v2Mapping1;
        private Vector2 _v2Mapping2;
        private Color32 _color;
        private MeshUniqueVertices.UniqueVertex _uniqueVertex;
        //private MeshUniqueVertices.UniqueVertex _uniqueNormal;
      }  

      #endregion // Private types

      #region Private vars

      /////////////////////////////////////////////////////////////////////////////////////////////////
      // Private vars
      /////////////////////////////////////////////////////////////////////////////////////////////////

      private static int  m_nCoroutineFrameMiliseconds = 0;
      private const float MAX_VERTEX_COLLAPSE_COST     = 10000000.0f;

      private List<Vertex>   m_listVertices;
      private List<Vertex>   m_listHeap;
      private TriangleList[] m_aListTriangles;
      [SerializeField, HideInInspector]
      private int   m_nOriginalMeshVertexCount = -1;
      [SerializeField, HideInInspector]
      private float m_fOriginalMeshSize        = 1.0f;

      [SerializeField, HideInInspector]
      private List<int> m_listVertexMap;
      [SerializeField, HideInInspector]
      private List<int> m_listVertexPermutationBack;

      [SerializeField, HideInInspector]
      private MeshUniqueVertices m_meshUniqueVertices;

      [SerializeField, HideInInspector]
      private Mesh m_meshOriginal;

      [SerializeField, HideInInspector]
      bool m_bUseEdgeLength = true, m_bUseCurvature = true, m_bProtectTexture = true, m_bLockBorder = true;


      #endregion // Private vars
    }
  }
}