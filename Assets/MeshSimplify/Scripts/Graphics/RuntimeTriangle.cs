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
			public int[] VertexIndices = new int[3];
			public bool HasUVData;
			public int[] IndicesUV = new int[3];
			public int[] Indices = new int[3];
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
				ret.VertexIndices [0] = idx0;
				ret.VertexIndices [1] = idx1;
				ret.VertexIndices [2] = idx2;
				ret.HasUVData = bUVData;
				if (bUVData) {
					ret.IndicesUV [0] = nIndex0;
					ret.IndicesUV [1] = nIndex1;
					ret.IndicesUV [2] = nIndex2;
				}
				ret.Indices [0] = nIndex0;
				ret.Indices [1] = nIndex1;
				ret.Indices [2] = nIndex2;
				return ret;
			}
		}
	}
}
