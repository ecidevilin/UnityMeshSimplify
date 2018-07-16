using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UltimateGameTools.MeshSimplifier;

public class MeshSimplify : MonoBehaviour
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

#if UNITY_EDITOR

  public void OnDrawGizmos()
  {
    if (m_meshSimplifyRoot != null)
    {
      if (m_meshSimplifyRoot.m_bExpandRelevanceSpheres == false)
      {
        return;
      }
    }
    else
    {
      if (m_bExpandRelevanceSpheres == false)
      {
        return;
      }
    }

    Gizmos.color = Color.red;

    RelevanceSphere[] aRelevanceSpheres = m_meshSimplifyRoot != null ? m_meshSimplifyRoot.m_aRelevanceSpheres : m_aRelevanceSpheres;

    if (aRelevanceSpheres == null)
    {
      return;
    }

    bool bDrawVertices = false;

    for (int i = 0; i < UnityEditor.Selection.gameObjects.Length; i++)
    {
      if (((UnityEditor.Selection.gameObjects[i] == this.gameObject) && m_meshSimplifyRoot == null) || ((m_meshSimplifyRoot != null) && (UnityEditor.Selection.gameObjects[i] == m_meshSimplifyRoot.gameObject)))
      {
        bDrawVertices = true;
      }
    }

    if (bDrawVertices == false)
    {
      return;
    }

    Vector3[] aVerticesWorld = Simplifier.GetWorldVertices(this.gameObject);

    if(aVerticesWorld == null)
    {
      return;
    }

    Matrix4x4[] aSphereMatrices = new Matrix4x4[aRelevanceSpheres.Length];

    for (int nSphere = 0; nSphere < aRelevanceSpheres.Length; nSphere++)
    {
      aSphereMatrices[nSphere] = Matrix4x4.TRS(aRelevanceSpheres[nSphere].m_v3Position, Quaternion.Euler(aRelevanceSpheres[nSphere].m_v3Rotation), aRelevanceSpheres[nSphere].m_v3Scale).inverse;
    }

    for (int nVertex = 0; nVertex < aVerticesWorld.Length; nVertex++)
    {
      for (int nSphere = 0; nSphere < aRelevanceSpheres.Length; nSphere++)
      {
        if (aRelevanceSpheres[nSphere].m_bExpanded)
        {
          Vector3 v3VertexSphereLocal = aSphereMatrices[nSphere].MultiplyPoint(aVerticesWorld[nVertex]);

          if (v3VertexSphereLocal.magnitude <= 0.5)
          {
            Gizmos.DrawCube(aVerticesWorld[nVertex], Vector3.one * UnityEditor.HandleUtility.GetHandleSize(aVerticesWorld[nVertex]) * 0.05f);
            break;
          }
        }
      }
    }
  }

