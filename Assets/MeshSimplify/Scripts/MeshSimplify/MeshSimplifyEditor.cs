using Chaos;
using UnityEngine;

public partial class MeshSimplify : MonoBehaviour
{

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

        Vector3[] aVerticesWorld = MeshUtil.GetWorldVertices(this.gameObject);

        if(aVerticesWorld == null)
        {
            return;
        }

        Matrix4x4[] aSphereMatrices = new Matrix4x4[aRelevanceSpheres.Length];

        for (int nSphere = 0; nSphere < aRelevanceSpheres.Length; nSphere++)
        {
            aSphereMatrices[nSphere] = Matrix4x4.TRS(aRelevanceSpheres[nSphere].m_v3Position, aRelevanceSpheres[nSphere].m_q4Rotation, aRelevanceSpheres[nSphere].m_v3Scale).inverse;
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
