using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine.Profiling;

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

            #endregion // Types

            #region Properties

            public static bool Cancelled { get; set; }
            public static int CoroutineFrameMiliseconds
            {
                get { return m_nCoroutineFrameMiliseconds; }
                set { m_nCoroutineFrameMiliseconds = value; }
            }

            public bool CoroutineEnded { get; set; }

            public bool UseEdgeLength {
                get { return m_bUseEdgeLength; }
                set { m_bUseEdgeLength = value; }
            }

            public bool UseCurvature {
                get { return m_bUseCurvature; }
                set { m_bUseCurvature = value; }
            }

            public bool ProtectTexture {
                get { return m_bProtectTexture; }
                set { m_bProtectTexture = value; }
            }

            public bool LockBorder {
                get { return m_bLockBorder; }
                set { m_bLockBorder = value; }
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

                m_listVertexMap = new List<int>();
                m_listVertexPermutationBack = new List<int>();
                m_listVertices = new List<Vertex>();
                m_aListTriangles = new TriangleList[m_meshOriginal.subMeshCount];

//                if (progress != null)
//                {
//                    progress("Preprocessing mesh: " + strProgressDisplayObjectName, "Building unique vertex data", 1.0f);
//
//                    if (Simplifier.Cancelled)
//                    {
//                        CoroutineEnded = true;
//                        yield break;
//                    }
//                }

//                m_meshUniqueVertices = new MeshUniqueVertices();
//                m_meshUniqueVertices.BuildData(m_meshOriginal, gameObject);
				Vector3[] worldVertices = new Vector3[sourceMesh.vertices.Length];
				SkinnedMeshRenderer skin;
				MeshFilter meshFilter;
				if ((skin = gameObject.GetComponent<SkinnedMeshRenderer> ()) != null) {
					BoneWeight[] aBoneWeights = sourceMesh.boneWeights;
					Matrix4x4[] aBindPoses = sourceMesh.bindposes;
					Transform[] aBones = skin.bones;

					for (int nVertex = 0; nVertex < sourceMesh.vertices.Length; nVertex++) {
						BoneWeight bw = aBoneWeights [nVertex];
						Vector4 v = sourceMesh.vertices [nVertex];
						v.w = 1;
						Vector3 v3World = aBones [bw.boneIndex0].localToWorldMatrix * aBindPoses [bw.boneIndex0] * v * bw.weight0
						                     + aBones [bw.boneIndex1].localToWorldMatrix * aBindPoses [bw.boneIndex1] * v * bw.weight1
						                     + aBones [bw.boneIndex2].localToWorldMatrix * aBindPoses [bw.boneIndex2] * v * bw.weight2
						                     + aBones [bw.boneIndex3].localToWorldMatrix * aBindPoses [bw.boneIndex3] * v * bw.weight3;

						worldVertices [nVertex] = v3World;

						if (progress != null && ((nVertex & 0xFF) == 0))
						{
							progress("Preprocessing mesh: " + strProgressDisplayObjectName, "Collecting vertex data", ((float)nVertex / (float)sourceMesh.vertices.Length));

							if (Cancelled)
							{
								CoroutineEnded = true;
								yield break;
							}
						}
					}
				} if ((meshFilter = gameObject.GetComponent<MeshFilter>()) != null)
				{
					Matrix4x4 transformation = gameObject.transform.localToWorldMatrix;
					for (int nVertex = 0; nVertex < sourceMesh.vertices.Length; nVertex++) {
						Vector4 v = sourceMesh.vertices [nVertex];
						v.w = 1;
						Vector3 v3World = transformation * v;

						worldVertices [nVertex] = v3World;

						if (progress != null && ((nVertex & 0xFF) == 0))
						{
							progress("Preprocessing mesh: " + strProgressDisplayObjectName, "Collecting vertex data", ((float)nVertex / (float)sourceMesh.vertices.Length));

							if (Cancelled)
							{
								CoroutineEnded = true;
								yield break;
							}
						}
					}
				}

				m_nOriginalMeshVertexCount = sourceMesh.vertexCount;//m_meshUniqueVertices.ListVertices.Count;
                m_fOriginalMeshSize = Mathf.Max(m_meshOriginal.bounds.size.x, m_meshOriginal.bounds.size.y, m_meshOriginal.bounds.size.z);

                m_heap = Heap<Vertex>.CreateMinHeap();

				for (int i = 0; i < m_nOriginalMeshVertexCount; i++)
                {
                    m_listVertexMap.Add(-1);
                    m_listVertexPermutationBack.Add(-1);
                }

                Vector2[] av2Mapping = m_meshOriginal.uv;

				AddVertices(sourceMesh.vertices, worldVertices);

//                ListIndices[] faceList = m_meshUniqueVertices.SubmeshesFaceList;
                for (int nSubMesh = 0; nSubMesh < m_meshOriginal.subMeshCount; nSubMesh++)
                {
                    int[] anIndices = m_meshOriginal.GetTriangles(nSubMesh);
//                    List<int> listIndices = faceList[nSubMesh].m_listIndices;
					m_aListTriangles[nSubMesh] = new TriangleList(anIndices.Length / 3);
                    AddFaceListSubMesh(nSubMesh, anIndices, av2Mapping);
                }

                if (Application.isEditor && !Application.isPlaying)
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

                        if (Cancelled)
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
                    Vertex mn = m_heap.ExtractTop();

                    m_listVertexPermutationBack[m_listVertices.Count - 1] = mn.m_nID;
                    m_listVertexMap[mn.m_nID] = mn.m_collapse != null ? mn.m_collapse.m_nID : -1;
                    Collapse(mn, mn.m_collapse, gameObject.transform, aRelevanceSpheres);
                }

                for (int nSubMesh = 0; nSubMesh < m_aListTriangles.Length; nSubMesh++)
                {
                    m_aListTriangles[nSubMesh].RemoveNull();
                }
                CoroutineEnded = true;
            }

            public void ComputeMeshWithVertexCount(GameObject gameObject, Mesh meshOut, int nVertices)
            {
                if (GetOriginalMeshUniqueVertexCount() == -1)
                {
                    return;
                }

                if (nVertices < 3)
                {
                    return;
                }

                if (nVertices >= GetOriginalMeshUniqueVertexCount())
                {
                    // Original vertex count requested

                    meshOut.triangles = new int[0];
                    meshOut.subMeshCount = m_meshOriginal.subMeshCount;

                    meshOut.vertices = m_meshOriginal.vertices;
                    meshOut.normals = m_meshOriginal.normals;
                    meshOut.tangents = m_meshOriginal.tangents;
                    meshOut.uv = m_meshOriginal.uv;
                    meshOut.uv2 = m_meshOriginal.uv2;
                    meshOut.colors32 = m_meshOriginal.colors32;
                    meshOut.boneWeights = m_meshOriginal.boneWeights;
                    meshOut.bindposes = m_meshOriginal.bindposes;

                    meshOut.triangles = m_meshOriginal.triangles;
                    meshOut.subMeshCount = m_meshOriginal.subMeshCount;

                    for (int nSubMesh = 0; nSubMesh < m_meshOriginal.subMeshCount; nSubMesh++)
                    {
                        meshOut.SetTriangles(m_meshOriginal.GetTriangles(nSubMesh), nSubMesh);
                    }

                    meshOut.name = gameObject.name + " simplified mesh";

                    return;
                }

                m_listVertices = new List<Vertex>();
                m_aListTriangles = new TriangleList[m_meshOriginal.subMeshCount];
#if false
                List<Vertex> listVertices = new List<Vertex>();
#endif
                Profiler.BeginSample("AddVertices");
				AddVerticesRuntime(m_meshOriginal.vertices);

                for (int i = 0; i < m_listVertices.Count; i++)
                {
                    m_listVertices[i].m_collapse = (m_listVertexMap[i] == -1) ? null : m_listVertices[m_listVertexMap[i]];
                    Vertex v = m_listVertices[m_listVertexPermutationBack[i]];
#if false
                    listVertices.Add(v);
#endif
                    v.m_bRuntimeCollapsed = i >= nVertices;
                }
                Profiler.EndSample();
                Profiler.BeginSample("AddFaceListSubMesh");
                Vector2[] av2Mapping = m_meshOriginal.uv;

                for (int nSubMesh = 0; nSubMesh < m_meshOriginal.subMeshCount; nSubMesh++)
                {
                    int[] anIndices = m_meshOriginal.GetTriangles(nSubMesh);
					m_aListTriangles[nSubMesh] = new TriangleList(anIndices.Length / 3);
                    AddFaceListSubMeshRuntime(nSubMesh, anIndices, av2Mapping);
                }
                Profiler.EndSample();

                //int nTotalVertices = listVertices.Count;
#if false
                Profiler.BeginSample("Collapse");
                //Stopwatch sw = Stopwatch.StartNew();
                while (listVertices.Count > nVertices)
                {
                    //if (progress != null)
                    //{
                    //    float fT = 1.0f;
                    //    if (nTotalVertices != nVertices && ((listVertices.Count & 0xFF) == 0))
                    //    {
                    //        fT = 1.0f - ((float)(listVertices.Count - nVertices) / (float)(nTotalVertices - nVertices));
                    //        progress("Simplifying mesh: " + strProgressDisplayObjectName, "Collapsing edges", fT);

                    //        if (Cancelled)
                    //        {
                    //            CoroutineEnded = true;
                    //            yield break;
                    //        }
                    //    }
                    //}

                    Vertex mn = listVertices[listVertices.Count - 1];
                    listVertices.RemoveAt(listVertices.Count - 1);
                    CollapseRuntime(mn, mn.m_collapse);
                    //m_listVertices.Remove(mn);

                    //if (sw.ElapsedMilliseconds > CoroutineFrameMiliseconds && CoroutineFrameMiliseconds > 0)
                    //{
                    //    yield return null;
                    //    sw = Stopwatch.StartNew();
                    //}
                }
                Profiler.EndSample();
#endif

                for (int nSubMesh = 0; nSubMesh < m_aListTriangles.Length; nSubMesh++)
                {
                    m_aListTriangles[nSubMesh].RemoveNull();
                }
                //Vector3[] av3Vertices = new Vector3[m_listVertices.Count];
                //for (int i = 0; i < m_listVertices.Count; i++)
                //{
                //    m_listVertices[i].m_nID = i;  // reassign id's
                //    av3Vertices[i] = m_listVertices[i].m_v3Position;
                //}

                Profiler.BeginSample("ConsolidateMesh");
                ConsolidateMesh(gameObject, m_meshOriginal, meshOut, m_aListTriangles);
                Profiler.EndSample();
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

            void ConsolidateMesh(GameObject gameObject, Mesh meshIn, Mesh meshOut, TriangleList[] aListTriangles)
            {
                Vector3[] av3Vertices = meshIn.vertices;
                Vector3[] av3NormalsIn = meshIn.normals;
                Vector4[] av4TangentsIn = meshIn.tangents;
                Vector2[] av2Mapping1In = meshIn.uv;
                Vector2[] av2Mapping2In = meshIn.uv2;
                Color[] acolColorsIn = meshIn.colors;
                Color32[] aColors32In = meshIn.colors32;
                BoneWeight[] aBoneWeights = meshIn.boneWeights;

                bool bUV1 = av2Mapping1In != null && av2Mapping1In.Length > 0;
                bool bUV2 = av2Mapping2In != null && av2Mapping2In.Length > 0;
                bool bNormal = av3NormalsIn != null && av3NormalsIn.Length > 0;
                bool bTangent = av4TangentsIn != null && av4TangentsIn.Length > 0;
                bool bColor = (acolColorsIn != null && acolColorsIn.Length > 0) || (aColors32In != null && aColors32In.Length > 0);
                bool bBone = aBoneWeights != null && aBoneWeights.Length > 0;

                List<List<int>> listlistIndicesOut = new List<List<int>>();
                List<Vector3> listVerticesOut = new List<Vector3>();
                List<Vector3> listNormalsOut = bNormal ? new List<Vector3>() : null;
                List<Vector4> listTangentsOut = bTangent ? new List<Vector4>() : null;
                List<Vector2> listMapping1Out = bUV1 ? new List<Vector2>() : null;
                List<Vector2> listMapping2Out = bUV2 ? new List<Vector2>() : null;
                List<Color32> listColors32Out = bColor ? new List<Color32>() : null;
                List<BoneWeight> listBoneWeightsOut = bBone ? new List<BoneWeight>() : null;
#if true
                int[] map = new int[av3Vertices.Length];
                int[] map2 = new int[av3Vertices.Length];
                for (int i = 0, imax = map.Length; i < imax; i++)
                {
                    map[i] = -1;
                    map2[i] = -1;
                }
                for (int nSubMesh = 0; nSubMesh < aListTriangles.Length; nSubMesh++)
                {
                    List<int> listIndicesOut = new List<int>();
                    List<Triangle> tl = aListTriangles[nSubMesh].m_listTriangles;
                    for (int i = 0; i < tl.Count; i++)
                    {
                        Triangle t = tl[i];
                        for (int v = 0; v < 3; v++)
                        {
                            int vid = t.Vertices[v].m_nID;
                            int uvi = t.IndicesUV[v];
                            if (map[vid] != -1 && map2[uvi] == map[vid])
                            {
                                listIndicesOut.Add(map[vid]);
                                continue;
                            }
                            int newVal = listVerticesOut.Count;
                            int vi = t.Indices[v];

                            listVerticesOut.Add(t.Vertices[v].m_v3Position);
                            if (bNormal) listNormalsOut.Add(av3NormalsIn[vi]);
                            if (bUV1) listMapping1Out.Add(av2Mapping1In[uvi]);
                            if (bUV2) listMapping2Out.Add(av2Mapping2In[vi]);
                            if (bTangent) listTangentsOut.Add(av4TangentsIn[vi]);
                            if (bColor)
                            {
                                Color32 color32 = new Color32(0, 0, 0, 0);

                                if (acolColorsIn != null && acolColorsIn.Length > 0)
                                {
                                    color32 = acolColorsIn[vi];
                                }
                                else if (aColors32In != null && aColors32In.Length > 0)
                                {
                                    color32 = aColors32In[vi];
                                }
                                listColors32Out.Add(color32);
                            }

							if (bBone) listBoneWeightsOut.Add(aBoneWeights[vi]);

                            listIndicesOut.Add(newVal);
                            map[vid] = newVal;
                            map2[uvi] = newVal;
                        }
                    }
                    listlistIndicesOut.Add(listIndicesOut);
                }
#else
                Dictionary<VertexDataHash, int> dicVertexDataHash2Index = new Dictionary<VertexDataHash, int>(new VertexDataHashComparer());

                //Stopwatch sw = Stopwatch.StartNew();

                for (int nSubMesh = 0; nSubMesh < aListTriangles.Length; nSubMesh++)
                {
                    List<int> listIndicesOut = new List<int>();

                    //string strMesh = aListTriangles.Length > 1 ? ("Consolidating submesh " + (nSubMesh + 1)) : "Consolidating mesh";

                    for (int i = 0; i < aListTriangles[nSubMesh].m_listTriangles.Count; i++)
                    {
                        //if (progress != null && ((i & 0xFF) == 0))
                        //{
                        //    float fT = aListTriangles[nSubMesh].m_listTriangles.Count == 1 ? 1.0f : ((float)i / (float)(aListTriangles[nSubMesh].m_listTriangles.Count - 1));

                        //    progress("Simplifying mesh: " + strProgressDisplayObjectName, strMesh, fT);

                        //    if (Cancelled)
                        //    {
                        //        yield break;
                        //    }
                        //}

                        //if (sw.ElapsedMilliseconds > CoroutineFrameMiliseconds && CoroutineFrameMiliseconds > 0)
                        //{
                        //    yield return null;
                        //    sw = Stopwatch.StartNew();
                        //}

                        for (int v = 0; v < 3; v++)
                        {
                            int nMappingIndex = aListTriangles[nSubMesh].m_listTriangles[i].IndicesUV[v];
                            int nVertexIndex = aListTriangles[nSubMesh].m_listTriangles[i].Indices[v];
                            Vector3 v3Vertex = aListTriangles[nSubMesh].m_listTriangles[i].Vertices[v].m_v3Position;
                            Vector3 v3Normal = bNormal ? av3NormalsIn[nVertexIndex] : Vector3.zero;
                            Vector4 v4Tangent = bTangent ? av4TangentsIn[nVertexIndex] : Vector4.zero;
                            Vector2 uv1 = bUV1 ? av2Mapping1In[nMappingIndex] : Vector2.zero;
                            Vector2 uv2 = bUV2 ? av2Mapping2In[nVertexIndex] : Vector2.zero;
                            Color32 color32 = new Color32(0, 0, 0, 0);

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

                                if (bNormal) listNormalsOut.Add(v3Normal);
                                if (bUV1) listMapping1Out.Add(uv1);
                                if (bUV2) listMapping2Out.Add(uv2);
                                if (bTangent) listTangentsOut.Add(v4Tangent);
                                if (bColor) listColors32Out.Add(color32);

                                if (bBone) listBoneWeightsOut.Add(aListTriangles[nSubMesh].m_listTriangles[i].Vertices[v].m_boneWeight);

                                listIndicesOut.Add(listVerticesOut.Count - 1);
                            }
                        }
                    }

                    listlistIndicesOut.Add(listIndicesOut);
                }
#endif

                meshOut.triangles = null;
                meshOut.vertices = listVerticesOut.ToArray();
                meshOut.normals = bNormal ? listNormalsOut.ToArray() : null;
                meshOut.tangents = bTangent ? listTangentsOut.ToArray() : null;
                meshOut.uv = bUV1 ? listMapping1Out.ToArray() : null;
                meshOut.uv2 = bUV2 ? listMapping2Out.ToArray() : null;
                meshOut.colors32 = bColor ? listColors32Out.ToArray() : null;
                meshOut.boneWeights = bBone ? listBoneWeightsOut.ToArray() : null;
                meshOut.bindposes = meshIn.bindposes;
                meshOut.subMeshCount = listlistIndicesOut.Count;

                for (int nSubMesh = 0; nSubMesh < listlistIndicesOut.Count; nSubMesh++)
                {
                    meshOut.SetTriangles(listlistIndicesOut[nSubMesh].ToArray(), nSubMesh);
                }

                meshOut.name = gameObject.name + " simplified mesh";
            }

            float ComputeEdgeCollapseCost(Vertex u, Vertex v, float fRelevanceBias)
            {
                bool bUseEdgeLength = m_bUseEdgeLength;
                bool bUseCurvature = m_bUseCurvature;
                bool bProtectTexture = m_bProtectTexture;
                bool bLockBorder = m_bLockBorder;

                int i;
                float fEdgeLength = bUseEdgeLength ? (Vector3.Magnitude(v.m_v3Position - u.m_v3Position) / m_fOriginalMeshSize) : 1.0f;
                float fCurvature = 0.001f;

                List<Triangle> sides = new List<Triangle>();

                for (i = 0; i < u.m_listFaces.Count; i++)
                {
                    if (u.m_listFaces[i].HasVertex(v))
                    {
                        sides.Add(u.m_listFaces[i]);
                    }
                }

                if (bUseCurvature)
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
                        Matrix4x4 mtxSphere = Matrix4x4.TRS(aRelevanceSpheres[nSphere].m_v3Position, aRelevanceSpheres[nSphere].m_q4Rotation, aRelevanceSpheres[nSphere].m_v3Scale);

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

                    ComputeEdgeCostAtVertex(m_listVertices[i], transform, aRelevanceSpheres);
                    m_heap.Insert(m_listVertices[i]);
                }
            }

            List<Triangle> tmpTriangles = new List<Triangle>(); 
            List<Vertex> tmpVertices = new List<Vertex>(); 


            void Collapse(Vertex u, Vertex v, Transform transform, RelevanceSphere[] aRelevanceSpheres)
            {
                if (v == null)
                {
                    m_listVertices.Remove(u);
                    u.Destructor();
                    return;
                }

                int i;
                tmpVertices.Clear();

                for (i = 0; i < u.m_listNeighbors.Count; i++)
                {
					Vertex nb = u.m_listNeighbors [i];
					if (nb != u) {
						tmpVertices.Add(nb);
					}
                }

                tmpTriangles.Clear();

                for (i = 0; i < u.m_listFaces.Count; i++)
                {
                    if (u.m_listFaces[i].HasVertex(v))
                    {
                        tmpTriangles.Add(u.m_listFaces[i]);
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
                        for (j = 0; j < tmpTriangles.Count; j++)
                        {
                            if (u.m_listFaces[i].TexAt(u) == tmpTriangles[j].TexAt(u))
                            {
                                u.m_listFaces[i].SetTexAt(u, tmpTriangles[j].TexAt(v));
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

                for (i = tmpTriangles.Count - 1; i >= 0; i--)
                {
                    Triangle t = tmpTriangles[i];
                    m_aListTriangles[t.SubMeshIndex].m_listTriangles[t.Index] = null;
                    t.Destructor();
                }

                // Update remaining triangles to have v instead of u

                for (i = u.m_listFaces.Count - 1; i >= 0; i--)
                {
                    u.m_listFaces[i].ReplaceVertex(u, v);
                }
                m_listVertices.Remove(u);
                u.Destructor();

                // Recompute the edge collapse costs for neighboring vertices

                for (i = 0; i < tmpVertices.Count; i++)
                {
                    ComputeEdgeCostAtVertex(tmpVertices[i], transform, aRelevanceSpheres);
                    m_heap.ModifyValue(tmpVertices[i].HeapIndex, tmpVertices[i]);
                }
            }
            void CollapseRuntime(Vertex u, Vertex v)
            {
                if (v == null)
                {
                    return;
                }

                int i;

                tmpTriangles.Clear();

                for (i = 0; i < u.m_listFaces.Count; i++)
                {
                    Triangle t = u.m_listFaces[i];
                    if (t.DestructedRuntime)
                    {
                        continue;
                    }
                    if (t.HasVertex(v))
                    {
                        tmpTriangles.Add(t);
                    }
                }

                // update texture mapping

                for (i = 0; i < u.m_listFaces.Count; i++)
                {
                    Triangle t = u.m_listFaces[i];
                    if (t.DestructedRuntime)
                    {
                        continue;
                    }
                    if (t.HasVertex(v))
                    {
                        continue;
                    }

                    int j;
                    if (t.HasUVData)
                    {
                        for (j = 0; j < tmpTriangles.Count; j++)
                        {
                            if (t.TexAt(u) == tmpTriangles[j].TexAt(u))
                            {
                                t.SetTexAt(u, tmpTriangles[j].TexAt(v));
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

                for (i = tmpTriangles.Count - 1; i >= 0; i--)
                {
                    Triangle t = tmpTriangles[i];
                    m_aListTriangles[t.SubMeshIndex].m_listTriangles[t.Index] = null;
                    //t.DestructorRuntime();
                    t.DestructedRuntime = true;
                }

                // Update remaining triangles to have v instead of u

                for (i = u.m_listFaces.Count - 1; i >= 0; i--)
                {
                    Triangle t = u.m_listFaces[i];
                    if (t.DestructedRuntime)
                    {
                        continue;
                    }
                    t.ReplaceVertexRuntime(u, v);
                }
            }

            void AddVertices(Vector3[] listVertices, Vector3[] listVerticesWorld)
            {
				for (int i = 0; i < listVertices.Length; i++)
                {
                    Vertex v = new Vertex(listVertices[i], listVerticesWorld[i], i);
                    m_listVertices.Add(v);
                }
            }
			void AddVerticesRuntime(Vector3[] listVertices)
            {
				for (int i = 0; i < listVertices.Length; i++)
                {
                    Vertex v = new Vertex(listVertices[i], Vector3.zero, i);
                    m_listVertices.Add(v);
                }
            }

            void AddFaceListSubMesh(int nSubMesh, int[] anIndices, Vector2[] v2Mapping)
            {
                bool bUVData = false;

                if (v2Mapping != null)
                {
                    if (v2Mapping.Length > 0)
                    {
                        bUVData = true;
                    }
                }

                List<Triangle> list = m_aListTriangles[nSubMesh].m_listTriangles;
				for (int i = 0; i < anIndices.Length; i += 3)
                {
                    Triangle tri = new Triangle(nSubMesh, list.Count,
						m_listVertices[anIndices[i]], m_listVertices[anIndices[i + 1]], m_listVertices[anIndices[i + 2]],
                                                bUVData, anIndices[i], anIndices[i + 1], anIndices[i + 2], true);


                    list.Add(tri);
                    ShareUV(v2Mapping, tri);
                }
            }
            void AddFaceListSubMeshRuntime(int nSubMesh, int[] anIndices, Vector2[] v2Mapping)
            {
                bool bUVData = false;

                if (v2Mapping != null)
                {
                    if (v2Mapping.Length > 0)
                    {
                        bUVData = true;
                    }
                }

                int[] inverseMap = new int[m_listVertices.Count];
				for (int i = 0; i < anIndices.Length; i++)
                {
					inverseMap[anIndices[i]] = i;
                }

                List<Triangle> list = m_aListTriangles[nSubMesh].m_listTriangles;
				for (int i = 0; i < anIndices.Length; i+=3)
                {
					Vertex v0 = m_listVertices[anIndices[i]];
					Vertex v1 = m_listVertices[anIndices[i + 1]];
					Vertex v2 = m_listVertices[anIndices[i + 2]];
                    Triangle tri = Triangle.CreateTriangle(nSubMesh, list.Count, v0 , v1, v2,
                                                bUVData, anIndices[i], anIndices[i + 1], anIndices[i + 2]);
                    if (null != tri)
                    {
                        Vertex p0 = tri.Vertices[0];
                        if (p0 != v0)
                        {
                            int idx = inverseMap[p0.m_nID];
                            if (idx > 0)
                            {
                                int uv = anIndices[idx];
                                tri.SetTexAt(0, uv);
                            }
                        }
                        Vertex p1 = tri.Vertices[1];
                        if (p1 != v1)
                        {
                            int idx = inverseMap[p1.m_nID];
                            if (idx > 0)
                            {
                                int uv = anIndices[idx];
                                tri.SetTexAt(1, uv);
                            }
                        }
                        Vertex p2 = tri.Vertices[2];
                        if (p2 != v2)
                        {
                            int idx = inverseMap[p2.m_nID];
                            if (idx > 0)
                            {
                                int uv = anIndices[idx];
                                tri.SetTexAt(2, uv);
                            }
                        }
                        list.Add(tri);
                    }
                    // NOTE: if need share uv at runtime
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

                        int tx1 = t.TexAt(nCurrentVert);
                        int tx2 = n.TexAt(t.Vertices[nCurrentVert]);

                        if (tx1 == tx2)
                        {
                            continue;
                        }

                        Vector2 uv1 = aMapping[tx1];
                        Vector2 uv2 = aMapping[tx2];

                        if (uv1 == uv2)
                        {
                            t.SetTexAt(nCurrentVert, tx2);
                        }
                    }
                }
            }

#endregion Private methods

#region Private vars

            /////////////////////////////////////////////////////////////////////////////////////////////////
            // Private vars
            /////////////////////////////////////////////////////////////////////////////////////////////////

            private static int m_nCoroutineFrameMiliseconds = 0;
            private const float MAX_VERTEX_COLLAPSE_COST = 10000000.0f;

            public List<Vertex> m_listVertices;
            private Heap<Vertex> m_heap;
            public TriangleList[] m_aListTriangles;
            [SerializeField, HideInInspector] private int m_nOriginalMeshVertexCount = -1;
            [SerializeField, HideInInspector] private float m_fOriginalMeshSize = 1.0f;
            [SerializeField, HideInInspector] private List<int> m_listVertexMap;
            [SerializeField, HideInInspector] private List<int> m_listVertexPermutationBack;
//            [SerializeField, HideInInspector] private MeshUniqueVertices m_meshUniqueVertices;
            [SerializeField, HideInInspector] private Mesh m_meshOriginal;
            [SerializeField, HideInInspector] private bool m_bUseEdgeLength = true;
            [SerializeField, HideInInspector] bool m_bUseCurvature = true, m_bProtectTexture = true, m_bLockBorder = true;


#endregion // Private vars
        }
    }
}