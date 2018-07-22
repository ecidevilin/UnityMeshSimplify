using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace UltimateGameTools
{
	namespace MeshSimplifier
	{
		public class RuntimeTriangle
		{
			public int SubMeshIndex;
			public int Index;
			public Vector3Int VertexIndices;
			public bool HasUVData;
			public Vector3Int IndicesUV;
			public Vector3Int Indices;
			private RuntimeTriangle()
			{
			}
			public static RuntimeTriangle CreateRuntimeTriangle(int nSubMesh, int nIndex, bool bUVData,
				int nIndex0, int nIndex1, int nIndex2
				, int nVertices, List<int> permutation, List<int> map)
			{
				int idx0 = nIndex0;
				int idx1 = nIndex1;
				int idx2 = nIndex2;
				while (permutation[idx0] >= nVertices)
				{
					int idx = map[idx0];
					if (idx == -1 || idx1 == idx || idx2 == idx)
					{
						return null;
					}
					idx0 = idx;
				}
				while (permutation[idx1] >= nVertices)
				{
					int idx = map[idx1];
					if (idx == -1 || idx0 == idx || idx2 == idx)
					{
						return null;
					}
					idx1 = idx;
				}
				while (permutation[idx2] >= nVertices)
				{
					int idx = map[idx2];
					if (idx == -1 || idx1 == idx || idx0 == idx)
					{
						return null;
					}
					idx2 = idx;
				}
				RuntimeTriangle ret = new RuntimeTriangle ();
				ret.SubMeshIndex = nSubMesh;
				ret.Index = nIndex;
				ret.VertexIndices = new Vector3Int (idx0, idx1, idx2);
				ret.HasUVData = bUVData;
				if (bUVData) {
					ret.IndicesUV = new Vector3Int (nIndex0, nIndex1, nIndex2);
				}
				ret.Indices = new Vector3Int (nIndex0, nIndex1, nIndex2);
				return ret;
			}
		}
	}
}
