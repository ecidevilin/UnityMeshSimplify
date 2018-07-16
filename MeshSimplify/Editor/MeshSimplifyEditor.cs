using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using UltimateGameTools.MeshSimplifier;

[CustomEditor(typeof(MeshSimplify)), CanEditMultipleObjects]
public class MeshSimplifyEditor : Editor
{
  void Progress(string strTitle, string strMessage, float fT)
  {
    int nPercent = Mathf.RoundToInt(fT * 100.0f);

    if(nPercent != s_nLastProgress || s_strLastTitle != strTitle || s_strLastMessage != strMessage)
    {
      s_strLastTitle   = strTitle;
      s_strLastMessage = strMessage;
      s_nLastProgress  = nPercent;

      if(EditorUtility.DisplayCancelableProgressBar(strTitle, strMessage, fT))
      {
        Simplifier.Cancelled = true;
      }
    }
  }

  void OnEnable()
  {
    PropertyGenerateIncludeChildren   = serializedObject.FindProperty("m_bGenerateIncludeChildren");
    PropertyEnablePrefabUsage         = serializedObject.FindProperty("m_bEnablePrefabUsage");
    PropertyExpandRelevanceSpheres    = serializedObject.FindProperty("m_bExpandRelevanceSpheres");
    PropertyRelevanceSpheres          = serializedObject.FindProperty("m_aRelevanceSpheres");
    PropertyOverrideRootSettings      = serializedObject.FindProperty("m_bOverrideRootSettings");
    PropertyVertexAmount              = serializedObject.FindProperty("m_fVertexAmount");
    PropertyUseEdgeLength             = serializedObject.FindProperty("m_bUseEdgeLength");
    PropertyUseCurvature              = serializedObject.FindProperty("m_bUseCurvature");
    PropertyProtectTexture            = serializedObject.FindProperty("m_bProtectTexture");
    PropertyLockBorder                = serializedObject.FindProperty("m_bLockBorder");
    PropertyDataDirty                 = serializedObject.FindProperty("m_bDataDirty");
    PropertyExcludedFromTree          = serializedObject.FindProperty("m_bExcludedFromTree");

    m_bComputeMesh             = false;
    m_bEnablePrefabUsage       = false;
    m_bDisablePrefabUsage      = false;
    m_bDeleteData              = false;
    m_bRemoveFromTree          = false;
    m_bSetupNewRelevanceSphere = false;

    SetHideFlags();
  }

  void OnDisable()
  {
    if(m_bPreviewOriginalMesh)
    {
      foreach (Object targetObject in targets)
      {
        if (targetObject != null)
        {
          MeshSimplify meshSimplify = targetObject as MeshSimplify;
          meshSimplify.AssignSimplifiedMesh(true);
        }
      }
    }
  }

