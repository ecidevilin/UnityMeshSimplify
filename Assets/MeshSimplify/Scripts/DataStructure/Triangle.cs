using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Chaos
{
    /// <summary>
    /// Stores vertex and mapping information.
    /// </summary>
    public class Triangle
    {
        public Vertex[] Vertices
        {
            get { return _vertices; }
        }

        public bool HasUVData
        {
            get { return _uvData; }
        }

        public Vector3 Normal
        {
            get { return _normal; }
        }

        public int[] Indices
        {
            get { return _indices; }
        }
        public int SubMeshIndex
        {
            get { return _subMeshIndex; }
        }

        public int Index
        {
            get { return _index; }
        }

        private Vertex[] _vertices;
        private bool _uvData;
        private int[] _uvs;
        private int[] _indices;
        private Vector3 _normal;
        private int _subMeshIndex;
        private int _index;
        private int[] _faceIndex;

        public Triangle(int nSubMeshIndex, int nIndex, Vertex v0, Vertex v1, Vertex v2, bool bUVData,
            int nIndex1, int nIndex2, int nIndex3, bool compute)
        {
            _vertices = new Vertex[3];
            _uvs = new int[3];
            _indices = new int[3];

            _vertices[0] = v0;
            _vertices[1] = v1;
            _vertices[2] = v2;

            _subMeshIndex = nSubMeshIndex;
            _index = nIndex;

            _uvData = bUVData;

            if (_uvData)
            {
                _uvs[0] = nIndex1;
                _uvs[1] = nIndex2;
                _uvs[2] = nIndex3;
            }

            _indices[0] = nIndex1;
            _indices[1] = nIndex2;
            _indices[2] = nIndex3;

            if (compute)
            {
                ComputeNormal();
            }

            _faceIndex = new int[3];

            for (int i = 0; i < 3; i++)
            {
                _faceIndex[i] = _vertices[i].ListFaces.Count;
                _vertices[i].ListFaces.Add(this);
                if (!compute)
                {
                    continue;
                }
                for (int j = 0; j < 3; j++)
                {
                    if (i != j)
                    {
                        if (_vertices[i].ListNeighbors.Contains(_vertices[j]) == false)
                        {
                            _vertices[i].ListNeighbors.Add(_vertices[j]);
                        }
                    }
                }
            }
        }

        public void Destructor()
        {
            int i;

            for (i = 0; i < 3; i++)
            {
                if (_vertices[i] != null)
                {
                    List<Triangle> list = _vertices[i].ListFaces;
                    Triangle t = list[list.Count - 1];
                    list[_faceIndex[i]] = t;
                    t._faceIndex[t.IndexOf(_vertices[i])] = _faceIndex[i];
                    list.RemoveAt(list.Count - 1);
                    //m_aVertices[i].m_listFaces.Remove(this);
                }
            }

            for (i = 0; i < 3; i++)
            {
                int i2 = (i + 1) % 3;

                if (_vertices[i] == null || _vertices[i2] == null) continue;

                _vertices[i].RemoveIfNonNeighbor(_vertices[i2]);
                _vertices[i2].RemoveIfNonNeighbor(_vertices[i]);
            }
        }

        public bool HasVertex(Vertex v)
        {
            return IndexOf(v) >= 0; //(v == m_aVertices[0] || v == m_aVertices[1] || v == m_aVertices[2]);
        }

        public int IndexOf(Vertex v)
        {
            for (int i = 0; i < 3; i++)
            {
                if (v == _vertices[i])
                {
                    return i;
                }
            }
            return -1;
        }

        public void ComputeNormal()
        {
            Vector3 v0 = _vertices[0].Position;
            Vector3 v1 = _vertices[1].Position;
            Vector3 v2 = _vertices[2].Position;

            _normal = Vector3.Cross((v1 - v0), (v2 - v1));

            if (_normal.magnitude == 0.0f) return;

            _normal = _normal / _normal.magnitude;
        }

        public int TexAt(Vertex vertex)
        {
            for (int i = 0; i < 3; i++)
            {
                if (_vertices[i] == vertex)
                {
                    return _uvs[i];
                }
            }

            UnityEngine.Debug.LogError("TexAt(): Vertex not found");
            return 0;
        }

        public int TexAt(int i)
        {
            return _uvs[i];
        }

        public void SetTexAt(int i, int uv)
        {
            _uvs[i] = uv;
        }

        public void ReplaceVertex(Vertex vold, Vertex vnew)
        {
            int idx;
            for (idx = 0; idx < 3; idx++)
            {
                if (vold == _vertices[idx])
                {
                    _vertices[idx] = vnew;
                    for (int i = 0; i < 3; i++)
                    {
                        if (i == idx)
                        {
                            continue;
                        }
                        Vertex n = _vertices[i];
                        List<Vertex> nn = n.ListNeighbors;
                        nn.Remove(vold);
                        if (!nn.Contains(vnew))
                        {
                            nn.Add(vnew);
                        }
                        List<Vertex> vn = vnew.ListNeighbors;
                        if (!vn.Contains(n))
                        {
                            vn.Add(n);
                        }
                    }
                    break;
                }
            }
            //vold.m_listFaces.Remove(this);
            _faceIndex[idx] = vnew.ListFaces.Count;
            vnew.ListFaces.Add(this);

            ComputeNormal();
        }
    };
}