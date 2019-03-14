using System;
using System.Collections.Generic;

public interface IHeapNode
{
    int HeapIndex { get; set; }
}

public class Heap<T> where T : IComparable<T>, IHeapNode
{
    protected List<T> _A;

    public delegate bool Compare<T>(T a, T b);

    protected Compare<T> _compareFunc;

    protected Heap(Compare<T> _func)
    {
        _compareFunc = _func;
        _A = new List<T>();
    }

    protected Heap(Compare<T> _func, List<T> a)
    {
        _compareFunc = _func;
        _A = a;
        for (int i = a.Count / 2; i >= 0; i--)
        {
            Heapify(i);
        }
    }

    protected virtual void Heapify(int i)
    {
        int l = Left(i);
        int r = Right(i);
        int m = i;
        int size = _A.Count;
        if (l < size && _compareFunc(_A[l], _A[i]))
        {
            m = l;
        }
        if (r < size && _compareFunc(_A[r], _A[m]))
        {
            m = r;
        }
        if (m != i)
        {
            Swap(i, m);
            Heapify(m);
        }
    }

    protected static int Parent(int idx)
    {
        return (idx - 1) / 2;
    }

    protected static int Left(int idx)
    {
        return 2 * idx + 1;
    }

    protected static int Right(int idx)
    {
        return 2 * idx + 2;
    }
    protected void Swap(int i, int j)
    {
        T t = _A[i];
        _A[i] = _A[j];
        _A[j] = t;
        _A[i].HeapIndex = i;
        _A[j].HeapIndex = j;
    }

    public int Size()
    {
        return _A.Count;
    }

    public T Top()
    {
        return _A[0];
    }

    public T ExtractTop()
    {
        if (_A.Count == 0)
        {
            throw new Exception("Heap underflow");
        }
        T top = _A[0];
        int last = _A.Count - 1;
        _A[0] = _A[last];
        _A[0].HeapIndex = 0;
        _A.RemoveAt(last);
        Heapify(0);
        return top;
    }

    public void Insert(T val)
    {
        _A.Add(val);
        int idx = _A.Count - 1;
        val.HeapIndex = idx;
        ModifyValue(idx, val);
    }

    public virtual void ModifyValue(int i, T val)
    {
        _A[i] = val;
        val.HeapIndex = i;
        Heapify(i);
        if (i != val.HeapIndex)
        {
            return;
        }
        _A[i] = val;
        val.HeapIndex = i;
        int p = Parent(i);
        while (i > 0 && _compareFunc(_A[i], _A[p]))
        {
            Swap(i, p);
            i = p;
            p = Parent(i);
        }
    }

    private static bool larger(T a, T b)
    {
        return a.CompareTo(b) > 0;
    }
    private static bool smaller(T a, T b)
    {
        return a.CompareTo(b) < 0;
    }
    public static Heap<T> CreateMaxHeap()
    {
        return new Heap<T>(larger);
    }
    public static Heap<T> CreateMaxHeap(List<T> a)
    {
        return new Heap<T>(larger, a);
    }
    public static Heap<T> CreateMinHeap()
    {
        return new Heap<T>(smaller);
    }
    public static Heap<T> CreateMinHeap(List<T> a)
    {
        return new Heap<T>(smaller, a);
    }
}
