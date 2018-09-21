using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Chaos
{

    public class Simplifier : MonoBehaviour
    {
        unsafe struct MappingLinkedNode
        {
            public MappingLinkedNode *Next;
            public int Mapping;
        }
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

        public bool UseEdgeLength
        {
            get { return m_bUseEdgeLength; }
            set { m_bUseEdgeLength = value; }
        }

        public bool UseCurvature
        {
            get { return m_bUseCurvature; }
            set { m_bUseCurvature = value; }
        }

        public bool ProtectTexture
        {
            get { return m_bProtectTexture; }
            set { m_bProtectTexture = value; }
        }

        public bool LockBorder
        {
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
            int vertexCount = sourceMesh.vertexCount;
            m_aVertexMap = new int[vertexCount];
            m_aVertexPermutation = new int[vertexCount];
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
            MeshFilter filter;
            if ((skin = gameObject.GetComponent<SkinnedMeshRenderer>()) != null)
            {
#if UNITY_2018_1_OR_NEWER
                LocalToWorldTransformation.Transform(skin, worldVertices);
#else
                    BoneWeight[] aBoneWeights = sourceMesh.boneWeights;
					Matrix4x4[] aBindPoses = sourceMesh.bindposes;
					Transform[] aBones = skin.bones;

					for (int nVertex = 0; nVertex < sourceMesh.vertices.Length; nVertex++) {
						BoneWeight bw = aBoneWeights[nVertex];
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
#endif
            }
            if ((filter = gameObject.GetComponent<MeshFilter>()) != null)
            {
#if UNITY_2018_1_OR_NEWER
                LocalToWorldTransformation.Transform(filter, worldVertices);
#else
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
#endif
            }

            m_nOriginalMeshVertexCount = sourceMesh.vertexCount;//m_meshUniqueVertices.ListVertices.Count;
            m_fOriginalMeshSize = Mathf.Max(m_meshOriginal.bounds.size.x, m_meshOriginal.bounds.size.y, m_meshOriginal.bounds.size.z);

            m_heap = Heap<Vertex>.CreateMinHeap();

            for (int i = 0; i < m_nOriginalMeshVertexCount; i++)
            {
                m_aVertexMap[i] = -1;
                m_aVertexPermutation[i] = -1;
            }

            Vector2[] av2Mapping = m_meshOriginal.uv;

            AddVertices(sourceMesh.vertices, worldVertices);

            int nTriangles = 0;
            //                ListIndices[] faceList = m_meshUniqueVertices.SubmeshesFaceList;
            for (int nSubMesh = 0; nSubMesh < m_meshOriginal.subMeshCount; nSubMesh++)
            {
                int[] anIndices = m_meshOriginal.GetTriangles(nSubMesh);
                //                    List<int> listIndices = faceList[nSubMesh].m_listIndices;
                m_aListTriangles[nSubMesh] = new TriangleList(anIndices.Length / 3);
                nTriangles = AddFaceListSubMesh(nSubMesh, anIndices, av2Mapping, nTriangles);
            }

            if (Application.isEditor && !Application.isPlaying)
            {
#if UNITY_2018_1_OR_NEWER
                float[] costs = new float[m_listVertices.Count];
                int[] collapses = new int[m_listVertices.Count];
                CostCompution.Compute(m_listVertices, m_aListTriangles, aRelevanceSpheres, m_bUseEdgeLength, m_bUseCurvature, m_bLockBorder, m_fOriginalMeshSize, costs, collapses);

                for (int i = 0; i < m_listVertices.Count; i++)
                {
                    Vertex v = m_listVertices[i];
                    v.m_fObjDist = costs[i];
                    v.m_collapse = collapses[i] == -1 ? null : m_listVertices[collapses[i]];
                    m_heap.Insert(v);
                }
#else
                    IEnumerator enumerator = ComputeAllEdgeCollapseCosts(strProgressDisplayObjectName, gameObject.transform, aRelevanceSpheres, progress);

                    while (enumerator.MoveNext())
                    {
                        if (Simplifier.Cancelled)
                        {
                            CoroutineEnded = true;
                            yield break;
                        }
                    }
#endif
            }
            else
            {
#if UNITY_2018_1_OR_NEWER
                float[] costs = new float[m_listVertices.Count];
                int[] collapses = new int[m_listVertices.Count];
                CostCompution.Compute(m_listVertices, m_aListTriangles, aRelevanceSpheres, m_bUseEdgeLength, m_bUseCurvature, m_bLockBorder, m_fOriginalMeshSize, costs, collapses);

                for (int i = 0; i < m_listVertices.Count; i++)
                {
                    Vertex v = m_listVertices[i];
                    v.m_fObjDist = costs[i];
                    v.m_collapse = m_listVertices[collapses[i]];
                    m_heap.Insert(v);
                }
#else
                    yield return StartCoroutine(ComputeAllEdgeCollapseCosts(strProgressDisplayObjectName, gameObject.transform, aRelevanceSpheres, progress));
                
#endif
            }

            //int nVertices = m_listVertices.Count;

            //Stopwatch sw = Stopwatch.StartNew();

            int vertexNum = m_listVertices.Count;
            while (vertexNum-- > 0)
            {
                //               if (progress != null && ((vertexNum & 0xFF) == 0))
                //               {
                //                   progress("Preprocessing mesh: " + strProgressDisplayObjectName, "Collapsing edges", 1.0f - ((float)vertexNum / (float)nVertices));

                //                   if (Cancelled)
                //                   {
                //                       CoroutineEnded = true;
                //                       yield break;
                //                   }
                //               }

                //               if (sw.ElapsedMilliseconds > CoroutineFrameMiliseconds && CoroutineFrameMiliseconds > 0)
                //               {
                //                   yield return null;
                //                   sw = Stopwatch.StartNew();
                //}
                Vertex mn = m_heap.ExtractTop();

                //                    m_listVertexPermutationBack[m_listVertices.Count - 1] = mn.m_nID;
                m_aVertexPermutation[mn.m_nID] = vertexNum;
                m_aVertexMap[mn.m_nID] = mn.m_collapse != null ? mn.m_collapse.m_nID : -1;
                Collapse(mn, mn.m_collapse, gameObject.transform, aRelevanceSpheres);
            }

            //for (int nSubMesh = 0; nSubMesh < m_aListTriangles.Length; nSubMesh++)
            //{
            //    m_aListTriangles[nSubMesh].RemoveNull();
            //}
            CoroutineEnded = true;
            yield return null;
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

            _av3VerticesOriginal = _av3VerticesOriginal ?? m_meshOriginal.vertices;
            _av3NormalsOriginal = _av3NormalsOriginal ?? m_meshOriginal.normals;
            _av4TangentsOriginal = _av4TangentsOriginal ?? m_meshOriginal.tangents;
            _av2Mapping1Original = _av2Mapping1Original ?? m_meshOriginal.uv;
            _av2Mapping2Original = _av2Mapping2Original ?? m_meshOriginal.uv2;
            _aColors32Original = _aColors32Original ?? m_meshOriginal.colors32;
            _aBoneWeightsOriginal = _aBoneWeightsOriginal ?? m_meshOriginal.boneWeights;
            _aBindPoses = _aBindPoses ?? m_meshOriginal.bindposes;
            int subMeshCount = m_meshOriginal.subMeshCount;
            if (null == _aSubMeshesOriginal)
            {
                _aSubMeshesOriginal = new int[subMeshCount][];
                for (int nSubMesh = 0; nSubMesh < subMeshCount; nSubMesh++)
                {
                    _aSubMeshesOriginal[nSubMesh] = m_meshOriginal.GetTriangles(nSubMesh);
                }
            }

            if (nVertices >= GetOriginalMeshUniqueVertexCount())
            {
                // Original vertex count requested

                meshOut.triangles = new int[0];
                meshOut.subMeshCount = m_meshOriginal.subMeshCount;

                meshOut.vertices = _av3VerticesOriginal;
                meshOut.normals = _av3NormalsOriginal;
                meshOut.tangents = _av4TangentsOriginal;
                meshOut.uv = _av2Mapping1Original;
                meshOut.uv2 = _av2Mapping2Original;
                meshOut.colors32 = _aColors32Original;
                meshOut.boneWeights = _aBoneWeightsOriginal;
                meshOut.bindposes = _aBindPoses;

                meshOut.triangles = m_meshOriginal.triangles;
                meshOut.subMeshCount = m_meshOriginal.subMeshCount;

                for (int nSubMesh = 0; nSubMesh < subMeshCount; nSubMesh++)
                {
                    meshOut.SetTriangles(_aSubMeshesOriginal[nSubMesh], nSubMesh);
                }

                meshOut.name = gameObject.name + " simplified mesh";

                return;
            }

            ConsolidateMesh(gameObject, meshOut, m_aVertexPermutation, m_aVertexMap, nVertices);
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

        unsafe void ConsolidateMesh(GameObject gameObject, Mesh meshOut, int[] permutation, int[] collapseMap, int nVertices)
        {
            int subMeshCount = _aSubMeshesOriginal.Length;
            if (null == _av3Vertices) _av3Vertices = (Vector3[])_av3VerticesOriginal.Clone();
            else _av3VerticesOriginal.CopyTo(_av3Vertices, 0);
            if (null == _av3NormalsIn) _av3NormalsIn = (Vector3[])_av3NormalsOriginal.Clone();
            else _av3NormalsOriginal.CopyTo(_av3NormalsIn, 0);
            if (null == _av4TangentsIn) _av4TangentsIn = (Vector4[])_av4TangentsOriginal.Clone();
            else _av4TangentsOriginal.CopyTo(_av4TangentsIn, 0);
            if (null == _av2Mapping1In) _av2Mapping1In = (Vector2[])_av2Mapping1Original.Clone();
            else _av2Mapping1Original.CopyTo(_av2Mapping1In, 0);
            if (null == _av2Mapping2In) _av2Mapping2In = (Vector2[])_av2Mapping2Original.Clone();
            else _av2Mapping2Original.CopyTo(_av2Mapping2In, 0);
            if (null == _aColors32In) _aColors32In = (Color32[])_aColors32Original.Clone();
            else _aColors32Original.CopyTo(_aColors32In, 0);
            if (null == _aBoneWeights) _aBoneWeights = (BoneWeight[])_aBoneWeightsOriginal.Clone();
            else _aBoneWeightsOriginal.CopyTo(_aBoneWeights, 0);
            if (null == _aSubMeshes) _aSubMeshes = new int[subMeshCount][];
            if (null == _aTriangleCount) _aTriangleCount = new int[subMeshCount];

            bool bUV1 = _av2Mapping1In != null && _av2Mapping1In.Length > 0;
            bool bUV2 = _av2Mapping2In != null && _av2Mapping2In.Length > 0;
            bool bNormal = _av3NormalsIn != null && _av3NormalsIn.Length > 0;
            bool bTangent = _av4TangentsIn != null && _av4TangentsIn.Length > 0;
            bool bColor32 = (_aColors32In != null && _aColors32In.Length > 0);
            bool bBone = _aBoneWeights != null && _aBoneWeights.Length > 0;

            _vertexMap = _vertexMap ?? new int[_av3Vertices.Length];
            for (int i = 0, imax = _vertexMap.Length; i < imax; i++)
            {
                _vertexMap[i] = -1;
            }
            int n = 0;
            for (int nSubMesh = 0; nSubMesh < subMeshCount; nSubMesh++)
            {
                if (null == _aSubMeshes[nSubMesh]) _aSubMeshes[nSubMesh] = (int[])_aSubMeshesOriginal[nSubMesh].Clone();
                else _aSubMeshesOriginal[nSubMesh].CopyTo(_aSubMeshes[nSubMesh], 0);
                int[] triangles = _aSubMeshes[nSubMesh];
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int idx0 = triangles[i];
                    int idx1 = triangles[i + 1];
                    int idx2 = triangles[i + 2];
                    while (permutation[idx0] >= nVertices)
                    {
                        int idx = collapseMap[idx0];
                        if (idx == -1 || idx1 == idx || idx2 == idx)
                        {
                            idx0 = -1;
                            break;
                        }
                        idx0 = idx;
                    }
                    while (permutation[idx1] >= nVertices)
                    {
                        int idx = collapseMap[idx1];
                        if (idx == -1 || idx0 == idx || idx2 == idx)
                        {
                            idx1 = -1;
                            break;
                        }
                        idx1 = idx;
                    }
                    while (permutation[idx2] >= nVertices)
                    {
                        int idx = collapseMap[idx2];
                        if (idx == -1 || idx1 == idx || idx0 == idx)
                        {
                            idx2 = -1;
                            break;
                        }
                        idx2 = idx;
                    }
                    if (idx0 == -1 || idx1 == -1 || idx2 == -1)
                    {
                        triangles[i] = -1;
                        triangles[i + 1] = -1;
                        triangles[i + 2] = -1;
                        continue;
                    }
                    if (_vertexMap[idx0] == -1)
                    {
                        _vertexMap[idx0] = n++;
                    }
                    triangles[i] = _vertexMap[idx0];
                    if (_vertexMap[idx1] == -1)
                    {
                        _vertexMap[idx1] = n++;
                    }
                    triangles[i + 1] = _vertexMap[idx1];
                    if (_vertexMap[idx2] == -1)
                    {
                        _vertexMap[idx2] = n++;
                    }
                    triangles[i + 2] = _vertexMap[idx2];
                }
                int l = triangles.Length;
                int h = 0;
                int t = l - 1;
                while (h < t)
                {
                    if (triangles[t] == -1)
                    {
                        t -= 3;
                        continue;
                    }
                    if (triangles[h] != -1)
                    {
                        h += 3;
                        continue;
                    }
                    triangles[h] = triangles[t - 2];
                    triangles[h + 1] = triangles[t - 1];
                    triangles[h + 2] = triangles[t];
                    triangles[t - 2] = -1;
                    triangles[t - 1] = -1;
                    triangles[t] = -1;
                    h += 3;
                    t -= 3;
                }
                if (t < l - 1)
                {
                    _aTriangleCount[nSubMesh] = t + 1;
                    //#if DEBUG
                    //                        if (t >= 0 && triangles[t] == -1)
                    //                        {
                    //                            throw new Exception("triangles[t] == -1");
                    //                        }
                    //#endif
                }
                else
                {
                    _aTriangleCount[nSubMesh] = l;
                }
            }
            NativeArray<MappingLinkedNode> headArray = new NativeArray<MappingLinkedNode>(_vertexMap.Length, Allocator.TempJob);
            int headCount = 0;
            int nodeSize = UnsafeUtility.AlignOf<MappingLinkedNode>();
            for (int i = 0; i < _vertexMap.Length; i++)
            {
                int idx = i;
                MappingLinkedNode *head = (MappingLinkedNode *) UnsafeUtility.Malloc(1, nodeSize, Allocator.TempJob);
                head->Next = null;
                head->Mapping = i;
                MappingLinkedNode *node = head;
                while (_vertexMap[idx] != -1)
                {
                    MappingLinkedNode *next = (MappingLinkedNode *)UnsafeUtility.Malloc(1, nodeSize, Allocator.TempJob);
                    next->Next = null;
                    next->Mapping = _vertexMap[idx];
                    node->Next = next;
                    int tmpI = _vertexMap[idx];
                    _vertexMap[idx] = -1;
                    idx = tmpI;
                    node = next;
                }
                if (head->Next != null)
                {
                    headArray[headCount++] = *head;
                }
            }
            Vector2 tmpUV = Vector2.zero;
            Vector2 tmpUV2 = Vector2.zero;
            Vector3 tmpNormal = Vector3.zero;
            Vector4 tmpTangent = Vector4.zero;
            Color32 tmpColor = Color.black;
            BoneWeight tmpBoneWeight = new BoneWeight();
            Vector2 tmpUV_ = Vector2.zero;
            Vector2 tmpUV2_ = Vector2.zero;
            Vector3 tmpNormal_ = Vector3.zero;
            Vector4 tmpTangent_ = Vector4.zero;
            Color32 tmpColor_ = Color.black;
            BoneWeight tmpBoneWeight_ = new BoneWeight();
            for (int i = 0; i < headCount; i++)
            {
                MappingLinkedNode head = headArray[i];
                MappingLinkedNode *node = &head;

                int idx = node->Mapping;
                Vector3 tmp = _av3Vertices[idx];
                if (bUV1) tmpUV = _av2Mapping1In[idx];
                if (bUV2) tmpUV2 = _av2Mapping2In[idx];
                if (bNormal) tmpNormal = _av3NormalsIn[idx];
                if (bTangent) tmpTangent = _av4TangentsIn[idx];
                if (bColor32) tmpColor = _aColors32In[idx];
                if (bBone) tmpBoneWeight = _aBoneWeights[idx];
                node = node->Next;
                while (node != null)
                {
                    int vidx = node->Mapping;
                    Vector3 tmp_ = _av3Vertices[vidx];
                    if (bUV1) tmpUV_ = _av2Mapping1In[vidx];
                    if (bUV2) tmpUV2_ = _av2Mapping2In[vidx];
                    if (bNormal) tmpNormal_ = _av3NormalsIn[vidx];
                    if (bTangent) tmpTangent_ = _av4TangentsIn[vidx];
                    if (bColor32) tmpColor_ = _aColors32In[vidx];
                    if (bBone) tmpBoneWeight_ = _aBoneWeights[vidx];
                    _av3Vertices[vidx] = tmp;
                    if (bUV1) _av2Mapping1In[vidx] = tmpUV;
                    if (bUV2) _av2Mapping2In[vidx] = tmpUV2;
                    if (bNormal) _av3NormalsIn[vidx] = tmpNormal;
                    if (bTangent) _av4TangentsIn[vidx] = tmpTangent;
                    if (bColor32) _aColors32In[vidx] = tmpColor;
                    if (bBone) _aBoneWeights[vidx] = tmpBoneWeight;
                    tmp = tmp_;
                    tmpUV = tmpUV_;
                    tmpUV2 = tmpUV2_;
                    tmpNormal = tmpNormal_;
                    tmpTangent = tmpTangent_;
                    tmpColor = tmpColor_;
                    tmpBoneWeight = tmpBoneWeight_;
                    MappingLinkedNode* oldNode = node;
                    node = node->Next;
                    UnsafeUtility.Free(oldNode, Allocator.TempJob);
                }
            }
            headArray.Dispose();
            //for (int i = 0; i < _vertexMap.Length; i++)
            //{
            //    int idx = i;
            //    Vector3 tmp = _av3Vertices[idx];
            //    if (bUV1) tmpUV = _av2Mapping1In[idx];
            //    if (bUV2) tmpUV2 = _av2Mapping2In[idx];
            //    if (bNormal) tmpNormal = _av3NormalsIn[idx];
            //    if (bTangent) tmpTangent = _av4TangentsIn[idx];
            //    if (bColor32) tmpColor = _aColors32In[idx];
            //    if (bBone) tmpBoneWeight = _aBoneWeights[idx];
            //    while (_vertexMap[idx] != -1)
            //    {
            //        Vector3 tmp_ = _av3Vertices[_vertexMap[idx]];
            //        if (bUV1) tmpUV_ = _av2Mapping1In[_vertexMap[idx]];
            //        if (bUV2) tmpUV2_ = _av2Mapping2In[_vertexMap[idx]];
            //        if (bNormal) tmpNormal_ = _av3NormalsIn[_vertexMap[idx]];
            //        if (bTangent) tmpTangent_ = _av4TangentsIn[_vertexMap[idx]];
            //        if (bColor32) tmpColor_ = _aColors32In[_vertexMap[idx]];
            //        if (bBone) tmpBoneWeight_ = _aBoneWeights[_vertexMap[idx]];
            //        _av3Vertices[_vertexMap[idx]] = tmp;
            //        if (bUV1) _av2Mapping1In[_vertexMap[idx]] = tmpUV;
            //        if (bUV2) _av2Mapping2In[_vertexMap[idx]] = tmpUV2;
            //        if (bNormal) _av3NormalsIn[_vertexMap[idx]] = tmpNormal;
            //        if (bTangent) _av4TangentsIn[_vertexMap[idx]] = tmpTangent;
            //        if (bColor32) _aColors32In[_vertexMap[idx]] = tmpColor;
            //        if (bBone) _aBoneWeights[_vertexMap[idx]] = tmpBoneWeight;
            //        tmp = tmp_;
            //        tmpUV = tmpUV_;
            //        tmpUV2 = tmpUV2_;
            //        tmpNormal = tmpNormal_;
            //        tmpTangent = tmpTangent_;
            //        tmpColor = tmpColor_;
            //        tmpBoneWeight = tmpBoneWeight_;
            //        int tmpI = _vertexMap[idx];
            //        _vertexMap[idx] = -1;
            //        idx = tmpI;
            //    }
            //}
            //#if DEBUG
            //                // Check
            //                for (int i = 0; i < n; i++)
            //                {
            //                    if (_vertexMap[i] != -1)
            //                    {
            //                        throw new Exception("");
            //                    }
            //                }
            //#endif

            this._meshOut = meshOut;

            _assignVertices = _assignVertices ?? (arr => this._meshOut.vertices = arr);
            if (bNormal) _assignNormals = _assignNormals ?? (arr => this._meshOut.normals = arr);
            if (bTangent) _assignTangents = _assignTangents ?? (arr => this._meshOut.tangents = arr);
            if (bUV1) _assignUV = _assignUV ?? (arr => this._meshOut.uv = arr);
            if (bUV2) _assignUV2 = _assignUV2 ?? (arr => this._meshOut.uv2 = arr);
            if (bColor32) _assignColor32 = _assignColor32 ?? (arr => this._meshOut.colors32 = arr);
            if (bBone) _assignBoneWeights = _assignBoneWeights ?? (arr => this._meshOut.boneWeights = arr);
            if (null == _setTriangles)
            {
                _setTriangles = new Action<int[]>[subMeshCount];
                for (int i = 0; i < subMeshCount; i++)
                {
                    int idx = i;
                    _setTriangles[i] = (arr => this._meshOut.SetTriangles(arr, idx));
                }
            }

            meshOut.triangles = null;
            // NOTE: 禁术
            UnsafeUtil.Vector3HackArraySizeCall(_av3Vertices, n, _assignVertices);
            if (bNormal) UnsafeUtil.Vector3HackArraySizeCall(_av3NormalsIn, n, _assignNormals);
            if (bTangent) UnsafeUtil.Vector4HackArraySizeCall(_av4TangentsIn, n, _assignTangents);
            if (bUV1) UnsafeUtil.Vector2HackArraySizeCall(_av2Mapping1In, n, _assignUV);
            if (bUV2) UnsafeUtil.Vector2HackArraySizeCall(_av2Mapping2In, n, _assignUV2);
            if (bColor32) UnsafeUtil.Color32HackArraySizeCall(_aColors32In, n, _assignColor32);
            if (bBone) UnsafeUtil.BoneWeightHackArraySizeCall(_aBoneWeights, n, _assignBoneWeights);
            if (bBone) meshOut.bindposes = _aBindPoses;
            meshOut.subMeshCount = _aSubMeshes.Length;

            for (int i = 0; i < subMeshCount; i++)
            {
                UnsafeUtil.IntegerHackArraySizeCall(_aSubMeshes[i], _aTriangleCount[i], _setTriangles[i]);
            }
            meshOut.UploadMeshData(false);
            //meshOut.name = gameObject.name + " simplified mesh";

        }

        float ComputeEdgeCollapseCost(Vertex u, Vertex v, float fRelevanceBias)
        {
            bool bUseEdgeLength = m_bUseEdgeLength;
            bool bUseCurvature = m_bUseCurvature;
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
            bool isBorder = u.IsBorder();
            if (isBorder && sides.Count > 1)
            {
                fCurvature = 1.0f;
            }

            if (bLockBorder && isBorder)
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
                //m_listVertices.Remove(u);
                u.Destructor();
                return;
            }

            int i;
            tmpVertices.Clear();

            for (i = 0; i < u.m_listNeighbors.Count; i++)
            {
                Vertex nb = u.m_listNeighbors[i];
                if (nb != u)
                {
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

            // Delete triangles on edge uv

            for (i = tmpTriangles.Count - 1; i >= 0; i--)
            {
                Triangle t = tmpTriangles[i];
                //m_aListTriangles[t.SubMeshIndex].m_listTriangles[t.Index] = null;
                t.Destructor();
            }

            // Update remaining triangles to have v instead of u

            for (i = u.m_listFaces.Count - 1; i >= 0; i--)
            {
                u.m_listFaces[i].ReplaceVertex(u, v);
            }
            //m_listVertices.Remove(u);
            u.Destructor();

            // Recompute the edge collapse costs for neighboring vertices

            for (i = 0; i < tmpVertices.Count; i++)
            {
                ComputeEdgeCostAtVertex(tmpVertices[i], transform, aRelevanceSpheres);
                m_heap.ModifyValue(tmpVertices[i].HeapIndex, tmpVertices[i]);
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

        int AddFaceListSubMesh(int nSubMesh, int[] anIndices, Vector2[] v2Mapping, int nTriangles)
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
                Triangle tri = new Triangle(nSubMesh, nTriangles + list.Count,
                    m_listVertices[anIndices[i]], m_listVertices[anIndices[i + 1]], m_listVertices[anIndices[i + 2]],
                                            bUVData, anIndices[i], anIndices[i + 1], anIndices[i + 2], true);


                list.Add(tri);
                ShareUV(v2Mapping, tri);
            }
            return nTriangles + list.Count;
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
        [SerializeField, HideInInspector]
        private int m_nOriginalMeshVertexCount = -1;
        [SerializeField, HideInInspector]
        private float m_fOriginalMeshSize = 1.0f;
        [SerializeField, HideInInspector]
        private int[] m_aVertexMap;
        [SerializeField, HideInInspector]
        private int[] m_aVertexPermutation;
        [SerializeField, HideInInspector]
        private Mesh m_meshOriginal;
        [SerializeField, HideInInspector]
        private bool m_bUseEdgeLength = true;
        [SerializeField, HideInInspector]
        bool m_bUseCurvature = true, m_bProtectTexture = true, m_bLockBorder = true;

        private Action<Vector3[]> _assignVertices;
        private Action<Vector3[]> _assignNormals;
        private Action<Vector4[]> _assignTangents;
        private Action<Vector2[]> _assignUV;
        private Action<Vector2[]> _assignUV2;
        private Action<Color32[]> _assignColor32;
        private Action<BoneWeight[]> _assignBoneWeights;
        private Action<int[]>[] _setTriangles;
        Vector3[] _av3VerticesOriginal;
        Vector3[] _av3NormalsOriginal;
        Vector4[] _av4TangentsOriginal;
        Vector2[] _av2Mapping1Original;
        Vector2[] _av2Mapping2Original;
        Color32[] _aColors32Original;
        BoneWeight[] _aBoneWeightsOriginal;
        int[][] _aSubMeshesOriginal;
        Vector3[] _av3Vertices;
        Vector3[] _av3NormalsIn;
        Vector4[] _av4TangentsIn;
        Vector2[] _av2Mapping1In;
        Vector2[] _av2Mapping2In;
        Color32[] _aColors32In;
        BoneWeight[] _aBoneWeights;
        Matrix4x4[] _aBindPoses;
        int[][] _aSubMeshes;
        int[] _aTriangleCount;
        Mesh _meshOut;
        int[] _vertexMap;


        #endregion // Private vars
    }
}