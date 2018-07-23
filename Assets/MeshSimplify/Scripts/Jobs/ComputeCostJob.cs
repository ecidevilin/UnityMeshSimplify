using System.Collections;
using System.Collections.Generic;
using UltimateGameTools.MeshSimplifier;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

public unsafe struct StructVertex
{
    public Vector3 Position;
    public Vector3 PositionWorld;
    public int ID; // Place of vertex in original list
    public int* Neighbors; // Adjacent vertices
    public int NeighborCount;
    public int* Faces; // Adjacent triangles
    public int FaceCount;
    public int IsBorder;
}

public unsafe struct StructTriangle
{
    public int* Indices;
    public Vector3 Normal;
    public int Index;
    public int HasUVData;
}

public struct StructRelevanceSphere
{
    public Matrix4x4 Transformation;
    public float Relevance;
}

public unsafe struct ComputeCostJob : IJobParallelFor
{
    [ReadOnly]
    public NativeArray<StructVertex> Vertices;
    [ReadOnly]
    public NativeArray<StructTriangle> Triangles;

    [ReadOnly] public NativeArray<StructRelevanceSphere> Spheres; 

    public NativeArray<float> Result;
    public NativeArray<int> Collapse; 
    public void Execute(int index)
    {
        StructVertex sv = Vertices[index];
        if (sv.NeighborCount == 0)
        {
            Collapse[index] = -1;
            Result[index] = -0.01f;
            return;
        }
        float cost = float.MaxValue;
        int collapse = -1;

        float fRelevanceBias = 0.0f;

        for (int nSphere = 0; nSphere < Spheres.Length; nSphere++)
        {
            Matrix4x4 mtxSphere = Spheres[nSphere].Transformation;

            Vector3 v3World = sv.PositionWorld;
            Vector3 v3Local = mtxSphere.inverse.MultiplyPoint(v3World);

            if (v3Local.magnitude <= 0.5f)
            {
                // Inside
                fRelevanceBias = Spheres[nSphere].Relevance;
            }
        }


        for (int i = 0; i < sv.NeighborCount; i++)
        {
            float dist = ComputeEdgeCollapseCost(sv, Vertices[sv.Neighbors[i]], fRelevanceBias);

            if (collapse == -1 || dist < cost)
            {
                collapse = sv.Neighbors[i];
                cost = dist;
            }
        }
        Result[index] = cost;
        Collapse[index] = collapse;
    }

    static public bool UseEdgeLength;
    static public bool UseCurvature;
    static public bool ProtectTexture;
    static public bool LockBorder;
    static public float OriginalMeshSize;


    public static bool HasVertex(StructTriangle t, int v)
    {
        return IndexOf(t, v) >= 0;
    }

