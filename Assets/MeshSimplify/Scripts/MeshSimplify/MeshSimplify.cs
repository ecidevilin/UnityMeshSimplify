using UnityEngine;
using System.Collections.Generic;
using Chaos;

public partial class MeshSimplify : MonoBehaviour
{
    public bool RecurseIntoChildren
    {
        get { return m_bGenerateIncludeChildren; }
    }

    public Simplifier MeshSimplifier
    {
        get { return m_meshSimplifier; }
        set { m_meshSimplifier = value; }
    }

    public bool DataDirty
    {
        get { return m_bDataDirty; }
        set { m_bDataDirty = value; }
    }

    [HideInInspector] public Mesh m_originalMesh = null;
    [HideInInspector] public Mesh m_simplifiedMesh = null;
    [HideInInspector] public bool m_bEnablePrefabUsage = false;
    [HideInInspector] public float m_fVertexAmount = 1.0f;
    [HideInInspector] public string m_strAssetPath = null;
    [HideInInspector] public MeshSimplify m_meshSimplifyRoot;
    [HideInInspector] public List<MeshSimplify> m_listDependentChildren = new List<MeshSimplify>();
    [HideInInspector] public bool m_bExpandRelevanceSpheres = true;
    [SerializeField, HideInInspector] private Simplifier m_meshSimplifier = null;
    [SerializeField, HideInInspector] private bool m_bGenerateIncludeChildren = true;
    [SerializeField, HideInInspector] private bool m_bOverrideRootSettings = false;
    [SerializeField, HideInInspector] private bool m_bUseEdgeLength = true;
    [SerializeField, HideInInspector] private bool m_bUseCurvature = true;
    [SerializeField, HideInInspector] private bool m_bProtectTexture = true;
    [SerializeField, HideInInspector] private bool m_bLockBorder = true;
    [SerializeField, HideInInspector] private bool m_bDataDirty = true;
    [SerializeField, HideInInspector] private bool m_bExcludedFromTree = false;
    public RelevanceSphere[] m_aRelevanceSpheres = null;
    public void ConfigureSimplifier()
    {
        if (m_meshSimplifyRoot != null && m_bOverrideRootSettings == false)
        {
            m_meshSimplifier.UseEdgeLength = m_meshSimplifyRoot.m_bUseEdgeLength;
            m_meshSimplifier.UseCurvature = m_meshSimplifyRoot.m_bUseCurvature;
            m_meshSimplifier.ProtectTexture = m_meshSimplifyRoot.m_bProtectTexture;
            m_meshSimplifier.LockBorder = m_meshSimplifyRoot.m_bLockBorder;
        }
        else
        {
            m_meshSimplifier.UseEdgeLength = m_bUseEdgeLength;
            m_meshSimplifier.UseCurvature = m_bUseCurvature;
            m_meshSimplifier.ProtectTexture = m_bProtectTexture;
            m_meshSimplifier.LockBorder = m_bLockBorder;
        }
    }
    public bool HasData()
    {
        return (m_meshSimplifier != null && m_simplifiedMesh != null) || (m_listDependentChildren != null && m_listDependentChildren.Count != 0);
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
        if (m_meshSimplifyRoot != null)
        {
            m_meshSimplifyRoot.m_listDependentChildren.Remove(this);
        }

        RestoreOriginalMesh(true, false);

        m_bExcludedFromTree = true;
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
