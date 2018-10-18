using Chaos;
using UnityEngine;

public partial class MeshSimplify : MonoBehaviour
{

#if UNITY_EDITOR

    public void OnDrawGizmos()
    {
        if (MeshSimplifyRoot != null)
        {
            if (MeshSimplifyRoot.ExpandRelevanceSpheres == false)
            {
                return;
            }
        }
        else
        {
            if (ExpandRelevanceSpheres == false)
            {
                return;
            }
        }

        Gizmos.color = Color.red;

        RelevanceSphere[] aRelevanceSpheres = MeshSimplifyRoot != null ? MeshSimplifyRoot.RelevanceSpheres : RelevanceSpheres;

        if (aRelevanceSpheres == null)
        {
            return;
        }

        bool bDrawVertices = false;

        for (int i = 0; i < UnityEditor.Selection.gameObjects.Length; i++)
        {
            if (((UnityEditor.Selection.gameObjects[i] == this.gameObject) && MeshSimplifyRoot == null) || ((MeshSimplifyRoot != null) && (UnityEditor.Selection.gameObjects[i] == MeshSimplifyRoot.gameObject)))
            {
                bDrawVertices = true;
            }
        }

        if (bDrawVertices == false)
        {
            return;
        }

        Vector3[] aVerticesWorld = MeshUtil.GetWorldVertices(this.gameObject);

        if(aVerticesWorld == null)
        {
            return;
        }

        Matrix4x4[] aSphereMatrices = new Matrix4x4[aRelevanceSpheres.Length];

        for (int nSphere = 0; nSphere < aRelevanceSpheres.Length; nSphere++)
        {
            aSphereMatrices[nSphere] = Matrix4x4.TRS(aRelevanceSpheres[nSphere].Position, aRelevanceSpheres[nSphere].Rotation, aRelevanceSpheres[nSphere].Scale).inverse;
        }

        for (int nVertex = 0; nVertex < aVerticesWorld.Length; nVertex++)
        {
            for (int nSphere = 0; nSphere < aRelevanceSpheres.Length; nSphere++)
            {
                if (aRelevanceSpheres[nSphere].Expanded)
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
                if (meshSimplify.SimplifiedMesh)
                {
                    if (UnityEditor.AssetDatabase.IsMainAsset(meshSimplify.SimplifiedMesh) || UnityEditor.AssetDatabase.IsSubAsset(meshSimplify.SimplifiedMesh))
                    {
                        Mesh newMesh = Instantiate(meshSimplify.SimplifiedMesh) as Mesh;
                        meshSimplify.SimplifiedMesh = newMesh;
                    }
                }

                meshSimplify.AssetPath = null;
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
