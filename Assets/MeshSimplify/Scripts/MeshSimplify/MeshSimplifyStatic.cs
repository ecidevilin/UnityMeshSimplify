using System.Collections;
using Chaos;
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

        return (meshSimplify._excludedFromTree == false) && ((meshSimplify.MeshSimplifyRoot == null) || (meshSimplify.MeshSimplifyRoot == root) || (meshSimplify == root) || (meshSimplify.MeshSimplifyRoot == root.MeshSimplifyRoot));
    }
    private static void ComputeDataRecursive(MeshSimplify root, GameObject gameObject, bool bRecurseIntoChildren, Simplifier.ProgressDelegate progress = null)
    {
        MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

        if (meshSimplify == null && root._generateIncludeChildren)
        {
            if (MeshUtil.HasValidMeshData(gameObject))
            {
                meshSimplify = gameObject.AddComponent<MeshSimplify>();
                meshSimplify.MeshSimplifyRoot = root;
                root.ListDependentChildren.Add(meshSimplify);
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
                        if (meshSimplify.OriginalMesh == null)
                        {
                            meshSimplify.OriginalMesh = meshFilter.sharedMesh;
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

                        meshSimplify._meshSimplifier = meshSimplify.gameObject.AddComponent<Simplifier>();
                        meshSimplify._meshSimplifier.hideFlags = HideFlags.HideInInspector;
                        meshSimplify.ConfigureSimplifier();

                        IEnumerator enumerator = meshSimplify._meshSimplifier.ProgressiveMesh(gameObject, meshSimplify.OriginalMesh, root.RelevanceSpheres, meshSimplify.name, progress);

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
                            if (meshSimplify.OriginalMesh == null)
                            {
                                meshSimplify.OriginalMesh = skin.sharedMesh;
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

                            meshSimplify._meshSimplifier = meshSimplify.gameObject.AddComponent<Simplifier>();
                            meshSimplify._meshSimplifier.hideFlags = HideFlags.HideInInspector;
                            meshSimplify.ConfigureSimplifier();

                            IEnumerator enumerator = meshSimplify._meshSimplifier.ProgressiveMesh(gameObject, meshSimplify.OriginalMesh, root.RelevanceSpheres, meshSimplify.name, progress);

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

                meshSimplify._dataDirty = false;
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
                if (meshSimplify._meshSimplifier != null)
                {
                    if (meshSimplify.SimplifiedMesh)
                    {
                        meshSimplify.SimplifiedMesh.Clear();
                    }

                    float fAmount = meshSimplify.VertexAmount;

                    if (meshSimplify._overrideRootSettings == false && meshSimplify.MeshSimplifyRoot != null)
                    {
                        fAmount = meshSimplify.MeshSimplifyRoot.VertexAmount;
                    }

                    if (meshSimplify.SimplifiedMesh == null)
                    {
                        meshSimplify.SimplifiedMesh = CreateNewEmptyMesh(meshSimplify);
                    }

                    meshSimplify.ConfigureSimplifier();

                    meshSimplify._meshSimplifier.ComputeMeshWithVertexCount(gameObject, meshSimplify.SimplifiedMesh, Mathf.RoundToInt(fAmount * meshSimplify._meshSimplifier.GetOriginalMeshUniqueVertexCount()));
                    
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
                if (meshSimplify.SimplifiedMesh != null)
                {
                    MeshFilter meshFilter = meshSimplify.GetComponent<MeshFilter>();

                    if (meshFilter != null)
                    {
                        meshFilter.sharedMesh = meshSimplify.SimplifiedMesh;
                    }
                    else
                    {
                        SkinnedMeshRenderer skin = meshSimplify.GetComponent<SkinnedMeshRenderer>();

                        if (skin != null)
                        {
                            skin.sharedMesh = meshSimplify.SimplifiedMesh;
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
                if (meshSimplify.OriginalMesh != null)
                {
                    MeshFilter meshFilter = meshSimplify.GetComponent<MeshFilter>();

                    if (meshFilter != null)
                    {
                        meshFilter.sharedMesh = meshSimplify.OriginalMesh;
                    }
                    else
                    {
                        SkinnedMeshRenderer skin = meshSimplify.GetComponent<SkinnedMeshRenderer>();

                        if (skin != null)
                        {
                            skin.sharedMesh = meshSimplify.OriginalMesh;
                        }
                    }
                }

                if (bDeleteData)
                {
                    meshSimplify.FreeData(false);
                    meshSimplify.ListDependentChildren.Clear();
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
                if (meshSimplify.OriginalMesh != null)
                {
                    nVertexCount += meshSimplify.OriginalMesh.vertexCount;
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
                if (meshSimplify.OriginalMesh != null)
                {
                    nTriangleCount += meshSimplify.OriginalMesh.triangles.Length / 3;
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
                if (meshSimplify.SimplifiedMesh != null)
                {
                    nVertexCount += meshSimplify.SimplifiedMesh.vertexCount;
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
                if (meshSimplify.SimplifiedMesh != null)
                {
                    nTriangleCount += meshSimplify.SimplifiedMesh.triangles.Length / 3;
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
                if (meshSimplify.SimplifiedMesh)
                {
                    meshSimplify.SimplifiedMesh.Clear();
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

                meshSimplify._dataDirty = true;
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
        if (meshSimplify.OriginalMesh == null)
        {
            return new Mesh();
        }

        Mesh meshOut = Mesh.Instantiate(meshSimplify.OriginalMesh);
        meshOut.Clear();
        return meshOut;
    }
}