#endif

  public static bool HasValidMeshData(GameObject go)
  {
    MeshFilter meshFilter = go.GetComponent<MeshFilter>();

    if (meshFilter != null)
    {
      return true;
    }
    else
    {
      SkinnedMeshRenderer skin = go.GetComponent<SkinnedMeshRenderer>();

      if (skin != null)
      {
        return true;
      }
    }

    return false;
  }

  public static bool IsRootOrBelongsToTree(MeshSimplify meshSimplify, MeshSimplify root)
  {
    if (meshSimplify == null)
    {
      return false;
    }

    return (meshSimplify.m_bExcludedFromTree == false) && ((meshSimplify.m_meshSimplifyRoot == null) || (meshSimplify.m_meshSimplifyRoot == root) || (meshSimplify == root) || (meshSimplify.m_meshSimplifyRoot == root.m_meshSimplifyRoot));
  }

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

    if (meshSimplify == null && HasValidMeshData(gameObject))
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

  private static void ComputeDataRecursive(MeshSimplify root, GameObject gameObject, bool bRecurseIntoChildren, Simplifier.ProgressDelegate progress = null)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify == null && root.m_bGenerateIncludeChildren)
    {
      if (HasValidMeshData(gameObject))
      {
        meshSimplify = gameObject.AddComponent<MeshSimplify>();
        meshSimplify.m_meshSimplifyRoot = root;
        root.m_listDependentChildren.Add(meshSimplify);
      }
    }
    
    if(meshSimplify != null)
    {
      if (IsRootOrBelongsToTree(meshSimplify, root))
      {
        meshSimplify.FreeData(false);

        MeshFilter meshFilter = meshSimplify.GetComponent<MeshFilter>();

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
          if (meshFilter.sharedMesh.vertexCount > 0)
          {
            if (meshSimplify.m_originalMesh == null)
            {
              meshSimplify.m_originalMesh = meshFilter.sharedMesh;
            }

            Simplifier[] simplifiers = meshSimplify.GetComponents<Simplifier>();

            for (int c = 0; c < simplifiers.Length; c++)
            {
              if (Application.isEditor && Application.isPlaying == false)
              {
                DestroyImmediate(simplifiers[c]);
              }
              else
              {
                Destroy(simplifiers[c]);
              }
            }

            meshSimplify.m_meshSimplifier = meshSimplify.gameObject.AddComponent<Simplifier>();
            meshSimplify.m_meshSimplifier.hideFlags = HideFlags.HideInInspector;
            meshSimplify.ConfigureSimplifier();

            IEnumerator enumerator = meshSimplify.m_meshSimplifier.ProgressiveMesh(gameObject, meshSimplify.m_originalMesh, root.m_aRelevanceSpheres, meshSimplify.name, progress);

            while (enumerator.MoveNext())
            {
              if (Simplifier.Cancelled)
              {
                return;
              }
            }

            if (Simplifier.Cancelled)
            {
              return;
            }
          }
        }
        else
        {
          SkinnedMeshRenderer skin = meshSimplify.GetComponent<SkinnedMeshRenderer>();

          if (skin != null)
          {
            if (skin.sharedMesh.vertexCount > 0)
            {
              if (meshSimplify.m_originalMesh == null)
              {
                meshSimplify.m_originalMesh = skin.sharedMesh;
              }

              Simplifier[] simplifiers = meshSimplify.GetComponents<Simplifier>();

              for (int c = 0; c < simplifiers.Length; c++)
              {
                if (Application.isEditor && Application.isPlaying == false)
                {
                  DestroyImmediate(simplifiers[c]);
                }
                else
                {
                  Destroy(simplifiers[c]);
                }
              }

              meshSimplify.m_meshSimplifier = meshSimplify.gameObject.AddComponent<Simplifier>();
              meshSimplify.m_meshSimplifier.hideFlags = HideFlags.HideInInspector;
              meshSimplify.ConfigureSimplifier();

              IEnumerator enumerator = meshSimplify.m_meshSimplifier.ProgressiveMesh(gameObject, meshSimplify.m_originalMesh, root.m_aRelevanceSpheres, meshSimplify.name, progress);

              while (enumerator.MoveNext())
              {
                if (Simplifier.Cancelled)
                {
                  return;
                }
              }

              if (Simplifier.Cancelled)
              {
                return;
              }
            }
          }
        }

        meshSimplify.m_bDataDirty = false;
      }
    }

    if(bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        ComputeDataRecursive(root, gameObject.transform.GetChild(nChild).gameObject, bRecurseIntoChildren, progress);

        if (Simplifier.Cancelled)
        {
          return;
        }
      }
    }
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

  private static void ComputeMeshRecursive(MeshSimplify root, GameObject gameObject, bool bRecurseIntoChildren, Simplifier.ProgressDelegate progress = null)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify != null)
    {
      if (IsRootOrBelongsToTree(meshSimplify, root))
      {
        if (meshSimplify.m_meshSimplifier != null)
        {
          if (meshSimplify.m_simplifiedMesh)
          {
            meshSimplify.m_simplifiedMesh.Clear();
          }

          float fAmount = meshSimplify.m_fVertexAmount;

          if (meshSimplify.m_bOverrideRootSettings == false && meshSimplify.m_meshSimplifyRoot != null)
          {
            fAmount = meshSimplify.m_meshSimplifyRoot.m_fVertexAmount;
          }

          if (meshSimplify.m_simplifiedMesh == null)
          {
            meshSimplify.m_simplifiedMesh = CreateNewEmptyMesh(meshSimplify);
          }

          meshSimplify.ConfigureSimplifier();

          IEnumerator enumerator = meshSimplify.m_meshSimplifier.ComputeMeshWithVertexCount(gameObject, meshSimplify.m_simplifiedMesh, Mathf.RoundToInt(fAmount * meshSimplify.m_meshSimplifier.GetOriginalMeshUniqueVertexCount()), meshSimplify.name + " Simplified", progress);

          while (enumerator.MoveNext())
          {
            if (Simplifier.Cancelled)
            {
              return;
            }
          }

          if (Simplifier.Cancelled)
          {
            return;
          }
        }
      }
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        ComputeMeshRecursive(root, gameObject.transform.GetChild(nChild).gameObject, bRecurseIntoChildren, progress);

        if (Simplifier.Cancelled)
        {
          return;
        }
      }
    }
  }

  public void AssignSimplifiedMesh(bool bRecurseIntoChildren)
  {
    AssignSimplifiedMeshRecursive(this, this.gameObject, bRecurseIntoChildren);
  }

  private static void AssignSimplifiedMeshRecursive(MeshSimplify root, GameObject gameObject, bool bRecurseIntoChildren)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify != null)
    {
      if (IsRootOrBelongsToTree(meshSimplify, root))
      {
        if (meshSimplify.m_simplifiedMesh != null)
        {
          MeshFilter meshFilter = meshSimplify.GetComponent<MeshFilter>();

          if (meshFilter != null)
          {
            meshFilter.sharedMesh = meshSimplify.m_simplifiedMesh;
          }
          else
          {
            SkinnedMeshRenderer skin = meshSimplify.GetComponent<SkinnedMeshRenderer>();

            if (skin != null)
            {
              skin.sharedMesh = meshSimplify.m_simplifiedMesh;
            }
          }
        }
      }
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        AssignSimplifiedMeshRecursive(root, gameObject.transform.GetChild(nChild).gameObject, bRecurseIntoChildren);
      }
    }
  }

  public void RestoreOriginalMesh(bool bDeleteData, bool bRecurseIntoChildren)
  {
    RestoreOriginalMeshRecursive(this, this.gameObject, bDeleteData, bRecurseIntoChildren);
  }

  private static void RestoreOriginalMeshRecursive(MeshSimplify root, GameObject gameObject, bool bDeleteData, bool bRecurseIntoChildren)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify != null)
    {
      if (IsRootOrBelongsToTree(meshSimplify, root))
      {
        if (meshSimplify.m_originalMesh != null)
        {
          MeshFilter meshFilter = meshSimplify.GetComponent<MeshFilter>();

          if (meshFilter != null)
          {
            meshFilter.sharedMesh = meshSimplify.m_originalMesh;
          }
          else
          {
            SkinnedMeshRenderer skin = meshSimplify.GetComponent<SkinnedMeshRenderer>();

            if (skin != null)
            {
              skin.sharedMesh = meshSimplify.m_originalMesh;
            }
          }
        }

        if (bDeleteData)
        {
          meshSimplify.FreeData(false);
          meshSimplify.m_listDependentChildren.Clear();
        }
      }
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        RestoreOriginalMeshRecursive(root, gameObject.transform.GetChild(nChild).gameObject, bDeleteData, bRecurseIntoChildren);
      }
    }
  }

  public bool HasOriginalMeshActive(bool bRecurseIntoChildren)
  {
    return HasOriginalMeshActiveRecursive(this, this.gameObject, bRecurseIntoChildren);
  }

  private static bool HasOriginalMeshActiveRecursive(MeshSimplify root, GameObject gameObject, bool bRecurseIntoChildren)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    bool bHasOriginalMeshActive = false;

    if (meshSimplify != null)
    {
      if (IsRootOrBelongsToTree(meshSimplify, root))
      {
        if (meshSimplify.m_originalMesh != null)
        {
          MeshFilter meshFilter = meshSimplify.GetComponent<MeshFilter>();

          if (meshFilter != null)
          {
            if(meshFilter.sharedMesh == meshSimplify.m_originalMesh)
            {
              bHasOriginalMeshActive = true;
            }
          }
          else
          {
            SkinnedMeshRenderer skin = meshSimplify.GetComponent<SkinnedMeshRenderer>();

            if (skin != null)
            {
              if(skin.sharedMesh == meshSimplify.m_originalMesh)
              {
                bHasOriginalMeshActive = true;
              }
            }
          }
        }
      }
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        bHasOriginalMeshActive = bHasOriginalMeshActive || HasOriginalMeshActiveRecursive(root, gameObject.transform.GetChild(nChild).gameObject, bRecurseIntoChildren);
      }
    }

    return bHasOriginalMeshActive;
  }

  public bool HasVertexData(bool bRecurseIntoChildren)
  {
    return HasVertexDataRecursive(this, this.gameObject, bRecurseIntoChildren);
  }

  private static bool HasVertexDataRecursive(MeshSimplify root, GameObject gameObject, bool bRecurseIntoChildren)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify != null)
    {
      if (IsRootOrBelongsToTree(meshSimplify, root))
      {
        if (meshSimplify.m_simplifiedMesh && meshSimplify.m_simplifiedMesh.vertexCount > 0)
        {
          return true;
        }
      }
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        if (HasVertexDataRecursive(root, gameObject.transform.GetChild(nChild).gameObject, bRecurseIntoChildren))
        {
          return true;
        }
      }
    }

    return false;
  }

  public int GetOriginalVertexCount(bool bRecurseIntoChildren)
  {
    int nVertexCount = 0;
    GetOriginalVertexCountRecursive(this, this.gameObject, ref nVertexCount, bRecurseIntoChildren);
    return nVertexCount;
  }

  private static void GetOriginalVertexCountRecursive(MeshSimplify root, GameObject gameObject, ref int nVertexCount, bool bRecurseIntoChildren)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify != null)
    {
      if (IsRootOrBelongsToTree(meshSimplify, root))
      {
        if (meshSimplify.m_originalMesh != null)
        {
          nVertexCount += meshSimplify.m_originalMesh.vertexCount;
        }
      }
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        GetOriginalVertexCountRecursive(root, gameObject.transform.GetChild(nChild).gameObject, ref nVertexCount, bRecurseIntoChildren);
      }
    }
  }

  public int GetOriginalTriangleCount(bool bRecurseIntoChildren)
  {
    int nTriangleCount = 0;
    GetOriginalTriangleCountRecursive(this, this.gameObject, ref nTriangleCount, bRecurseIntoChildren);
    return nTriangleCount;
  }

  private static void GetOriginalTriangleCountRecursive(MeshSimplify root, GameObject gameObject, ref int nTriangleCount, bool bRecurseIntoChildren)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify != null)
    {
      if (IsRootOrBelongsToTree(meshSimplify, root))
      {
        if (meshSimplify.m_originalMesh != null)
        {
          nTriangleCount += meshSimplify.m_originalMesh.triangles.Length / 3;
        }
      }
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        GetOriginalTriangleCountRecursive(root, gameObject.transform.GetChild(nChild).gameObject, ref nTriangleCount, bRecurseIntoChildren);
      }
    }
  }

  public int GetSimplifiedVertexCount(bool bRecurseIntoChildren)
  {
    int nVertexCount = 0;
    GetSimplifiedVertexCountRecursive(this, this.gameObject, ref nVertexCount, bRecurseIntoChildren);
    return nVertexCount;
  }

  private static void GetSimplifiedVertexCountRecursive(MeshSimplify root, GameObject gameObject, ref int nVertexCount, bool bRecurseIntoChildren)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify != null)
    {
      if (IsRootOrBelongsToTree(meshSimplify, root))
      {
        if (meshSimplify.m_simplifiedMesh != null)
        {
          nVertexCount += meshSimplify.m_simplifiedMesh.vertexCount;
        }
      }
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        GetSimplifiedVertexCountRecursive(root, gameObject.transform.GetChild(nChild).gameObject, ref nVertexCount, bRecurseIntoChildren);
      }
    }
  }

  public int GetSimplifiedTriangleCount(bool bRecurseIntoChildren)
  {
    int nTriangleCount = 0;
    GetSimplifiedTriangleCountRecursive(this, this.gameObject, ref nTriangleCount, bRecurseIntoChildren);
    return nTriangleCount;
  }

  private static void GetSimplifiedTriangleCountRecursive(MeshSimplify root, GameObject gameObject, ref int nTriangleCount, bool bRecurseIntoChildren)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify != null)
    {
      if (IsRootOrBelongsToTree(meshSimplify, root))
      {
        if (meshSimplify.m_simplifiedMesh != null)
        {
          nTriangleCount += meshSimplify.m_simplifiedMesh.triangles.Length / 3;
        }
      }
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        GetSimplifiedTriangleCountRecursive(root, gameObject.transform.GetChild(nChild).gameObject, ref nTriangleCount, bRecurseIntoChildren);
      }
    }
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

  public void FreeData(bool bRecurseIntoChildren)
  {
    FreeDataRecursive(this, this.gameObject, bRecurseIntoChildren);
  }

  private static void FreeDataRecursive(MeshSimplify root, GameObject gameObject, bool bRecurseIntoChildren)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify != null)
    {
      if (IsRootOrBelongsToTree(meshSimplify, root))
      {
        if (meshSimplify.m_simplifiedMesh)
        {
          meshSimplify.m_simplifiedMesh.Clear();
        }

        Simplifier[] simplifiers = gameObject.GetComponents<Simplifier>();

        for (int c = 0; c < simplifiers.Length; c++)
        {
          if (Application.isEditor && Application.isPlaying == false)
          {
            DestroyImmediate(simplifiers[c]);
          }
          else
          {
            Destroy(simplifiers[c]);
          }
        }

        meshSimplify.m_bDataDirty = true;
      }
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        FreeDataRecursive(root, gameObject.transform.GetChild(nChild).gameObject, bRecurseIntoChildren);
      }
    }
  }

  private static Mesh CreateNewEmptyMesh(MeshSimplify meshSimplify)
  {
    if(meshSimplify.m_originalMesh == null)
    {
      return new Mesh();
    }

    Mesh meshOut = Mesh.Instantiate(meshSimplify.m_originalMesh);
    meshOut.Clear();
    return meshOut;
  }

#if UNITY_EDITOR

  public void DisablePrefabUsage(bool bRecurseIntoChildren)
  {
    DisablePrefabUsageRecursive(this, this.gameObject, bRecurseIntoChildren);
  }

  private static void DisablePrefabUsageRecursive(MeshSimplify root, GameObject gameObject, bool bRecurseIntoChildren)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify != null)
    {
      if (IsRootOrBelongsToTree(meshSimplify, root))
      {
        if (meshSimplify.m_simplifiedMesh)
        {
          if (UnityEditor.AssetDatabase.IsMainAsset(meshSimplify.m_simplifiedMesh) || UnityEditor.AssetDatabase.IsSubAsset(meshSimplify.m_simplifiedMesh))
          {
            Mesh newMesh = Instantiate(meshSimplify.m_simplifiedMesh) as Mesh;
            meshSimplify.m_simplifiedMesh = newMesh;
          }
        }

        meshSimplify.m_strAssetPath = null;
      }
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        DisablePrefabUsageRecursive(root, gameObject.transform.GetChild(nChild).gameObject, bRecurseIntoChildren);
      }
    }
  }

#endif
}
