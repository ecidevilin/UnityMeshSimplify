using System;
using UnityEngine;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UltimateGameTools.MeshSimplifier;
using UnityEngine.Profiling;

public class SimplifyMeshPreview : MonoBehaviour
{
    [Serializable]
    public class ShowcaseObject
    {
        public MeshSimplify m_meshSimplify;
        public Vector3 m_position;
        public Vector3 m_angles;
        public Vector3 m_rotationAxis = Vector3.up;
        //public string m_description;
    }

    public ShowcaseObject[] ShowcaseObjects;
    public Material WireframeMaterial;
    public float MouseSensitvity = 0.3f;
    public float MouseReleaseSpeed = 3.0f;

    void Start()
    {
        if (ShowcaseObjects != null)
        {
            //for (int i = 0; i < ShowcaseObjects.Length; i++)
            //{
            //    ShowcaseObjects[i].m_description = ShowcaseObjects[i].m_description.Replace("\\n", Environment.NewLine);
            //}

            SetActiveObject(0);
        }

        Simplifier.CoroutineFrameMiliseconds = 20;
    }

    void Progress(string strTitle, string strMessage, float fT)
    {
        int nPercent = Mathf.RoundToInt(fT * 100.0f);

        if (nPercent != m_nLastProgress || m_strLastTitle != strTitle || m_strLastMessage != strMessage)
        {
            m_strLastTitle = strTitle;
            m_strLastMessage = strMessage;
            m_nLastProgress = nPercent;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            m_bGUIEnabled = !m_bGUIEnabled;
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            m_bWireframe = !m_bWireframe;
            SetWireframe(m_bWireframe);
        }

        if (m_selectedMeshSimplify != null)
        {
            if (Input.GetMouseButton(0) && Input.mousePosition.y > 100)
            {
                Vector3 v3Angles = ShowcaseObjects[m_nSelectedIndex].m_rotationAxis * -((Input.mousePosition.x - m_fLastMouseX) * MouseSensitvity);
                m_selectedMeshSimplify.transform.Rotate(v3Angles, Space.Self);
            }
            else if (Input.GetMouseButtonUp(0) && Input.mousePosition.y > 100)
            {
                m_fRotationSpeed = -(Input.mousePosition.x - m_fLastMouseX) * MouseReleaseSpeed;
            }
            else
            {
                Vector3 v3Angles = ShowcaseObjects[m_nSelectedIndex].m_rotationAxis * (m_fRotationSpeed * Time.deltaTime);
                m_selectedMeshSimplify.transform.Rotate(v3Angles, Space.Self);
            }
        }

        m_fLastMouseX = Input.mousePosition.x;
    }

    void OnGUI()
    {
        // Main menu

        if (m_bGUIEnabled == false)
        {
            return;
        }

        int nWidth = 400;

        if (ShowcaseObjects == null)
        {
            return;
        }

        bool bAllowInteract = (string.IsNullOrEmpty(m_strLastTitle) || string.IsNullOrEmpty(m_strLastMessage));

        GUI.Box(new Rect(0, 0, nWidth + 10, 240), "");
        GUILayout.Space(20);
        GUILayout.Label("Select model:", GUILayout.Width(nWidth));

        GUILayout.BeginHorizontal();

        for (int i = 0; i < ShowcaseObjects.Length; i++)
        {
            if (GUILayout.Button(ShowcaseObjects[i].m_meshSimplify.name) && bAllowInteract)
            {
                if (m_selectedMeshSimplify != null)
                {
                    DestroyImmediate(m_selectedMeshSimplify.gameObject);
                }

                SetActiveObject(i);
            }
        }

        GUILayout.EndHorizontal();

        if (m_selectedMeshSimplify != null)
        {
            //GUILayout.Space(20);
            //GUILayout.Label(ShowcaseObjects[m_nSelectedIndex].m_description);
            //GUILayout.Space(20);

            GUI.changed = false;
            m_bWireframe = GUILayout.Toggle(m_bWireframe, "Show wireframe");

            if (GUI.changed && m_selectedMeshSimplify != null)
            {
                SetWireframe(m_bWireframe);
            }

            GUILayout.Space(20);

            int nSimplifiedVertices = m_selectedMeshSimplify.GetSimplifiedTriangleCount(true);
            int nTotalVertices = m_selectedMeshSimplify.GetOriginalTriangleCount(true);

            GUILayout.Label("Triangle count: " + nSimplifiedVertices + "/" + nTotalVertices + " " + (Mathf.RoundToInt(((float)nSimplifiedVertices / (float)nTotalVertices) * 100.0f).ToString() + "% from original"));

            GUILayout.Space(20);

            if (!string.IsNullOrEmpty(m_strLastTitle) && !string.IsNullOrEmpty(m_strLastMessage))
            {
                GUILayout.Label(m_strLastTitle + ": " + m_strLastMessage, GUILayout.MaxWidth(nWidth));
                GUI.color = Color.blue;

                Rect lastRect = GUILayoutUtility.GetLastRect();
                GUI.Box(new Rect(10, lastRect.yMax + 5, 204, 24), "");
                GUI.Box(new Rect(12, lastRect.yMax + 7, m_nLastProgress * 2, 20), "");
            }
            else
            {
                GUILayout.Label("Vertices: " + (m_fVertexAmount * 100.0f).ToString("0.00") + "%");
                float VertexAmount = GUILayout.HorizontalSlider(m_fVertexAmount, 0.0f, 1.0f, GUILayout.Width(200));
                if (!Mathf.Approximately(VertexAmount, m_fVertexAmount))
                {
                    m_fVertexAmount = VertexAmount;
                    ComputeMeshWithVertices(m_fVertexAmount);
                }

                //GUILayout.BeginHorizontal();
                //GUILayout.Space(3);

                //if (GUILayout.Button("Compute simplified mesh", GUILayout.Width(200)))
                //{
                //    ComputeMeshWithVertices(m_fVertexAmount);
                //}

                //GUILayout.FlexibleSpace();
                //GUILayout.EndHorizontal();
            }
        }
    }

