using UnityEngine;
using System.Collections.Generic;
using Chaos;

public partial class MeshSimplify : MonoBehaviour
{
    public bool RecurseIntoChildren
    {
        get { return _generateIncludeChildren; }
    }

    public Simplifier MeshSimplifier
    {
        get { return _meshSimplifier; }
        set { _meshSimplifier = value; }
    }

    public bool DataDirty
    {
        get { return _dataDirty; }
        set { _dataDirty = value; }
    }

    [HideInInspector] public Mesh OriginalMesh = null;
    [HideInInspector] public Mesh SimplifiedMesh = null;
    [HideInInspector] public float VertexAmount = 1.0f;
    [HideInInspector] public string AssetPath = null;
    [HideInInspector] public MeshSimplify MeshSimplifyRoot;
    [HideInInspector] public List<MeshSimplify> ListDependentChildren = new List<MeshSimplify>();
    [HideInInspector] public bool ExpandRelevanceSpheres = true;
    [SerializeField, HideInInspector] private Simplifier _meshSimplifier = null;
    [SerializeField, HideInInspector] private bool _generateIncludeChildren = true;
    [SerializeField, HideInInspector] private bool _overrideRootSettings = false;
    [SerializeField, HideInInspector] private bool _useEdgeLength = true;
    [SerializeField, HideInInspector] private bool _useCurvature = true;
    [SerializeField, HideInInspector] private bool _protectTexture = true;
    [SerializeField, HideInInspector] private bool _lockBorder = true;
    [SerializeField, HideInInspector] private bool _dataDirty = true;
    [SerializeField, HideInInspector] private bool _excludedFromTree = false;
    public RelevanceSphere[] RelevanceSpheres = null;
    public void ConfigureSimplifier()
    {
        if (MeshSimplifyRoot != null && _overrideRootSettings == false)
        {
            _meshSimplifier.UseEdgeLength = MeshSimplifyRoot._useEdgeLength;
            _meshSimplifier.UseCurvature = MeshSimplifyRoot._useCurvature;
            _meshSimplifier.ProtectTexture = MeshSimplifyRoot._protectTexture;
            _meshSimplifier.LockBorder = MeshSimplifyRoot._lockBorder;
        }
        else
        {
            _meshSimplifier.UseEdgeLength = _useEdgeLength;
            _meshSimplifier.UseCurvature = _useCurvature;
            _meshSimplifier.ProtectTexture = _protectTexture;
            _meshSimplifier.LockBorder = _lockBorder;
        }
    }
    public bool HasData()
    {
        return (_meshSimplifier != null && SimplifiedMesh != null) || (ListDependentChildren != null && ListDependentChildren.Count != 0);
    }

    public bool HasNonMeshSimplifyGameObjectsInTree()
    {
        return HasNonMeshSimplifyGameObjectsInTreeRecursive(this, this.gameObject);
    }

    public void ComputeData(bool bRecurseIntoChildren, Simplifier.ProgressDelegate progress = null)
    {
        ComputeDataRecursive(this, this.gameObject, bRecurseIntoChildren, progress);
    }

    public void ComputeMesh(bool bRecurseIntoChildren, Simplifier.ProgressDelegate progress = null)
    {
        ComputeMeshRecursive(this, this.gameObject, bRecurseIntoChildren, progress);
    }


    public void AssignSimplifiedMesh(bool bRecurseIntoChildren)
    {
        AssignSimplifiedMeshRecursive(this, this.gameObject, bRecurseIntoChildren);
    }

    public void RestoreOriginalMesh(bool bDeleteData, bool bRecurseIntoChildren)
    {
        RestoreOriginalMeshRecursive(this, this.gameObject, bDeleteData, bRecurseIntoChildren);
    }

    public int GetOriginalVertexCount(bool bRecurseIntoChildren)
    {
        int nVertexCount = 0;
        GetOriginalVertexCountRecursive(this, this.gameObject, ref nVertexCount, bRecurseIntoChildren);
        return nVertexCount;
    }


    public int GetOriginalTriangleCount(bool bRecurseIntoChildren)
    {
        int nTriangleCount = 0;
        GetOriginalTriangleCountRecursive(this, this.gameObject, ref nTriangleCount, bRecurseIntoChildren);
        return nTriangleCount;
    }


    public int GetSimplifiedVertexCount(bool bRecurseIntoChildren)
    {
        int nVertexCount = 0;
        GetSimplifiedVertexCountRecursive(this, this.gameObject, ref nVertexCount, bRecurseIntoChildren);
        return nVertexCount;
    }


    public int GetSimplifiedTriangleCount(bool bRecurseIntoChildren)
    {
        int nTriangleCount = 0;
        GetSimplifiedTriangleCountRecursive(this, this.gameObject, ref nTriangleCount, bRecurseIntoChildren);
        return nTriangleCount;
    }

    public void FreeData(bool bRecurseIntoChildren)
    {
        FreeDataRecursive(this, this.gameObject, bRecurseIntoChildren);
    }

    public void RemoveFromTree()
    {
        if (MeshSimplifyRoot != null)
        {
            MeshSimplifyRoot.ListDependentChildren.Remove(this);
        }

        RestoreOriginalMesh(true, false);

        _excludedFromTree = true;
    }
    //public bool HasVertexData(bool bRecurseIntoChildren)
    //{
    //    return HasVertexDataRecursive(this, this.gameObject, bRecurseIntoChildren);
    //}
    //public bool HasOriginalMeshActive(bool bRecurseIntoChildren)
    //{
    //    return HasOriginalMeshActiveRecursive(this, this.gameObject, bRecurseIntoChildren);
    //}
}
