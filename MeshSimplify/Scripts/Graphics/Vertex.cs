﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UltimateGameTools
{
    namespace MeshSimplifier
    {

        /// <summary>
        /// Stores topology information and edge collapsing information.
        /// </summary>
        public class Vertex : IHeapNode, IComparable<Vertex>
        {
            public int HeapIndex { get; set; }

            public int CompareTo(Vertex other)
            {
                return this.m_fObjDist > other.m_fObjDist ? 1 : this.m_fObjDist < other.m_fObjDist ? -1 : 0;
            }

            public Vector3 m_v3Position;
            public Vector3 m_v3PositionWorld;
            public bool m_bHasBoneWeight;
            public BoneWeight m_boneWeight;
            public int m_nID; // Place of vertex in original list
            public List<Vertex> m_listNeighbors; // Adjacent vertices
            public List<Triangle> m_listFaces; // Adjacent triangles
            public float m_fObjDist; // Cached cost of collapsing edge
            public Vertex m_collapse; // Candidate vertex for collapse

            public Vertex(Vector3 v, Vector3 v3World, bool bHasBoneWeight, BoneWeight boneWeight,
                int nID)
            {
                m_v3Position = v;
                m_v3PositionWorld = v3World;
                m_bHasBoneWeight = bHasBoneWeight;
                m_boneWeight = boneWeight;
                this.m_nID = nID;

                m_listNeighbors = new List<Vertex>();
                m_listFaces = new List<Triangle>();
            }

            public void Destructor()
            {
                while (m_listNeighbors.Count > 0)
                {
                    m_listNeighbors[0].m_listNeighbors.Remove(this);

                    if (m_listNeighbors.Count > 0)
                    {
                        m_listNeighbors.RemoveAt(0);
                    }
                }
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
    }
}