    private void SetActiveObject(int index)
    {
        m_nSelectedIndex = index;

        MeshSimplify meshSimplify = Instantiate(ShowcaseObjects[index].m_meshSimplify);
        meshSimplify.transform.position = ShowcaseObjects[index].m_position;
        meshSimplify.transform.rotation = Quaternion.Euler(ShowcaseObjects[index].m_angles);

        m_selectedMeshSimplify = meshSimplify;

        m_objectMaterials = new Dictionary<GameObject, Material[]>();
        AddMaterials(meshSimplify.gameObject, m_objectMaterials);

        m_bWireframe = false;
    }

    private void AddMaterials(GameObject theGameObject, Dictionary<GameObject, Material[]> dicMaterials)
    {
        Renderer theRenderer = theGameObject.GetComponent<Renderer>();
        MeshSimplify meshSimplify = theGameObject.GetComponent<MeshSimplify>();

        if (theRenderer != null && theRenderer.sharedMaterials != null && meshSimplify != null)
        {
            dicMaterials.Add(theGameObject, theRenderer.sharedMaterials);
        }

        for (int i = 0; i < theGameObject.transform.childCount; i++)
        {
            AddMaterials(theGameObject.transform.GetChild(i).gameObject, dicMaterials);
        }
    }

    private void SetWireframe(bool bEnabled)
    {
        m_bWireframe = bEnabled;

        foreach (KeyValuePair<GameObject, Material[]> pair in m_objectMaterials)
        {
            Renderer theRenderer = pair.Key.GetComponent<Renderer>();

            if (bEnabled)
            {
                Material[] materials = new Material[pair.Value.Length];

                for (int i = 0; i < pair.Value.Length; i++)
                {
                    materials[i] = WireframeMaterial;
                }

                theRenderer.sharedMaterials = materials;
            }
            else
            {
                theRenderer.sharedMaterials = pair.Value;
            }
        }
    }

    private void ComputeMeshWithVertices(float fAmount)
    {
        foreach (KeyValuePair<GameObject, Material[]> pair in m_objectMaterials)
        {
            GameObject go = pair.Key;
            MeshSimplify meshSimplify = go.GetComponent<MeshSimplify>();
            MeshFilter meshFilter = null;
            SkinnedMeshRenderer skin;

            if (meshSimplify && ((skin = go.GetComponent<SkinnedMeshRenderer>()) != null || (meshFilter = go.GetComponent<MeshFilter>()) != null))
            {
                Mesh newMesh = null;
                if (skin != null)
                {
                    newMesh = new Mesh();//Mesh.Instantiate(skin.sharedMesh);
                }
                else// if (meshFilter != null)
                {
                    newMesh = new Mesh(); //Mesh.Instantiate(meshFilter.sharedMesh);
                }

                if (meshSimplify.MeshSimplifier != null)
                {
                    Profiler.BeginSample("ComputeMeshWithVertexCount");
                    meshSimplify.MeshSimplifier.ComputeMeshWithVertexCount(go, newMesh, Mathf.RoundToInt(fAmount * meshSimplify.MeshSimplifier.GetOriginalMeshUniqueVertexCount()));
                    Profiler.EndSample();
                    
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

        m_strLastTitle = "";
        m_strLastMessage = "";
        m_nLastProgress = 0;
    }

    Dictionary<GameObject, Material[]> m_objectMaterials;
    MeshSimplify m_selectedMeshSimplify;
    int m_nSelectedIndex = -1;
    bool m_bWireframe;
    float m_fRotationSpeed = 10.0f;
    float m_fLastMouseX;

    Mesh m_newMesh;
    int m_nLastProgress = -1;
    string m_strLastTitle = "";
    string m_strLastMessage = "";

    float m_fVertexAmount = 1.0f;

    bool m_bGUIEnabled = true;

}