  void OnSceneGUI()
  {
    MeshSimplify meshSimplify = target as MeshSimplify;

    bool bDrawSpheres = true;

    if (meshSimplify.m_meshSimplifyRoot != null)
    {
      if (meshSimplify.m_meshSimplifyRoot.m_bExpandRelevanceSpheres == false)
      {
        bDrawSpheres = false;
      }
    }
    else
    {
      if (meshSimplify.m_bExpandRelevanceSpheres == false)
      {
        bDrawSpheres = false;
      }
    }

    if (meshSimplify.m_aRelevanceSpheres != null && bDrawSpheres)
    {
      for (int nSphere = 0; nSphere < meshSimplify.m_aRelevanceSpheres.Length; nSphere++)
      {
        if (meshSimplify.m_aRelevanceSpheres[nSphere].m_bExpanded == false)
        {
          continue;
        }

        RelevanceSphere relevanceSphere = meshSimplify.m_aRelevanceSpheres[nSphere] as RelevanceSphere;

        if (Tools.current == Tool.Move)
        {
          EditorGUI.BeginChangeCheck();
          Vector3 v3Position = Handles.PositionHandle(relevanceSphere.m_v3Position, Quaternion.Euler(relevanceSphere.m_v3Rotation));
          if (EditorGUI.EndChangeCheck())
          {
            Undo.RecordObject(meshSimplify, "Move Relevance Sphere");
            relevanceSphere.m_v3Position = v3Position;
            meshSimplify.RestoreOriginalMesh(false, true);
            meshSimplify.SetDataDirty(true);
            EditorUtility.SetDirty(target);
          }
        }
        else if (Tools.current == Tool.Rotate)
        {
          EditorGUI.BeginChangeCheck();
          Quaternion qRotation = Handles.RotationHandle(Quaternion.Euler(relevanceSphere.m_v3Rotation), relevanceSphere.m_v3Position);
          if (EditorGUI.EndChangeCheck())
          {
            Undo.RecordObject(meshSimplify, "Rotate Relevance Sphere");
            relevanceSphere.m_v3Rotation = qRotation.eulerAngles;
            meshSimplify.RestoreOriginalMesh(false, true);
            meshSimplify.SetDataDirty(true);
            EditorUtility.SetDirty(target);
          }
        }
        else if (Tools.current == Tool.Scale)
        {
          EditorGUI.BeginChangeCheck();
          Vector3 v3Scale = Handles.ScaleHandle(relevanceSphere.m_v3Scale, relevanceSphere.m_v3Position, Quaternion.Euler(relevanceSphere.m_v3Rotation), HandleUtility.GetHandleSize(relevanceSphere.m_v3Position) * 1.0f);
          if (EditorGUI.EndChangeCheck())
          {
            Undo.RecordObject(meshSimplify, "Scale Relevance Sphere");
            relevanceSphere.m_v3Scale = v3Scale;
            meshSimplify.RestoreOriginalMesh(false, true);
            meshSimplify.SetDataDirty(true);
            EditorUtility.SetDirty(target);
          }
        }

        if(Event.current.type == EventType.Repaint)
        { 
          Matrix4x4 mtxHandles = Handles.matrix;
          Handles.matrix = Matrix4x4.TRS(relevanceSphere.m_v3Position, Quaternion.Euler(relevanceSphere.m_v3Rotation), relevanceSphere.m_v3Scale);
          Handles.color  = new Color(0.0f, 0.0f, 1.0f, 0.5f);
          Handles.SphereHandleCap(0, Vector3.zero, Quaternion.identity, 1.0f, EventType.Repaint);
          Handles.matrix = mtxHandles;
        }
      }
    }
  }

