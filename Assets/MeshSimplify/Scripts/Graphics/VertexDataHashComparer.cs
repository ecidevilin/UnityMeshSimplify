using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UltimateGameTools
{
    namespace MeshSimplifier
    {
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
    }
}
