using System.Collections;
using System.Collections.Generic;
using System;
using Chaos;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

unsafe public struct CopyMeshJob<T> : IJobParallelFor, IDisposable where T : struct
{
    [NativeDisableUnsafePtrRestriction]
    private Simplifier.MappingLinkedNode* _pHeadArray;
    //[ReadOnly]
    //public NativeArray<Simplifier.MappingLinkedNode> headArray;
    [NativeDisableUnsafePtrRestriction]
    private void* _pArray;
    [NativeDisableParallelForRestriction]
    private NativeArray<T> _array;

    public static CopyMeshJob<T> Create(Simplifier.MappingLinkedNode *pHeadArray, int length)
    {
        CopyMeshJob<T> job = new CopyMeshJob<T>();
        job._pHeadArray = pHeadArray;
        job._array = new NativeArray<T>(length, Allocator.TempJob);
        job._pArray = job._array.GetUnsafePtr();

        return job;
    }

    public void Execute(int index)
    {
        Simplifier.MappingLinkedNode* node = _pHeadArray[index].Next;
        int idx = node->Mapping;
        T tmp = UnsafeUtility.ReadArrayElement<T>(_pArray, idx);
        node = node->Next;
        while (node != null)
        {
            int vidx = node->Mapping;
            T tmp_ = UnsafeUtility.ReadArrayElement<T>(_pArray, vidx);
            UnsafeUtility.WriteArrayElement<T>(_pArray, vidx, tmp);
            tmp = tmp_;
            node = node->Next;
        }
    }
    public void Dispose()
    {
        _array.Dispose();
    }

    public void CopyFromArray(Vector2[] inArray)
    {
        fixed (void* arrPtr = inArray)
        {
            UnsafeUtility.MemCpy(_pArray, arrPtr, inArray.Length * sizeof(Vector2));
        }
    }
    public void CopyFromArray(Vector3[] inArray)
    {
        fixed (void* arrPtr = inArray)
        {
            UnsafeUtility.MemCpy(_pArray, arrPtr, inArray.Length * sizeof(Vector3));
        }
    }
    public void CopyFromArray(Vector4[] inArray)
    {
        fixed (void* arrPtr = inArray)
        {
            UnsafeUtility.MemCpy(_pArray, arrPtr, inArray.Length * sizeof(Vector4));
        }
    }
    public void CopyFromArray(Color32[] inArray)
    {
        fixed (void* arrPtr = inArray)
        {
            UnsafeUtility.MemCpy(_pArray, arrPtr, inArray.Length * sizeof(Color32));
        }
    }
    public void CopyFromArray(BoneWeight[] inArray)
    {
        fixed (void* arrPtr = inArray)
        {
            UnsafeUtility.MemCpy(_pArray, arrPtr, inArray.Length * sizeof(BoneWeight));
        }
    }
    public void CopyToArrayAndDispose(Vector2[] outArray, int len)
    {
        fixed (void* arrPtr = outArray)
        {
            UnsafeUtility.MemCpy(arrPtr, _pArray, len * sizeof(Vector2));
        }
        Dispose();
    }
    public void CopyToArrayAndDispose(Vector3[] outArray, int len)
    {
        fixed (void* arrPtr = outArray)
        {
            UnsafeUtility.MemCpy(arrPtr, _pArray, len * sizeof(Vector3));
        }
        Dispose();
    }
    public void CopyToArrayAndDispose(Vector4[] outArray, int len)
    {
        fixed (void* arrPtr = outArray)
        {
            UnsafeUtility.MemCpy(arrPtr, _pArray, len * sizeof(Vector4));
        }
        Dispose();
    }
    public void CopyToArrayAndDispose(Color32[] outArray, int len)
    {
        fixed (void* arrPtr = outArray)
        {
            UnsafeUtility.MemCpy(arrPtr, _pArray, len * sizeof(Color32));
        }
        Dispose();
    }
    public void CopyToArrayAndDispose(BoneWeight[] outArray, int len)
    {
        fixed (void* arrPtr = outArray)
        {
            UnsafeUtility.MemCpy(arrPtr, _pArray, len * sizeof(BoneWeight));
        }
        Dispose();
    }
}