  public override void OnInspectorGUI()
  {
    MeshSimplify meshSimplify;

    string strIncludeChildrenLabel = "Recurse Into Children";
    int nButtonWidth               = 200;
    int nButtonWidthSmall          = 130;

    foreach (Object targetObject in targets)
    {
      meshSimplify = targetObject as MeshSimplify;

      if (meshSimplify.m_meshSimplifyRoot != null && targets.Length > 1)
      {
        EditorGUILayout.HelpBox("One or more GameObjects of the selection is not a root MeshSimplify GameObject. Only root MeshSimplify GameObjects can be edited at the same time.", MessageType.Warning);
        return;
      }
    }

    if (targets.Length > 1)
    {
      EditorGUILayout.HelpBox("Multiple selection", MessageType.Info);
    }

    serializedObject.Update();

    EditorGUILayout.Space();

    meshSimplify = target as MeshSimplify;

    if (meshSimplify.m_meshSimplifyRoot == null)
    {
      EditorGUILayout.PropertyField(PropertyGenerateIncludeChildren, new GUIContent(strIncludeChildrenLabel, "If checked, we will traverse the whole GameObject's hierarchy looking for meshes"));

      EditorGUI.BeginChangeCheck();
      EditorGUILayout.PropertyField(PropertyEnablePrefabUsage, new GUIContent("Enable Prefab Usage", "Will save the generated mesh assets to disk, so that this GameObject can be used as a prefab and be instantiated at runtime. Otherwise the mesh won't be available"));

      if (EditorGUI.EndChangeCheck())
      {
        if (PropertyEnablePrefabUsage.boolValue)
        {
          m_bEnablePrefabUsage = true;
        }
        else
        {
          m_bDisablePrefabUsage = true;
        }
      }
    }
    else
    {
      if (PropertyExcludedFromTree.boolValue)
      {
        GUILayout.Label("Object has been excluded from mesh simplification.");
      }
      else
      {
        GUILayout.Label("Child MeshSimplify GameObject depending on " + meshSimplify.m_meshSimplifyRoot.name);
        EditorGUILayout.PropertyField(PropertyOverrideRootSettings, new GUIContent("Override " + meshSimplify.m_meshSimplifyRoot.name + " settings", "Will allow to edit this object's own parameters, instead of inheriting those of the root Automatic LOD GameObject"));
      }
    }

    if (meshSimplify.m_meshSimplifyRoot == null || (PropertyOverrideRootSettings.boolValue == true && PropertyExcludedFromTree.boolValue == false))
    {
      bool bIsOverriden = PropertyOverrideRootSettings.boolValue == true;

      EditorGUI.BeginChangeCheck();
      EditorGUILayout.PropertyField(PropertyUseEdgeLength, new GUIContent("Use Edge Length", "Will take edge length into consideration when simplifying the mesh. Edges with higher length will be more likely to be kept"));
      if (EditorGUI.EndChangeCheck())
      {
        PropertyDataDirty.boolValue = true;
      }

      EditorGUI.BeginChangeCheck();
      EditorGUILayout.PropertyField(PropertyUseCurvature, new GUIContent("Use Curvature", "Will take the angle between triangles into consideration. Edges with smaller angles between two triangles will be more likely to be kept when simplifying the mesh."));
      if (EditorGUI.EndChangeCheck())
      {
        PropertyDataDirty.boolValue = true;
      }

      EditorGUI.BeginChangeCheck();
      EditorGUILayout.PropertyField(PropertyProtectTexture, new GUIContent("Protect Texture", "Will try to keep mapping integrity during the process of mesh simplification"));
      if (EditorGUI.EndChangeCheck())
      {
        PropertyDataDirty.boolValue = true;
      }

      EditorGUI.BeginChangeCheck();
      EditorGUILayout.PropertyField(PropertyLockBorder, new GUIContent("Keep Borders", "Will try to keep those vertices that form an object's border"));
      if (EditorGUI.EndChangeCheck())
      {
        PropertyDataDirty.boolValue = true;
      }

      EditorGUILayout.Space();

      float fVertexAmount = EditorGUILayout.Slider(new GUIContent("Vertex %", "The percentage of vertices from the original mesh to keep when simplifying it"), PropertyVertexAmount.floatValue * 100.0f, 0.0f, 100.0f);
      PropertyVertexAmount.floatValue = Mathf.Clamp01(fVertexAmount / 100.0f);
      int nSimplifiedMeshVertexCount = meshSimplify.GetSimplifiedVertexCount(bIsOverriden == false);
      int nMeshVertexCount = meshSimplify.GetOriginalVertexCount(bIsOverriden == false);

      EditorGUILayout.LabelField("Vertex count: " + nSimplifiedMeshVertexCount + "/" + nMeshVertexCount);

      int nSimplifiedMeshTriangleCount = meshSimplify.GetSimplifiedTriangleCount(bIsOverriden == false);
      int nMeshTriangleCount = meshSimplify.GetOriginalTriangleCount(bIsOverriden == false);

      EditorGUILayout.LabelField("Triangle count: " + nSimplifiedMeshTriangleCount + "/" + nMeshTriangleCount);

      //EditorGUILayout.LabelField("Child nodes: " + (meshSimplify.m_listDependentChildren == null ? 0 : meshSimplify.m_listDependentChildren.Count));

      EditorGUILayout.Space();

      EditorGUI.BeginChangeCheck();

      m_bPreviewOriginalMesh = EditorGUILayout.Toggle(new GUIContent("Preview Original Mesh", "Allows to quickly switch between viewing the simplified mesh and the original mesh to check the reduction quality"), m_bPreviewOriginalMesh);

      if (EditorGUI.EndChangeCheck())
      {
        if (m_bPreviewOriginalMesh)
        {
          meshSimplify.RestoreOriginalMesh(false, bIsOverriden == false);
        }
        else
        {
          meshSimplify.AssignSimplifiedMesh(bIsOverriden == false);
        }
      }

      EditorGUILayout.Space();
    }

    if (meshSimplify.m_meshSimplifyRoot == null)
    {
      PropertyExpandRelevanceSpheres.boolValue = EditorGUILayout.Foldout(PropertyExpandRelevanceSpheres.boolValue, new GUIContent("Vertex Relevance Modifiers:"));

      if (PropertyExpandRelevanceSpheres.boolValue)
      {
        EditorGUILayout.HelpBox("Use vertex relevance spheres to select which vertices should be preserved with more or less priority when simplifying the mesh.", MessageType.Info);

        EditorGUILayout.Space();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(new GUIContent("Add New Sphere", "Adds a new vertex relevance sphere"), GUILayout.Width(nButtonWidthSmall)))
        {
          PropertyRelevanceSpheres.InsertArrayElementAtIndex(0);
          PropertyDataDirty.boolValue = true;
          m_bSetupNewRelevanceSphere = true;
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUI.indentLevel++;

        int nSphereToDelete = -1;

        for (int i = 0; i < PropertyRelevanceSpheres.arraySize; i++)
        {
          SerializedProperty elementProperty = PropertyRelevanceSpheres.GetArrayElementAtIndex(i);

          SerializedProperty elementExpanded  = elementProperty.FindPropertyRelative("m_bExpanded");
          SerializedProperty elementPosition  = elementProperty.FindPropertyRelative("m_v3Position");
          SerializedProperty elementRotation  = elementProperty.FindPropertyRelative("m_v3Rotation");
          SerializedProperty elementScale     = elementProperty.FindPropertyRelative("m_v3Scale");
          SerializedProperty elementRelevance = elementProperty.FindPropertyRelative("m_fRelevance");

          elementExpanded.boolValue = EditorGUILayout.Foldout(elementExpanded.boolValue, new GUIContent("Sphere"));

          if (elementExpanded.boolValue)
          {
            bool bWideMode = EditorGUIUtility.wideMode;

            EditorGUIUtility.wideMode = true;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(elementPosition, new GUIContent("Position"));
            if (EditorGUI.EndChangeCheck())
            {
              PropertyDataDirty.boolValue = true;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(elementRotation, new GUIContent("Rotation"));
            if (EditorGUI.EndChangeCheck())
            {
              PropertyDataDirty.boolValue = true;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(elementScale, new GUIContent("Scale"));
            if (EditorGUI.EndChangeCheck())
            {
              PropertyDataDirty.boolValue = true;
            }

            EditorGUI.BeginChangeCheck();
            elementRelevance.floatValue = EditorGUILayout.Slider(new GUIContent("Relevance", "Tells the simplification algorithm how relevant the vertices inside this sphere are. Default relevance is 0, use lower values to discard non important vertices, and higher values to keep them before others when simplifying the mesh"), elementRelevance.floatValue, -1.0f, 1.0f);
            if (EditorGUI.EndChangeCheck())
            {
              PropertyDataDirty.boolValue = true;
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("Remove Sphere", "Removes this simplification sphere"), GUILayout.Width(nButtonWidthSmall)))
            {
              nSphereToDelete = i;
              PropertyDataDirty.boolValue = true;
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            EditorGUIUtility.wideMode = bWideMode;
          }
        }

        if (nSphereToDelete >= 0)
        {
          PropertyRelevanceSpheres.DeleteArrayElementAtIndex(nSphereToDelete);
        }

        EditorGUI.indentLevel--;
      }

      EditorGUILayout.Space();
    }

    if (meshSimplify.m_meshSimplifyRoot == null || (PropertyOverrideRootSettings.boolValue == true && PropertyExcludedFromTree.boolValue == false))
    {
      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();

      if (GUILayout.Button(new GUIContent("Compute mesh", "Starts the mesh simplification process and assigns the GameObject the new simplified mesh"), GUILayout.Width(nButtonWidth)))
      {
        m_bComputeMesh = true;
      }

      GUILayout.FlexibleSpace();
      GUILayout.EndHorizontal();

      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();

      if (GUILayout.Button(new GUIContent("Restore Original Mesh...", "Deletes the simplified data and restores the original mesh"), GUILayout.Width(nButtonWidth)))
      {
        if (EditorUtility.DisplayDialog("Delete all data and restore original mesh?", "Are you sure you want to delete all data and restore the original mesh?", "Delete", "Cancel"))
        {
          m_bDeleteData = true;
        }
      }

      GUILayout.FlexibleSpace();
      GUILayout.EndHorizontal();
    }

    if (meshSimplify.m_meshSimplifyRoot != null && PropertyExcludedFromTree.boolValue == false)
    {
      EditorGUILayout.Space();

      GUILayout.BeginHorizontal();
      GUILayout.FlexibleSpace();

      if (GUILayout.Button(new GUIContent("Exclude from tree...", "Restores this GameObject's original mesh and excludes it from the mesh simplification tree"), GUILayout.Width(nButtonWidth)))
      {
        if (EditorUtility.DisplayDialog("Remove from tree?", "Are you sure you want to restore this gameobject's mesh and exclude it from the tree?", "Remove", "Cancel"))
        {
          m_bRemoveFromTree = true;
        }
      }

      GUILayout.FlexibleSpace();
      GUILayout.EndHorizontal();
    }

    serializedObject.ApplyModifiedProperties();

    bool bRepaint = false;

    if(m_bEnablePrefabUsage)
    {
      m_bEnablePrefabUsage = false;
      SaveMeshAssets();
    }

    if(m_bDisablePrefabUsage)
    {
      m_bDisablePrefabUsage = false;

      if (PropertyEnablePrefabUsage.boolValue == false)
      {
        foreach (Object targetObject in targets)
        {
          meshSimplify = targetObject as MeshSimplify;
          meshSimplify.DisablePrefabUsage(true);
        }
      }
    }

    if (m_bComputeMesh && Event.current.type == EventType.Repaint)
    {
      m_bComputeMesh = false;

      Simplifier.Cancelled = false;

      foreach (Object targetObject in targets)
      {
        meshSimplify = targetObject as MeshSimplify;

        if (meshSimplify.HasData() == false)
        {
          if (PropertyGenerateIncludeChildren.boolValue == false)
          {
            if (MeshSimplify.HasValidMeshData(meshSimplify.gameObject) == false)
            {
              EditorUtility.DisplayDialog("Error", "Object " + meshSimplify.name + " has no MeshFilter nor Skinned Mesh to process. Please use the \"" + strIncludeChildrenLabel + "\" parameter if you want to process the whole " + meshSimplify.name + " hierarchy for meshes", "OK");
              continue;
            }
          }
        }

        try
        {
          if (meshSimplify.HasDataDirty() || meshSimplify.HasData() == false || meshSimplify.HasNonMeshSimplifyGameObjectsInTree())
          {
            meshSimplify.RestoreOriginalMesh(true, meshSimplify.m_meshSimplifyRoot == null);
            meshSimplify.ComputeData(meshSimplify.m_meshSimplifyRoot == null, Progress);

            if (Simplifier.Cancelled)
            {
              meshSimplify.RestoreOriginalMesh(true, meshSimplify.m_meshSimplifyRoot == null);
              break;
            }
          }

          meshSimplify.ComputeMesh(meshSimplify.m_meshSimplifyRoot == null, Progress);

          if (Simplifier.Cancelled)
          {
            break;
          }

          meshSimplify.AssignSimplifiedMesh(meshSimplify.m_meshSimplifyRoot == null);

          if (meshSimplify.m_strAssetPath != null && meshSimplify.m_bEnablePrefabUsage)
          {
            SaveMeshAssets();
          }
        }
        catch (System.Exception e)
        {
          Debug.LogError("Error generating mesh: " + e.Message + " Stack: " + e.StackTrace);
          EditorUtility.ClearProgressBar();
          Simplifier.Cancelled = false;
        }

        Simplifier.Cancelled = false;
      }

      bRepaint = true;
      EditorUtility.ClearProgressBar();
    }

    if (m_bDeleteData && Event.current.type == EventType.Repaint)
    {
      m_bDeleteData = false;

      foreach (Object targetObject in targets)
      {
        meshSimplify = targetObject as MeshSimplify;
        meshSimplify.RestoreOriginalMesh(true, meshSimplify.m_meshSimplifyRoot == null);
        RemoveChildMeshSimplifyComponents(meshSimplify);
      }

      bRepaint = true;
    }

    if (m_bRemoveFromTree && Event.current.type == EventType.Repaint)
    {
      m_bRemoveFromTree = false;
      meshSimplify = target as MeshSimplify;
      meshSimplify.RemoveFromTree();

      if (Application.isEditor && Application.isPlaying == false)
      {
        UnityEngine.Object.DestroyImmediate(meshSimplify);
      }
      else
      {
        UnityEngine.Object.Destroy(meshSimplify);
      }

      bRepaint = true;
    }

    if (m_bSetupNewRelevanceSphere)
    {
      m_bSetupNewRelevanceSphere = false;

      foreach (Object targetObject in targets)
      {
        meshSimplify = targetObject as MeshSimplify;

        if (meshSimplify.m_aRelevanceSpheres != null && meshSimplify.m_aRelevanceSpheres.Length > 0)
        {
          meshSimplify.m_aRelevanceSpheres[0].SetDefault(meshSimplify.transform, 0.0f);
        }
      }
    }

    if (bRepaint)
    {
      Repaint();
    }
  }

  void RemoveChildMeshSimplifyComponents(MeshSimplify meshSimplify)
  {
    RemoveChildMeshSimplifyComponentsRecursive(meshSimplify.gameObject, meshSimplify.gameObject, true);
  }

  void RemoveChildMeshSimplifyComponentsRecursive(GameObject root, GameObject gameObject, bool bRecurseIntoChildren)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify != null && meshSimplify.m_meshSimplifyRoot != null)
    {
      if (Application.isEditor && Application.isPlaying == false)
      {
        UnityEngine.Object.DestroyImmediate(meshSimplify);
      }
      else
      {
        UnityEngine.Object.Destroy(meshSimplify);
      }
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        RemoveChildMeshSimplifyComponentsRecursive(root, gameObject.transform.GetChild(nChild).gameObject, true);
      }
    }
  }
  
  void SaveMeshAssets()
  {
    try
    {
      foreach (Object targetObject in targets)
      {
        MeshSimplify meshSimplify = targetObject as MeshSimplify;
        GameObject    gameObject    = meshSimplify.gameObject;

        if (meshSimplify.m_meshSimplifyRoot == null && meshSimplify.m_bEnablePrefabUsage)
        {
          string strMeshAssetPath = meshSimplify.m_strAssetPath;

          if (string.IsNullOrEmpty(strMeshAssetPath))
          {
            //Debug.Log("Showing file selection panel");

            strMeshAssetPath = UnityEditor.EditorUtility.SaveFilePanelInProject("Save mesh asset(s)", "mesh_" + gameObject.name + gameObject.GetInstanceID().ToString() + ".asset", "asset", "Please enter a file name to save the mesh asset(s) to");

            if (strMeshAssetPath.Length == 0)
            {
              //Debug.LogWarning("strMeshAssetPath.Length == 0. User cancelled?");
              return;
            }

            //Debug.Log("User selected " + strMeshAssetPath + " using panel.");

            meshSimplify.m_strAssetPath = strMeshAssetPath;
          }

          int nCounter = 0;

          //Debug.Log("Saving files to " + strMeshAssetPath + ". Exists previously?: " + System.IO.File.Exists(strMeshAssetPath));
          SaveMeshAssetsRecursive(gameObject, gameObject, strMeshAssetPath, true, System.IO.File.Exists(strMeshAssetPath), ref nCounter);
        }
      }
    }
    catch (System.Exception e)
    {
      Debug.LogError("Error saving mesh assets to disk: " + e.Message + " Stack: " + e.StackTrace);
      EditorUtility.ClearProgressBar();
      Simplifier.Cancelled = false;
    }

    EditorUtility.ClearProgressBar();
    UnityEditor.AssetDatabase.Refresh();
    Simplifier.Cancelled = false;
  }

  bool SaveMeshAssetsRecursive(GameObject root, GameObject gameObject, string strFile, bool bRecurseIntoChildren, bool bAssetAlreadyCreated, ref int nProgressElementsCounter)
  {
    if(gameObject == null || Simplifier.Cancelled)
    {
      return bAssetAlreadyCreated;
    }

    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify != null && meshSimplify.HasData() && (meshSimplify.m_meshSimplifyRoot == null || meshSimplify.m_meshSimplifyRoot.gameObject == root))
    {
      int nTotalProgressElements = meshSimplify.m_meshSimplifyRoot != null ? (meshSimplify.m_meshSimplifyRoot.m_listDependentChildren.Count + 1) : 1;

      if (meshSimplify.m_simplifiedMesh != null && MeshSimplify.HasValidMeshData(meshSimplify.gameObject))
      {
        float fT = (float)nProgressElementsCounter / (float)nTotalProgressElements;
        Progress("Saving meshes to asset file", meshSimplify.name, fT);

        if (Simplifier.Cancelled)
        {
          return bAssetAlreadyCreated;
        }

        if (bAssetAlreadyCreated == false && UnityEditor.AssetDatabase.Contains(meshSimplify.m_simplifiedMesh) == false)
        {
          //Debug.Log("Creating asset " + meshSimplify.m_simplifiedMesh.name);

          UnityEditor.AssetDatabase.CreateAsset(meshSimplify.m_simplifiedMesh, strFile);
          bAssetAlreadyCreated = true;
        }
        else
        {
          if (UnityEditor.AssetDatabase.Contains(meshSimplify.m_simplifiedMesh) == false)
          {
            //Debug.Log("Adding asset " + meshSimplify.m_simplifiedMesh.name);

            UnityEditor.AssetDatabase.AddObjectToAsset(meshSimplify.m_simplifiedMesh, strFile);
            UnityEditor.AssetDatabase.ImportAsset(UnityEditor.AssetDatabase.GetAssetPath(meshSimplify.m_simplifiedMesh));
          }
        }

        nProgressElementsCounter++;
      }
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        bAssetAlreadyCreated = SaveMeshAssetsRecursive(root, gameObject.transform.GetChild(nChild).gameObject, strFile, bRecurseIntoChildren, bAssetAlreadyCreated, ref nProgressElementsCounter);
      }
    }

    return bAssetAlreadyCreated;
  }

  void SetHideFlags()
  {
    foreach (Object targetObject in targets)
    {
      MeshSimplify meshSimplify = targetObject as MeshSimplify;

      if (meshSimplify.m_meshSimplifyRoot == null)
      {
        SetHideFlagsRecursive(meshSimplify.gameObject, meshSimplify.gameObject, true);
      }
    }
  }

  void SetHideFlagsRecursive(GameObject root, GameObject gameObject, bool bRecurseIntoChildren)
  {
    MeshSimplify meshSimplify = gameObject.GetComponent<MeshSimplify>();

    if (meshSimplify && meshSimplify.GetMeshSimplifier())
    {
      meshSimplify.GetMeshSimplifier().hideFlags = HideFlags.HideInInspector;
    }

    if (bRecurseIntoChildren)
    {
      for (int nChild = 0; nChild < gameObject.transform.childCount; nChild++)
      {
        SetHideFlagsRecursive(root, gameObject.transform.GetChild(nChild).gameObject, true);
      }
    }
  }

  bool m_bComputeMesh             = false;
  bool m_bEnablePrefabUsage       = false;
  bool m_bDisablePrefabUsage      = false;
  bool m_bDeleteData              = false;
  bool m_bRemoveFromTree          = false;
  bool m_bSetupNewRelevanceSphere = false;

  bool m_bPreviewOriginalMesh     = false;

  SerializedProperty PropertyGenerateIncludeChildren;
  SerializedProperty PropertyEnablePrefabUsage;
  SerializedProperty PropertyExpandRelevanceSpheres;
  SerializedProperty PropertyRelevanceSpheres;
  SerializedProperty PropertyOverrideRootSettings;
  SerializedProperty PropertyVertexAmount;
  SerializedProperty PropertyUseEdgeLength;
  SerializedProperty PropertyUseCurvature;
  SerializedProperty PropertyProtectTexture;
  SerializedProperty PropertyLockBorder;
  SerializedProperty PropertyDataDirty;
  SerializedProperty PropertyExcludedFromTree;

  static int    s_nLastProgress  = -1;
  static string s_strLastTitle   = "";
  static string s_strLastMessage = "";
}
