using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Jobs;
#if UNITY_2018_1_OR_NEWER
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
#endif

namespace Chaos
{

    public class Simplifier : MonoBehaviour
    {
#if UNITY_2018_1_OR_NEWER
        public unsafe struct MappingLinkedNode
        {
            public MappingLinkedNode *Next;
            public int Mapping;
        }
#endif
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
            get { return _coroutineFrameMiliseconds; }
            set { _coroutineFrameMiliseconds = value; }
        }

        public bool CoroutineEnded { get; set; }

        public bool UseEdgeLength
        {
            get { return _useEdgeLength; }
            set { _useEdgeLength = value; }
        }

        public bool UseCurvature
        {
            get { return _useCurvature; }
            set { _useCurvature = value; }
        }

        public bool ProtectTexture
        {
            get { return _protectTexture; }
            set { _protectTexture = value; }
        }

        public bool LockBorder
        {
            get { return _lockBorder; }
            set { _lockBorder = value; }
        }

        #endregion // Properties

        #region Public methods

        /////////////////////////////////////////////////////////////////////////////////////////////////
        // Public methods
        /////////////////////////////////////////////////////////////////////////////////////////////////

        public IEnumerator ProgressiveMesh(GameObject gameObject, Mesh sourceMesh, RelevanceSphere[] aRelevanceSpheres, string strProgressDisplayObjectName = "", ProgressDelegate progress = null)
        {
            _meshOriginal = sourceMesh;

            //Vector3[] aVerticesWorld = GetWorldVertices(gameObject);

            //if (aVerticesWorld == null)
            //{
            //  CoroutineEnded = true;
            //  yield break;
            //}
            int vertexCount = sourceMesh.vertexCount;
            _vertexMapping = new int[vertexCount];
            _vertexPermutation = new int[vertexCount];
            _listVertices = new List<Vertex>();
            _listTriangles = new TriangleList[_meshOriginal.subMeshCount];

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

            _originalMeshVertexCount = sourceMesh.vertexCount;//m_meshUniqueVertices.ListVertices.Count;
            _originalMeshSize = Mathf.Max(_meshOriginal.bounds.size.x, _meshOriginal.bounds.size.y, _meshOriginal.bounds.size.z);

            _heap = Heap<Vertex>.CreateMinHeap();

            for (int i = 0; i < _originalMeshVertexCount; i++)
            {
                _vertexMapping[i] = -1;
                _vertexPermutation[i] = -1;
            }

            Vector2[] av2Mapping = _meshOriginal.uv;

            AddVertices(sourceMesh.vertices, worldVertices);

            int nTriangles = 0;
            //                ListIndices[] faceList = m_meshUniqueVertices.SubmeshesFaceList;
            for (int nSubMesh = 0; nSubMesh < _meshOriginal.subMeshCount; nSubMesh++)
            {
                int[] anIndices = _meshOriginal.GetTriangles(nSubMesh);
                //                    List<int> listIndices = faceList[nSubMesh].m_listIndices;
                _listTriangles[nSubMesh] = new TriangleList(anIndices.Length / 3);
                nTriangles = AddFaceListSubMesh(nSubMesh, anIndices, av2Mapping, nTriangles);
            }

            if (Application.isEditor && !Application.isPlaying)
            {
#if UNITY_2018_1_OR_NEWER
                float[] costs = new float[_listVertices.Count];
                int[] collapses = new int[_listVertices.Count];
                CostCompution.Compute(_listVertices, _listTriangles, aRelevanceSpheres, _useEdgeLength, _useCurvature, _lockBorder, _originalMeshSize, costs, collapses);

                for (int i = 0; i < _listVertices.Count; i++)
                {
                    Vertex v = _listVertices[i];
                    v.ObjDist = costs[i];
                    v.CollapseVertex = collapses[i] == -1 ? null : _listVertices[collapses[i]];
                    _heap.Insert(v);
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
                float[] costs = new float[_listVertices.Count];
                int[] collapses = new int[_listVertices.Count];
                CostCompution.Compute(_listVertices, _listTriangles, aRelevanceSpheres, _useEdgeLength, _useCurvature, _lockBorder, _originalMeshSize, costs, collapses);

                for (int i = 0; i < _listVertices.Count; i++)
                {
                    Vertex v = _listVertices[i];
                    v.ObjDist = costs[i];
                    v.CollapseVertex = _listVertices[collapses[i]];
                    _heap.Insert(v);
                }
#else
                    yield return StartCoroutine(ComputeAllEdgeCollapseCosts(strProgressDisplayObjectName, gameObject.transform, aRelevanceSpheres, progress));
                
#endif
            }

            //int nVertices = m_listVertices.Count;

            //Stopwatch sw = Stopwatch.StartNew();

            int vertexNum = _listVertices.Count;
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
                Vertex mn = _heap.ExtractTop();

                //                    m_listVertexPermutationBack[m_listVertices.Count - 1] = mn.m_nID;
                _vertexPermutation[mn.ID] = vertexNum;
                _vertexMapping[mn.ID] = mn.CollapseVertex != null ? mn.CollapseVertex.ID : -1;
                Collapse(mn, mn.CollapseVertex, gameObject.transform, aRelevanceSpheres);
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

            _verticesOriginal = _verticesOriginal ?? _meshOriginal.vertices;
            _normalsOriginal = _normalsOriginal ?? _meshOriginal.normals;
            _tangentsOriginal = _tangentsOriginal ?? _meshOriginal.tangents;
            _texCoord1Original = _texCoord1Original ?? _meshOriginal.uv;
            _texCoord2Original = _texCoord2Original ?? _meshOriginal.uv2;
            _colors32Original = _colors32Original ?? _meshOriginal.colors32;
            _boneWeightsOriginal = _boneWeightsOriginal ?? _meshOriginal.boneWeights;
            _bindPoses = _bindPoses ?? _meshOriginal.bindposes;
            int subMeshCount = _meshOriginal.subMeshCount;
            if (null == _subMeshesOriginal)
            {
                _subMeshesOriginal = new int[subMeshCount][];
                for (int nSubMesh = 0; nSubMesh < subMeshCount; nSubMesh++)
                {
                    _subMeshesOriginal[nSubMesh] = _meshOriginal.GetTriangles(nSubMesh);
                }
            }

            if (nVertices >= GetOriginalMeshUniqueVertexCount())
            {
                // Original vertex count requested

                meshOut.triangles = new int[0];
                meshOut.subMeshCount = _meshOriginal.subMeshCount;

                meshOut.vertices = _verticesOriginal;
                meshOut.normals = _normalsOriginal;
                meshOut.tangents = _tangentsOriginal;
                meshOut.uv = _texCoord1Original;
                meshOut.uv2 = _texCoord2Original;
                meshOut.colors32 = _colors32Original;
                meshOut.boneWeights = _boneWeightsOriginal;
                meshOut.bindposes = _bindPoses;

                meshOut.triangles = _meshOriginal.triangles;
                meshOut.subMeshCount = _meshOriginal.subMeshCount;

                for (int nSubMesh = 0; nSubMesh < subMeshCount; nSubMesh++)
                {
                    meshOut.SetTriangles(_subMeshesOriginal[nSubMesh], nSubMesh);
                }

                meshOut.name = gameObject.name + " simplified mesh";

                return;
            }

            ConsolidateMesh(gameObject, meshOut, _vertexPermutation, _vertexMapping, nVertices);
        }

        public int GetOriginalMeshUniqueVertexCount()
        {
            return _originalMeshVertexCount;
        }

        public int GetOriginalMeshTriangleCount()
        {
            return _meshOriginal.triangles.Length / 3;
        }
        #endregion // Public methods

        #region Private methods

        /////////////////////////////////////////////////////////////////////////////////////////////////
        // Private methods
        /////////////////////////////////////////////////////////////////////////////////////////////////
        unsafe void ConsolidateMesh(GameObject gameObject, Mesh meshOut, int[] permutation, int[] collapseMap, int nVertices)
        {
            int subMeshCount = _subMeshesOriginal.Length;
            if (null == _vertices) _vertices = (Vector3[])_verticesOriginal.Clone();
            else _verticesOriginal.CopyTo(_vertices, 0);
            if (null == _normalsIn) _normalsIn = (Vector3[])_normalsOriginal.Clone();
            else _normalsOriginal.CopyTo(_normalsIn, 0);
            if (null == _tangentsIn) _tangentsIn = (Vector4[])_tangentsOriginal.Clone();
            else _tangentsOriginal.CopyTo(_tangentsIn, 0);
            if (null == _texCoord1In) _texCoord1In = (Vector2[])_texCoord1Original.Clone();
            else _texCoord1Original.CopyTo(_texCoord1In, 0);
            if (null == _texCoord2In) _texCoord2In = (Vector2[])_texCoord2Original.Clone();
            else _texCoord2Original.CopyTo(_texCoord2In, 0);
            if (null == _colors32In) _colors32In = (Color32[])_colors32Original.Clone();
            else _colors32Original.CopyTo(_colors32In, 0);
            if (null == _boneWeights) _boneWeights = (BoneWeight[])_boneWeightsOriginal.Clone();
            else _boneWeightsOriginal.CopyTo(_boneWeights, 0);
            if (null == _subMeshes) _subMeshes = new int[subMeshCount][];
            if (null == _triangleCount) _triangleCount = new int[subMeshCount];

            bool bUV1 = _texCoord1In != null && _texCoord1In.Length > 0;
            bool bUV2 = _texCoord2In != null && _texCoord2In.Length > 0;
            bool bNormal = _normalsIn != null && _normalsIn.Length > 0;
            bool bTangent = _tangentsIn != null && _tangentsIn.Length > 0;
            bool bColor32 = (_colors32In != null && _colors32In.Length > 0);
            bool bBone = _boneWeights != null && _boneWeights.Length > 0;

            _vertexMap = _vertexMap ?? new int[_vertices.Length];
            for (int i = 0, imax = _vertexMap.Length; i < imax; i++)
            {
                _vertexMap[i] = -1;
            }
            int n = 0;
            for (int nSubMesh = 0; nSubMesh < subMeshCount; nSubMesh++)
            {
                if (null == _subMeshes[nSubMesh]) _subMeshes[nSubMesh] = (int[])_subMeshesOriginal[nSubMesh].Clone();
                else _subMeshesOriginal[nSubMesh].CopyTo(_subMeshes[nSubMesh], 0);
                int[] triangles = _subMeshes[nSubMesh];
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
                    _triangleCount[nSubMesh] = t + 1;
                    //#if DEBUG
                    //                        if (t >= 0 && triangles[t] == -1)
                    //                        {
                    //                            throw new Exception("triangles[t] == -1");
                    //                        }
                    //#endif
                }
                else
                {
                    _triangleCount[nSubMesh] = l;
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
            for (int i = 0; i < _vertexMap.Length; i++)
            {
                int idx = i;
                Vector3 tmp = _vertices[idx];
                if (bUV1) tmpUV = _texCoord1In[idx];
                if (bUV2) tmpUV2 = _texCoord2In[idx];
                if (bNormal) tmpNormal = _normalsIn[idx];
                if (bTangent) tmpTangent = _tangentsIn[idx];
                if (bColor32) tmpColor = _colors32In[idx];
                if (bBone) tmpBoneWeight = _boneWeights[idx];
                while (_vertexMap[idx] != -1)
                {
                    Vector3 tmp_ = _vertices[_vertexMap[idx]];
                    if (bUV1) tmpUV_ = _texCoord1In[_vertexMap[idx]];
                    if (bUV2) tmpUV2_ = _texCoord2In[_vertexMap[idx]];
                    if (bNormal) tmpNormal_ = _normalsIn[_vertexMap[idx]];
                    if (bTangent) tmpTangent_ = _tangentsIn[_vertexMap[idx]];
                    if (bColor32) tmpColor_ = _colors32In[_vertexMap[idx]];
                    if (bBone) tmpBoneWeight_ = _boneWeights[_vertexMap[idx]];
                    _vertices[_vertexMap[idx]] = tmp;
                    if (bUV1) _texCoord1In[_vertexMap[idx]] = tmpUV;
                    if (bUV2) _texCoord2In[_vertexMap[idx]] = tmpUV2;
                    if (bNormal) _normalsIn[_vertexMap[idx]] = tmpNormal;
                    if (bTangent) _tangentsIn[_vertexMap[idx]] = tmpTangent;
                    if (bColor32) _colors32In[_vertexMap[idx]] = tmpColor;
                    if (bBone) _boneWeights[_vertexMap[idx]] = tmpBoneWeight;
                    tmp = tmp_;
                    tmpUV = tmpUV_;
                    tmpUV2 = tmpUV2_;
                    tmpNormal = tmpNormal_;
                    tmpTangent = tmpTangent_;
                    tmpColor = tmpColor_;
                    tmpBoneWeight = tmpBoneWeight_;
                    int tmpI = _vertexMap[idx];
                    _vertexMap[idx] = -1;
                    idx = tmpI;
                }
            }

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
            UnsafeUtil.Vector3HackArraySizeCall(_vertices, n, _assignVertices);
            if (bNormal) UnsafeUtil.Vector3HackArraySizeCall(_normalsIn, n, _assignNormals);
            if (bTangent) UnsafeUtil.Vector4HackArraySizeCall(_tangentsIn, n, _assignTangents);
            if (bUV1) UnsafeUtil.Vector2HackArraySizeCall(_texCoord1In, n, _assignUV);
            if (bUV2) UnsafeUtil.Vector2HackArraySizeCall(_texCoord2In, n, _assignUV2);
            if (bColor32) UnsafeUtil.Color32HackArraySizeCall(_colors32In, n, _assignColor32);
            if (bBone) UnsafeUtil.BoneWeightHackArraySizeCall(_boneWeights, n, _assignBoneWeights);
            if (bBone) meshOut.bindposes = _bindPoses;
            meshOut.subMeshCount = _subMeshes.Length;

            for (int i = 0; i < subMeshCount; i++)
            {
                UnsafeUtil.IntegerHackArraySizeCall(_subMeshes[i], _triangleCount[i], _setTriangles[i]);
            }
            meshOut.UploadMeshData(false);
            //meshOut.name = gameObject.name + " simplified mesh";

        }

        float ComputeEdgeCollapseCost(Vertex u, Vertex v, float fRelevanceBias)
        {
            bool bUseEdgeLength = _useEdgeLength;
            bool bUseCurvature = _useCurvature;
            bool bLockBorder = _lockBorder;

            int i;
            float fEdgeLength = bUseEdgeLength ? (Vector3.Magnitude(v.Position - u.Position) / _originalMeshSize) : 1.0f;
            float fCurvature = 0.001f;

            List<Triangle> sides = new List<Triangle>();

            for (i = 0; i < u.ListFaces.Count; i++)
            {
                if (u.ListFaces[i].HasVertex(v))
                {
                    sides.Add(u.ListFaces[i]);
                }
            }

            if (bUseCurvature)
            {
                for (i = 0; i < u.ListFaces.Count; i++)
                {
                    float fMinCurv = 1.0f;

                    for (int j = 0; j < sides.Count; j++)
                    {
                        float dotprod = Vector3.Dot(u.ListFaces[i].Normal, sides[j].Normal);
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
            if (v.ListNeighbors.Count == 0)
            {
                v.CollapseVertex = null;
                v.ObjDist = -0.01f;
                return;
            }

            v.ObjDist = MAX_VERTEX_COLLAPSE_COST;
            v.CollapseVertex = null;

            float fRelevanceBias = 0.0f;

            if (aRelevanceSpheres != null)
            {
                for (int nSphere = 0; nSphere < aRelevanceSpheres.Length; nSphere++)
                {
                    Matrix4x4 mtxSphere = Matrix4x4.TRS(aRelevanceSpheres[nSphere].Position, aRelevanceSpheres[nSphere].Rotation, aRelevanceSpheres[nSphere].Scale);

                    Vector3 v3World = v.PositionWorld;
                    Vector3 v3Local = mtxSphere.inverse.MultiplyPoint(v3World);

                    if (v3Local.magnitude <= 0.5f)
                    {
                        // Inside
                        fRelevanceBias = aRelevanceSpheres[nSphere].Relevance;
                    }
                }
            }

            for (int i = 0; i < v.ListNeighbors.Count; i++)
            {
                float dist = ComputeEdgeCollapseCost(v, v.ListNeighbors[i], fRelevanceBias);

                if (v.CollapseVertex == null || dist < v.ObjDist)
                {
                    v.CollapseVertex = v.ListNeighbors[i];
                    v.ObjDist = dist;
                }
            }
        }

        IEnumerator ComputeAllEdgeCollapseCosts(string strProgressDisplayObjectName, Transform transform, RelevanceSphere[] aRelevanceSpheres, ProgressDelegate progress = null)
        {
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < _listVertices.Count; i++)
            {
                if (progress != null && ((i & 0xFF) == 0))
                {
                    progress("Preprocessing mesh: " + strProgressDisplayObjectName, "Computing edge collapse cost", _listVertices.Count == 1 ? 1.0f : ((float)i / (_listVertices.Count - 1.0f)));

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

                ComputeEdgeCostAtVertex(_listVertices[i], transform, aRelevanceSpheres);
                _heap.Insert(_listVertices[i]);
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

            for (i = 0; i < u.ListNeighbors.Count; i++)
            {
                Vertex nb = u.ListNeighbors[i];
                if (nb != u)
                {
                    tmpVertices.Add(nb);
                }
            }

            tmpTriangles.Clear();

            for (i = 0; i < u.ListFaces.Count; i++)
            {
                if (u.ListFaces[i].HasVertex(v))
                {
                    tmpTriangles.Add(u.ListFaces[i]);
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

            for (i = u.ListFaces.Count - 1; i >= 0; i--)
            {
                u.ListFaces[i].ReplaceVertex(u, v);
            }
            //m_listVertices.Remove(u);
            u.Destructor();

            // Recompute the edge collapse costs for neighboring vertices

            for (i = 0; i < tmpVertices.Count; i++)
            {
                ComputeEdgeCostAtVertex(tmpVertices[i], transform, aRelevanceSpheres);
                _heap.ModifyValue(tmpVertices[i].HeapIndex, tmpVertices[i]);
            }
        }

        void AddVertices(Vector3[] listVertices, Vector3[] listVerticesWorld)
        {
            for (int i = 0; i < listVertices.Length; i++)
            {
                Vertex v = new Vertex(listVertices[i], listVerticesWorld[i], i);
                _listVertices.Add(v);
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

            List<Triangle> list = _listTriangles[nSubMesh].ListTriangles;
            for (int i = 0; i < anIndices.Length; i += 3)
            {
                Triangle tri = new Triangle(nSubMesh, nTriangles + list.Count,
                    _listVertices[anIndices[i]], _listVertices[anIndices[i + 1]], _listVertices[anIndices[i + 2]],
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

                for (int j = 0; j < t.Vertices[nCurrentVert].ListFaces.Count; j++)
                {
                    Triangle n = t.Vertices[nCurrentVert].ListFaces[j];

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

        private static int _coroutineFrameMiliseconds = 0;
        private const float MAX_VERTEX_COLLAPSE_COST = 10000000.0f;

        private List<Vertex> _listVertices;
        private Heap<Vertex> _heap;
        private TriangleList[] _listTriangles;
        [SerializeField, HideInInspector]
        private int _originalMeshVertexCount = -1;
        [SerializeField, HideInInspector]
        private float _originalMeshSize = 1.0f;
        [SerializeField, HideInInspector]
        private int[] _vertexMapping;
        [SerializeField, HideInInspector]
        private int[] _vertexPermutation;
        [SerializeField, HideInInspector]
        private Mesh _meshOriginal;
        [SerializeField, HideInInspector]
        private bool _useEdgeLength = true;
        [SerializeField, HideInInspector]
        bool _useCurvature = true, _protectTexture = true, _lockBorder = true;

        private Action<Vector3[]> _assignVertices;
        private Action<Vector3[]> _assignNormals;
        private Action<Vector4[]> _assignTangents;
        private Action<Vector2[]> _assignUV;
        private Action<Vector2[]> _assignUV2;
        private Action<Color32[]> _assignColor32;
        private Action<BoneWeight[]> _assignBoneWeights;
        private Action<int[]>[] _setTriangles;
        Vector3[] _verticesOriginal;
        Vector3[] _normalsOriginal;
        Vector4[] _tangentsOriginal;
        Vector2[] _texCoord1Original;
        Vector2[] _texCoord2Original;
        Color32[] _colors32Original;
        BoneWeight[] _boneWeightsOriginal;
        int[][] _subMeshesOriginal;
        Vector3[] _vertices;
        Vector3[] _normalsIn;
        Vector4[] _tangentsIn;
        Vector2[] _texCoord1In;
        Vector2[] _texCoord2In;
        Color32[] _colors32In;
        BoneWeight[] _boneWeights;
        Matrix4x4[] _bindPoses;
        int[][] _subMeshes;
        int[] _triangleCount;
        Mesh _meshOut;
        int[] _vertexMap;


#endregion // Private vars
    }
}