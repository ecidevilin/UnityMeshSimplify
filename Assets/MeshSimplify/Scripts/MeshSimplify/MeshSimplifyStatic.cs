using System.Collections;
using System.Collections.Generic;
using Chaos;
using UltimateGameTools.MeshSimplifier;
using UnityEngine;

public partial class MeshSimplify : MonoBehaviour
{
    private static bool HasNonMeshSimplifyGameObjectsInTreeRecursive(MeshSimplify root, GameObject gameObject)
    {
        MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

        if (meshSimplify == null && MeshUtil.HasValidMeshData(gameObject))
        {
            return true;
        }

        for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
        {
            if (HasNonMeshSimplifyGameObjectsInTreeRecursive(root, gameObject.transform.GetChild(nChild).gameObject))
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
    private static void ComputeDataRecursive(MeshSimplify root, GameObject gameObject, bool bRecurseIntoChildren, Simplifier.ProgressDelegate progress = null)
    {
        MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

        if (meshSimplify == null && root.m_bGenerateIncludeChildren)
        {
            if (MeshUtil.HasValidMeshData(gameObject))
            {
                meshSimplify = gameObject.AddComponent<MeshSimplify>();
                meshSimplify.m_meshSimplifyRoot = root;
                root.m_listDependentChildren.Add(meshSimplify);
            }
        }

        if (meshSimplify != null)
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

        if (bRecurseIntoChildren)
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

                    meshSimplify.m_meshSimplifier.ComputeMeshWithVertexCount(gameObject, meshSimplify.m_simplifiedMesh, Mathf.RoundToInt(fAmount * meshSimplify.m_meshSimplifier.GetOriginalMeshUniqueVertexCount()));
                    
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
    //private static bool HasOriginalMeshActiveRecursive(MeshSimplify root, GameObject gameObject, bool bRecurseIntoChildren)
    //{
    //    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    //    bool bHasOriginalMeshActive = false;

    //    if (meshSimplify != null)
    //    {
    //        if (IsRootOrBelongsToTree(meshSimplify, root))
    //        {
    //            if (meshSimplify.m_originalMesh != null)
    //            {
    //                MeshFilter meshFilter = meshSimplify.GetComponent<MeshFilter>();

    //                if (meshFilter != null)
    //                {
    //                    if (meshFilter.sharedMesh == meshSimplify.m_originalMesh)
    //                    {
    //                        bHasOriginalMeshActive = true;
    //                    }
    //                }
    //                else
    //                {
    //                    SkinnedMeshRenderer skin = meshSimplify.GetComponent<SkinnedMeshRenderer>();

    //                    if (skin != null)
    //                    {
    //                        if (skin.sharedMesh == meshSimplify.m_originalMesh)
    //                        {
    //                            bHasOriginalMeshActive = true;
    //                        }
    //                    }
    //                }
    //            }
    //        }
    //    }

    //    if (bRecurseIntoChildren)
    //    {
    //        for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
    //        {
    //            bHasOriginalMeshActive = bHasOriginalMeshActive || HasOriginalMeshActiveRecursive(root, gameObject.transform.GetChild(nChild).gameObject, bRecurseIntoChildren);
    //        }
    //    }

    //    return bHasOriginalMeshActive;
    //}
    //private static bool HasVertexDataRecursive(MeshSimplify root, GameObject gameObject, bool bRecurseIntoChildren)
    //{
    //    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    //    if (meshSimplify != null)
    //    {
    //        if (IsRootOrBelongsToTree(meshSimplify, root))
    //        {
    //            if (meshSimplify.m_simplifiedMesh && meshSimplify.m_simplifiedMesh.vertexCount > 0)
    //            {
    //                return true;
    //            }
    //        }
    //    }

    //    if (bRecurseIntoChildren)
    //    {
    //        for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
    //        {
    //            if (HasVertexDataRecursive(root, gameObject.transform.GetChild(nChild).gameObject, bRecurseIntoChildren))
    //            {
    //                return true;
    //            }
    //        }
    //    }

    //    return false;
    //}
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
                        DestroyImmediate(simplifiers[c], true);
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
        if (meshSimplify.m_originalMesh == null)
        {
            return new Mesh();
        }

        Mesh meshOut = Mesh.Instantiate(meshSimplify.m_originalMesh);
        meshOut.Clear();
        return meshOut;
    }
}
