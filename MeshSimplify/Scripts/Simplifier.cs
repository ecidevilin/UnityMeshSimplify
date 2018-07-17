using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace UltimateGameTools
{
  namespace MeshSimplifier
  {

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

        //Vector3[] aVerticesWorld = GetWorldVertices(gameObject);

        //if (aVerticesWorld == null)
        //{
        //  CoroutineEnded = true;
        //  yield break;
        //}

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
        m_meshUniqueVertices.BuildData(m_meshOriginal, gameObject);

        m_nOriginalMeshVertexCount = m_meshUniqueVertices.ListVertices.Count;
        m_fOriginalMeshSize        = Mathf.Max(m_meshOriginal.bounds.size.x, m_meshOriginal.bounds.size.y, m_meshOriginal.bounds.size.z);

        m_heap = Heap<Vertex>.CreateMinHeap();

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

            bool bUV1 = av2Mapping1In != null && av2Mapping1In.Length > 0;
            bool bUV2 = av2Mapping2In != null && av2Mapping2In.Length > 0;
            bool bNormal = av3NormalsIn != null && av3NormalsIn.Length > 0;
            bool bTangent = av4TangentsIn != null && av4TangentsIn.Length > 0;

            bool bColor = (acolColorsIn != null && acolColorsIn.Length > 0) || (aColors32In != null && aColors32In.Length > 0);

            List<List<int>>  listlistIndicesOut = new List<List<int>>();
            List<Vector3>    listVerticesOut    = new List<Vector3>();
            List<Vector3>    listNormalsOut     = bNormal ? new List<Vector3>() : null;
            List<Vector4>    listTangentsOut    = bTangent ? new List<Vector4>() : null;
            List<Vector2>    listMapping1Out    = bUV1? new List<Vector2>() : null;
            List<Vector2>    listMapping2Out    = bUV2 ? new List<Vector2>() : null;
            List<Color32>    listColors32Out    = bColor ? new List<Color32>() : null;
            List<BoneWeight> listBoneWeightsOut = new List<BoneWeight>();

            Dictionary<VertexDataHash, int> dicVertexDataHash2Index = new Dictionary<VertexDataHash, int>(new VertexDataHashComparer());

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

                        Vector3 v3Vertex  = aListTriangles[nSubMesh].m_listTriangles[i].Vertices[v].m_v3Position;
                        Vector3 v3Normal  = bNormal  ? av3NormalsIn [nVertexIndex]  : Vector3.zero;
                        Vector4 v4Tangent = bTangent ? av4TangentsIn[nVertexIndex]  : Vector4.zero;
                        Vector2 uv1       = bUV1     ? av2Mapping1In[nMappingIndex] : Vector2.zero;
                        Vector2 uv2       = bUV2     ? av2Mapping2In[nVertexIndex]  : Vector2.zero;
                        Color32 color32   = new Color32(0, 0, 0, 0);

                        if (bColor)
                        {
                            if (acolColorsIn != null && acolColorsIn.Length > 0)
                            {
                                color32 = acolColorsIn[nVertexIndex];
                            }
                            else if (aColors32In != null && aColors32In.Length > 0)
                            {
                                color32 = aColors32In[nVertexIndex];
                            }
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
            meshOut.normals      = bNormal ? listNormalsOut.ToArray()     : null;
            meshOut.tangents     = bTangent ? listTangentsOut.ToArray()    : null;
            meshOut.uv           = bUV1 ? listMapping1Out.ToArray()    : null;
            meshOut.uv2          = bUV2 ? listMapping2Out.ToArray()    : null;
            meshOut.colors32     = bColor ? listColors32Out.ToArray()    : null;
            meshOut.boneWeights  = listBoneWeightsOut.Count > 0 ? listBoneWeightsOut.ToArray() : null;
            meshOut.bindposes    = meshIn.bindposes;
            meshOut.subMeshCount = listlistIndicesOut.Count;

            for (int nSubMesh = 0; nSubMesh < listlistIndicesOut.Count; nSubMesh++)
            {
                meshOut.SetTriangles(listlistIndicesOut[nSubMesh].ToArray(), nSubMesh);
            }

            meshOut.name = gameObject.name + " simplified mesh";

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
            Matrix4x4 mtxSphere = Matrix4x4.TRS(aRelevanceSpheres[nSphere].m_v3Position,aRelevanceSpheres[nSphere].m_q4Rotation, aRelevanceSpheres[nSphere].m_v3Scale);

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
          m_heap.Insert(m_listVertices[i]);
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
                        m_heap.ModifyValue(tmp[i].m_nHeapSpot, tmp[i]);
          }
        }
      }

      void AddVertices(List<Vector3> listVertices, List<Vector3> listVerticesWorld, List<SerializableBoneWeight> listBoneWeights)
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
        Vertex hp = m_heap.ExtractTop();
        return hp;
      }

      #endregion Private methods

      #region Private vars

      /////////////////////////////////////////////////////////////////////////////////////////////////
      // Private vars
      /////////////////////////////////////////////////////////////////////////////////////////////////

      private static int  m_nCoroutineFrameMiliseconds = 0;
      private const float MAX_VERTEX_COLLAPSE_COST     = 10000000.0f;

      public List<Vertex>   m_listVertices;
        private Heap<Vertex> m_heap;
            public TriangleList[] m_aListTriangles;
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