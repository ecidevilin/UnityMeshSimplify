using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Chaos;
using UltimateGameTools.MeshSimplifier;

public partial class MeshSimplify : MonoBehaviour
{
    public bool RecurseIntoChildren
    {
        get
        {
            return m_bGenerateIncludeChildren;
        }
    }

    public Simplifier MeshSimplifier
    {
        get
        {
            return m_meshSimplifier;
        }
        set
        {
            m_meshSimplifier = value;
        }
    }

    [HideInInspector]
    public Mesh m_originalMesh = null;

    [HideInInspector]
    public Mesh m_simplifiedMesh = null;

    [HideInInspector]
    public bool m_bEnablePrefabUsage = false;

    [HideInInspector]
    public float m_fVertexAmount = 1.0f;

    [HideInInspector]
    public string m_strAssetPath = null;

    [HideInInspector]
    public MeshSimplify m_meshSimplifyRoot;

    [HideInInspector]
    public List<MeshSimplify> m_listDependentChildren = new List<MeshSimplify>();

    [HideInInspector]
    public bool m_bExpandRelevanceSpheres = true;

    public RelevanceSphere[] m_aRelevanceSpheres = null;

    [SerializeField, HideInInspector]
    private Simplifier m_meshSimplifier = null;

    [SerializeField, HideInInspector]
    private bool m_bGenerateIncludeChildren = true;

    [SerializeField, HideInInspector]
    private bool m_bOverrideRootSettings = false;

    [SerializeField, HideInInspector]
    private bool m_bUseEdgeLength = true, m_bUseCurvature = true, m_bProtectTexture = true, m_bLockBorder = true;

    [SerializeField, HideInInspector]
    private bool m_bDataDirty = true;

    [SerializeField, HideInInspector]
    private bool m_bExcludedFromTree = false;



    public bool IsGenerateIncludeChildrenActive()
    {
        return m_bGenerateIncludeChildren;
    }

    public bool HasDependentChildren()
    {
        return m_listDependentChildren != null && m_listDependentChildren.Count > 0;
    }

    public bool HasDataDirty()
    {
        return m_bDataDirty;
    }

    public bool SetDataDirty(bool bDirty)
    {
        return m_bDataDirty = bDirty;
    }

    public bool HasNonMeshSimplifyGameObjectsInTree()
    {
        return HasNonMeshSimplifyGameObjectsInTreeRecursive(this, this.gameObject);
    }

    private bool HasNonMeshSimplifyGameObjectsInTreeRecursive(MeshSimplify root, GameObject gameObject)
    {
        MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

        if (meshSimplify == null && MeshUtil.HasValidMeshData(gameObject))
        {
            return true;
        }

        for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
        {
            if(HasNonMeshSimplifyGameObjectsInTreeRecursive(root, gameObject.transform.GetChild(nChild).gameObject))
            {
                return true;
            }
        }

        return false;
    }

    public void ConfigureSimplifier()
    {
        m_meshSimplifier.UseEdgeLength  = (m_meshSimplifyRoot != null && m_bOverrideRootSettings == false) ? m_meshSimplifyRoot.m_bUseEdgeLength  : m_bUseEdgeLength;
        m_meshSimplifier.UseCurvature   = (m_meshSimplifyRoot != null && m_bOverrideRootSettings == false) ? m_meshSimplifyRoot.m_bUseCurvature   : m_bUseCurvature;
        m_meshSimplifier.ProtectTexture = (m_meshSimplifyRoot != null && m_bOverrideRootSettings == false) ? m_meshSimplifyRoot.m_bProtectTexture : m_bProtectTexture;
        m_meshSimplifier.LockBorder     = (m_meshSimplifyRoot != null && m_bOverrideRootSettings == false) ? m_meshSimplifyRoot.m_bLockBorder     : m_bLockBorder;
    }

    public Simplifier GetMeshSimplifier()
    {
        return m_meshSimplifier;
    }

    public void ComputeData(bool bRecurseIntoChildren, Simplifier.ProgressDelegate progress = null)
    {
        ComputeDataRecursive(this, this.gameObject, bRecurseIntoChildren, progress);
    }


    public bool HasData()
    {
        return (m_meshSimplifier != null && m_simplifiedMesh != null) || (m_listDependentChildren != null && m_listDependentChildren.Count != 0);
    }

    public bool HasSimplifiedMesh()
    {
        return m_simplifiedMesh != null && m_simplifiedMesh.vertexCount > 0;
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


    public bool HasOriginalMeshActive(bool bRecurseIntoChildren)
    {
        return HasOriginalMeshActiveRecursive(this, this.gameObject, bRecurseIntoChildren);
    }


    public bool HasVertexData(bool bRecurseIntoChildren)
    {
        return HasVertexDataRecursive(this, this.gameObject, bRecurseIntoChildren);
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
}
