#if UNITY_2018_1_OR_NEWER
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace Chaos
{
    public struct SkinLocalToWorldJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Matrix4x4> Bones;
        [ReadOnly]
        public NativeArray<BoneWeight> BoneWeights;
        [ReadOnly]
        public NativeArray<Matrix4x4> BindPoses;
        [ReadOnly]
        public NativeArray<Vector3> Vertices;
        public NativeArray<Vector3> WorldPositions;
        public void Execute(int index)
        {
            BoneWeight bw = BoneWeights[index];
            Vector4 v = Vertices[index];
            v.w = 1;
            WorldPositions[index] = Bones[bw.boneIndex0] * BindPoses[bw.boneIndex0] * v * bw.weight0
                            + Bones[bw.boneIndex1] * BindPoses[bw.boneIndex1] * v * bw.weight1
                            + Bones[bw.boneIndex2] * BindPoses[bw.boneIndex2] * v * bw.weight2
                            + Bones[bw.boneIndex3] * BindPoses[bw.boneIndex3] * v * bw.weight3;

        }
    }
    public struct MeshLocalToWorldJob : IJobParallelFor
    {
        public Matrix4x4 LocalToWorldMatrix;
        [ReadOnly] public NativeArray<Vector3> Vertices;
        public NativeArray<Vector3> WorldPositions;
        public void Execute(int index)
        {
            Vector4 v = Vertices[index];
            v.w = 1;
            WorldPositions[index] = LocalToWorldMatrix*v;

        }
    }

    public static class LocalToWorldTransformation
    {
        public static void Transform(SkinnedMeshRenderer skin, Vector3[] worldVertices)
        {
            SkinLocalToWorldJob job = new SkinLocalToWorldJob();
            Transform[] bones = skin.bones;
            job.Bones = new NativeArray<Matrix4x4>(bones.Length, Allocator.TempJob);
            for (int i = 0, imax = bones.Length; i < imax; i++)
            {
                job.Bones[i] = bones[i].localToWorldMatrix;
            }
            Mesh mesh = skin.sharedMesh;
            job.BoneWeights = new NativeArray<BoneWeight>(mesh.boneWeights, Allocator.TempJob);
            job.BindPoses = new NativeArray<Matrix4x4>(mesh.bindposes, Allocator.TempJob);
            job.Vertices = new NativeArray<Vector3>(mesh.vertices, Allocator.TempJob);
            job.WorldPositions = new NativeArray<Vector3>(worldVertices, Allocator.TempJob);
            JobHandle handle = job.Schedule(worldVertices.Length, 1);
            handle.Complete();
            job.WorldPositions.CopyTo(worldVertices);
            job.Bones.Dispose();
            job.BoneWeights.Dispose();
            job.BindPoses.Dispose();
            job.Vertices.Dispose();
            job.WorldPositions.Dispose();
        }
        public static void Transform(MeshFilter filter, Vector3[] worldVertices)
        {
            MeshLocalToWorldJob job = new MeshLocalToWorldJob();
            Mesh mesh = filter.sharedMesh;
            job.LocalToWorldMatrix = filter.transform.localToWorldMatrix;
            job.Vertices = new NativeArray<Vector3>(mesh.vertices, Allocator.TempJob);
            job.WorldPositions = new NativeArray<Vector3>(worldVertices, Allocator.TempJob);
            JobHandle handle = job.Schedule(worldVertices.Length, 1);
            handle.Complete();
            job.WorldPositions.CopyTo(worldVertices);
            job.Vertices.Dispose();
            job.WorldPositions.Dispose();
        }
    }
}
#endif