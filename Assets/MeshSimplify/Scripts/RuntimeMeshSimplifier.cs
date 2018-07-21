using System.Collections;
using System.Collections.Generic;
using Chaos;
using UltimateGameTools.MeshSimplifier;
using UnityEngine;

[RequireComponent(typeof(MeshSimplify))]
public class RuntimeMeshSimplifier : MonoBehaviour
{
    public string ProgressTitle{ get { return m_strLastTitle; } }
    public string ProgressMessage{ get { return m_strLastMessage; } }
    public int ProgressPercent{ get { return m_nLastProgress; } }
    public bool Finished{ get { return m_bFinished; } }

    public void Simplify(float percent)
    {
        if (m_bFinished == false)
        {
            StartCoroutine(ComputeMeshWithVertices(Mathf.Clamp01(percent / 100.0f)));
        }
    }

    private void Awake()
    {
        m_selectedMeshSimplify = GetComponent<MeshSimplify>();
    
        m_objectMaterials = new Dictionary<GameObject, Material[]>();
        AddMaterials(m_selectedMeshSimplify.gameObject, m_objectMaterials);

        m_bFinished = false;
    }

    private void AddMaterials(GameObject theGameObject, Dictionary<GameObject, Material[]> dicMaterials)
    {
        Renderer theRenderer = theGameObject.GetComponent<Renderer>();

        if (theRenderer != null && theRenderer.sharedMaterials != null && (MeshUtil.HasValidMeshData(theGameObject) || theGameObject.GetComponent<MeshSimplify>() != null))
        {
            dicMaterials.Add(theGameObject, theRenderer.sharedMaterials);
        }

        if (m_selectedMeshSimplify.RecurseIntoChildren)
        {
            for (int i = 0; i < theGameObject.transform.childCount; i++)
            {
                AddMaterials(theGameObject.transform.GetChild(i).gameObject, dicMaterials);
            }
        }
    }

    private void Progress(string strTitle, string strMessage, float fT)
    {
        int nPercent = Mathf.RoundToInt(fT * 100.0f);

        if (nPercent != m_nLastProgress || m_strLastTitle != strTitle || m_strLastMessage != strMessage)
        {
            m_strLastTitle   = strTitle;
            m_strLastMessage = strMessage;
            m_nLastProgress  = nPercent;
            //Debug.Log(strTitle + " " + strMessage + " " + nPercent);
        }
    }

    private IEnumerator ComputeMeshWithVertices(float fAmount)
    {
        Simplifier.CoroutineFrameMiliseconds = 20;

        foreach (KeyValuePair<GameObject, Material[]> pair in m_objectMaterials)
        {
            GameObject go = pair.Key;
            MeshSimplify        meshSimplify = go.GetComponent<MeshSimplify>();
            MeshFilter          meshFilter   = null;
            SkinnedMeshRenderer skin         = null;

            if(meshSimplify == null)
            {
                meshSimplify = go.AddComponent<MeshSimplify>();
                meshSimplify.m_meshSimplifyRoot = m_selectedMeshSimplify;
                m_selectedMeshSimplify.m_listDependentChildren.Add(meshSimplify);
            }

            if(meshSimplify.MeshSimplifier == null)
            {
                meshSimplify.MeshSimplifier = meshSimplify.gameObject.AddComponent<Simplifier>();
                meshSimplify.MeshSimplifier.hideFlags = HideFlags.HideInInspector;
                meshSimplify.ConfigureSimplifier();
            }

            if (meshSimplify && ((skin = go.GetComponent<SkinnedMeshRenderer>()) != null || (meshFilter = go.GetComponent<MeshFilter>()) != null))
            {
                Mesh newMesh = null;
                if (null != skin)
                {
                    newMesh = Mesh.Instantiate(skin.sharedMesh);
                }
                else// if(null != meshFilter)
                {
                    newMesh = Mesh.Instantiate(meshFilter.sharedMesh);
                }
                 

                if (meshSimplify.HasData() == false)
                {
                    meshSimplify.MeshSimplifier.CoroutineEnded = false;

                    StartCoroutine(meshSimplify.MeshSimplifier.ProgressiveMesh(go, meshFilter != null ? meshFilter.sharedMesh : skin.sharedMesh, null, meshSimplify.name, Progress));

                    while (meshSimplify.MeshSimplifier.CoroutineEnded == false)
                    {
                        yield return null;
                    }
                }

                if (meshSimplify.MeshSimplifier != null)
                {
                    meshSimplify.MeshSimplifier.CoroutineEnded = false;

                    meshSimplify.MeshSimplifier.ComputeMeshWithVertexCount(go, newMesh, Mathf.RoundToInt(fAmount * meshSimplify.MeshSimplifier.GetOriginalMeshUniqueVertexCount()));

                    while (meshSimplify.MeshSimplifier.CoroutineEnded == false)
                    {
                        yield return null;
                    }

                    if (skin != null)
                    {
                        skin.sharedMesh = newMesh;
                    }
                    else// if (meshFilter != null)
                    {
                        meshFilter.mesh = newMesh;
                    }

                    meshSimplify.m_simplifiedMesh = newMesh;
                }
            }
        }

        m_bFinished = true;
    }

    private Dictionary<GameObject, Material[]> m_objectMaterials;
    private MeshSimplify m_selectedMeshSimplify;

    private bool   m_bFinished      = false;
    private Mesh   m_newMesh;
    private int    m_nLastProgress  = -1;
    private string m_strLastTitle   = "";
    private string m_strLastMessage = "";
}