    public static int IndexOf(StructTriangle t, int v)
    {
        for (int i = 0; i < 3; i++)
        {
            if (t.Indices[i] == v)
            {
                return i;
            }
        }
        return -1;
    }
    float ComputeEdgeCollapseCost(StructVertex u, StructVertex v, float fRelevanceBias)
    {
        bool bUseEdgeLength = UseEdgeLength;
        bool bUseCurvature = UseCurvature;
        bool bProtectTexture = ProtectTexture;
        bool bLockBorder = LockBorder;

        int i;
        float fEdgeLength = bUseEdgeLength ? (Vector3.Magnitude(v.Position - u.Position) / OriginalMeshSize) : 1.0f;
        float fCurvature = 0.001f;

        List<StructTriangle> sides = new List<StructTriangle>();

        for (i = 0; i < u.FaceCount; i++)
        {
            StructTriangle ut = Triangles[u.Faces[i]];
            if (HasVertex(ut, v.ID))
            {
                sides.Add(ut);
            }
        }

        if (bUseCurvature)
        {
            for (i = 0; i < u.FaceCount; i++)
            {
                float fMinCurv = 1.0f;

                for (int j = 0; j < sides.Count; j++)
                {
                    float dotprod = Vector3.Dot(Triangles[u.Faces[i]].Normal, sides[j].Normal);
                    fMinCurv = Mathf.Min(fMinCurv, (1.0f - dotprod) / 2.0f);
                }

                fCurvature = Mathf.Max(fCurvature, fMinCurv);
            }
        }

        if (u.IsBorder == 1 && sides.Count > 1)
        {
            fCurvature = 1.0f;
        }

        if (bProtectTexture)
        {
            bool bNoMatch = true;

            for (i = 0; i < u.FaceCount; i++)
            {
                for (int j = 0; j < sides.Count; j++)
                {
                    StructTriangle ut = Triangles[u.Faces[i]];
                    if (ut.HasUVData == 0)
                    {
                        bNoMatch = false;
                        break;
                    }

                    if (IndexOf(ut, u.ID) == IndexOf(sides[j], u.ID))
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

        if (bLockBorder && u.IsBorder == 1)
        {
            fCurvature = float.MaxValue;
        }

        fCurvature += fRelevanceBias;

        return fEdgeLength * fCurvature;
    }
}


public static class CostCompution
{
    public unsafe static void Compute(List<Vertex> vertices, TriangleList[] triangleLists, RelevanceSphere[] aRelevanceSpheres, bool bUseEdgeLength, bool bUseCurvature, bool bProtectTexture, bool bLockBorder, float fOriginalMeshSize, float[] costs, int[] collapses)
    {
        ComputeCostJob.UseEdgeLength = bUseEdgeLength;
        ComputeCostJob.UseCurvature = bUseCurvature;
        ComputeCostJob.ProtectTexture = bProtectTexture;
        ComputeCostJob.LockBorder = bLockBorder;
        ComputeCostJob.OriginalMeshSize = fOriginalMeshSize;
        ComputeCostJob job = new ComputeCostJob();
        List<StructTriangle> structTriangles = new List<StructTriangle>();
        int intAlignment = UnsafeUtility.SizeOf<int>();
        for (int n = 0; n < triangleLists.Length; n++)
        {
            List<Triangle> triangles = triangleLists[n].m_listTriangles;
            for (int i = 0; i < triangles.Count; i++)
            {
                Triangle t = triangles[i];
                StructTriangle st = new StructTriangle()
                {
                    Index = t.Index,
                    Indices = (int*)UnsafeUtility.Malloc(t.Indices.Length * intAlignment, intAlignment, Allocator.TempJob),
                    Normal = t.Normal,
                    HasUVData = t.HasUVData ? 1 : 0,
                };
                for (int j = 0; j < t.Indices.Length; j++)
                {
                    st.Indices[j] = t.Indices[j];
                }
                structTriangles.Add(st);
            }
        }
        job.Triangles = new NativeArray<StructTriangle>(structTriangles.ToArray(), Allocator.Temp);
        job.Vertices = new NativeArray<StructVertex>(vertices.Count, Allocator.TempJob);
        for (int i = 0; i < vertices.Count; i++)
        {
            Vertex v = vertices[i];
            StructVertex sv = new StructVertex()
            {
                Position = v.m_v3Position,
                PositionWorld = v.m_v3PositionWorld,
                ID = v.m_nID,
                Neighbors = v.m_listNeighbors.Count == 0 ? null : (int*)UnsafeUtility.Malloc(v.m_listNeighbors.Count * intAlignment, intAlignment, Allocator.TempJob),
                NeighborCount = v.m_listNeighbors.Count,
                Faces = (int*)UnsafeUtility.Malloc(v.m_listFaces.Count * intAlignment, intAlignment, Allocator.TempJob),
                FaceCount = v.m_listFaces.Count,
                IsBorder = v.IsBorder() ? 1 : 0,
            };
            for (int j = 0; j < v.m_listNeighbors.Count; j++)
            {
                sv.Neighbors[j] = v.m_listNeighbors[j].m_nID;
            }
            for (int j = 0; j < v.m_listFaces.Count; j++)
            {
                sv.Faces[j] = v.m_listFaces[j].Index;
            }
            job.Vertices[i] = sv;
        }
        job.Spheres = new NativeArray<StructRelevanceSphere>(aRelevanceSpheres.Length, Allocator.TempJob);
        for (int i = 0; i < aRelevanceSpheres.Length; i++)
        {
            RelevanceSphere rs = aRelevanceSpheres[i];
            StructRelevanceSphere srs = new StructRelevanceSphere()
            {
                Transformation = Matrix4x4.TRS(rs.m_v3Position, rs.m_q4Rotation, rs.m_v3Scale),
                Relevance = rs.m_fRelevance,
            };
            job.Spheres[i] = srs;
        }
        job.Result = new NativeArray<float>(costs, Allocator.TempJob);
        job.Collapse = new NativeArray<int>(collapses, Allocator.TempJob);
        JobHandle handle = job.Schedule(costs.Length, 1);
        handle.Complete();
        job.Result.CopyTo(costs);
        job.Collapse.CopyTo(collapses);
        for (int i = 0; i < job.Triangles.Length; i++)
        {
            UnsafeUtility.Free(job.Triangles[i].Indices, Allocator.TempJob);
        }
        for (int i = 0; i < job.Vertices.Length; i++)
        {
            UnsafeUtility.Free(job.Vertices[i].Neighbors, Allocator.TempJob);
            UnsafeUtility.Free(job.Vertices[i].Faces, Allocator.TempJob);
        }
        job.Vertices.Dispose();
        job.Triangles.Dispose();
        job.Spheres.Dispose();
        job.Result.Dispose();
        job.Collapse.Dispose();
    }
}