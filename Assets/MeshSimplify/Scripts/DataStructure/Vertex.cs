using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chaos
{

    /// <summary>
    /// Stores topology information and edge collapsing information.
    /// </summary>
    public class Vertex : IHeapNode, IComparable<Vertex>
    {
        public int HeapIndex { get; set; }

        public int CompareTo(Vertex other)
        {
            return this.ObjDist > other.ObjDist ? 1 : this.ObjDist < other.ObjDist ? -1 : 0;
        }

        public Vector3 Position {
            get { return _position;}
        }

        public Vector3 PositionWorld
        {
            get { return _positionWorld; }
        }

        public int ID
        {
            get { return _id; }
        }

        public List<Vertex> ListNeighbors
        {
            get { return _listNeighbors; }
        }

        public List<Triangle> ListFaces
        {
            get { return _listFaces; }
        } 

        public Vector2 UV
        {
            get { return _uv; }
        }

        public Vector3 Normal
        {
            get { return _normal; }
        }

        private Vector3 _position;
        private Vector3 _positionWorld;
        private int _id; // Place of vertex in original list
        private List<Vertex> _listNeighbors; // Adjacent vertices
        private List<Triangle> _listFaces; // Adjacent triangles
        public float ObjDist; // Cached cost of collapsing edge
        public Vertex CollapseVertex; // Candidate vertex for collapse

        private Vector2 _uv;
        private Vector3 _normal;

        public Vertex(Vector3 v, Vector3 v3World, int nID, Vector2 uv, Vector3 normal)
        {
            _position = v;
            _positionWorld = v3World;
            this._id = nID;
            _uv = uv;
            _normal = normal;

            _listNeighbors = new List<Vertex>();
            _listFaces = new List<Triangle>();
        }

        public void Destructor()
        {
            for (int i = 0, imax = _listNeighbors.Count; i < imax; i++)
            {
                _listNeighbors[i]._listNeighbors.Remove(this);
            }
            //while (m_listNeighbors.Count > 0)
            //{
            //    m_listNeighbors[m_listNeighbors.Count - 1].m_listNeighbors.Remove(this);

            //    if (m_listNeighbors.Count > 0)
            //    {
            //        m_listNeighbors.RemoveAt(m_listNeighbors.Count - 1);
            //    }
            //}
        }

        public void RemoveIfNonNeighbor(Vertex n)
        {
            int idx = _listNeighbors.IndexOf(n);
            if (idx < 0)
            {
                return;
            }

            for (int i = 0; i < _listFaces.Count; i++)
            {
                if (_listFaces[i].HasVertex(n))
                {
                    return;
                }
            }

            _listNeighbors.RemoveAt(idx);
        }

        public bool IsBorder()
        {
            int i, j;

            for (i = 0; i < _listNeighbors.Count; i++)
            {
                int nCount = 0;

                for (j = 0; j < _listFaces.Count; j++)
                {
                    if (_listFaces[j].HasVertex(_listNeighbors[i]))
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
}