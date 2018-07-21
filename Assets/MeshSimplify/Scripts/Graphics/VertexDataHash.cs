using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UltimateGameTools
{
    namespace MeshSimplifier
    {

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
                _v3Vertex = v3Vertex;
                _v3Normal = v3Normal;
                _v2Mapping1 = v2Mapping1;
                _v2Mapping2 = v2Mapping2;
                _color = color;
                _uniqueVertex = new UniqueVertex(v3Vertex);
                //_uniqueNormal = new UniqueVertex(v3Normal);
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
            private UniqueVertex _uniqueVertex;
            //private UniqueVertex _uniqueNormal;
        }
    }
}
