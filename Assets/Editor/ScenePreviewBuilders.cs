using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ScenePreviewBuilders
{
    private static string Level2ImportedModelPath => "Assets/Models/Level2/level2_cave_repaired.glb";
    static ScenePreviewBuilders()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall += BuildCurrentScenePreview;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += BuildCurrentScenePreview;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += BuildCurrentScenePreview;
    }

    private static void BuildCurrentScenePreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return;

        if (scene.name == "Level1" && RemoveMissingScripts(scene) > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (scene.name == "LevelSelect" && NeedsLevelSelectRebuild())
        {
            GameObject oldRoot = GameObject.Find("Tactical 3D Mission Map");
            if (oldRoot != null)
                Object.DestroyImmediate(oldRoot);
            LevelSelectMapBootstrap.BuildMapInCurrentScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SceneView.lastActiveSceneView?.FrameSelected();
        }
        else if (scene.name == "Level1" && NeedsLevel1Rebuild())
        {
            GameObject map = BuildLevel1TrainingMap();
            Selection.activeGameObject = map;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        else if (scene.name == "Level2" && NeedsLevel2Rebuild())
        {
            GameObject root = BuildLevel2Preview(true);
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        else if (scene.name == "Level3" && NeedsLevel3Rebuild())
        {
            BuildLevel2Preview(false);
            GameObject root = Level3AutoBootstrap.RebuildScene();
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static bool NeedsLevel2Rebuild()
    {
        GameObject preview = GameObject.Find(Level2SinkholeBootstrap.PreviewRootName);
        Level2PreviewMarker marker = preview != null ? preview.GetComponent<Level2PreviewMarker>() : null;
        return preview == null || marker == null || marker.Version != Level2SinkholeBootstrap.PreviewVersion ||
            preview.GetComponentsInChildren<Renderer>(true).Length == 0 ||
            Object.FindFirstObjectByType<Level2SinkholeRuntimeLoader>(FindObjectsInactive.Include) != null ||
            GameObject.Find("glTF-StableFramerate") != null;
    }

    private static bool NeedsLevel1Rebuild()
    {
        // Level1PoolExtension versions include both the pool shaft and the room enclosure.
        GameObject map = GameObject.Find("Extended Training Pool");
        Level1PoolExtension extension = map != null ? map.GetComponent<Level1PoolExtension>() : null;
        return map == null || extension == null || extension.MapVersion != Level1PoolExtension.CurrentMapVersion ||
            map.transform.childCount < 8;
    }

    private static int RemoveMissingScripts(Scene scene)
    {
        int removed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(item.gameObject);
        }
        return removed;
    }

    private static GameObject BuildLevel1TrainingMap()
    {
        GameObject existingMap = GameObject.Find("Extended Training Pool");
        if (existingMap != null)
            Object.DestroyImmediate(existingMap);

        GameObject map = new GameObject("Extended Training Pool");
        map.AddComponent<Level1PoolExtension>();

        Debug.Log("Level 1 training map rebuilt as a persistent 60 m pool.");
        return map;
    }

    private static GameObject BuildLevel2Preview(bool includeSharedCave)
    {
        CleanupLevel2LegacyObjects();

        AssetDatabase.ImportAsset(Level2ImportedModelPath, ImportAssetOptions.ForceSynchronousImport);
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Level2ImportedModelPath);

        GameObject root = new GameObject(Level2SinkholeBootstrap.PreviewRootName);
        Level2SinkholeBootstrap.ExpandMap(root.transform);
        root.AddComponent<Level2PreviewMarker>().Version = Level2SinkholeBootstrap.PreviewVersion;

        if (modelAsset == null)
        {
            Debug.LogError($"Level 2 editor preview could not import {Level2ImportedModelPath}.");
            return root;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(modelAsset, root.transform) as GameObject;
        if (instance == null)
            instance = Object.Instantiate(modelAsset, root.transform);
        instance.name = "Fused Kelkaya Zacaton Cave Model";
        // Keep the imported root rotation and scale: the source scan uses a
        // non-standard coordinate basis and GLTFast corrects it on this transform.
        instance.transform.localPosition = Vector3.zero;

        Bounds bounds = CalculateBounds(instance);
        root.AddComponent<Level2SinkholeRuntimeLoader>().AddEnvironmentColliders();
        Physics.SyncTransforms();
        CreateLevel2PreviewWater(root.transform);
        CenoteLandscape.Build(root.transform, bounds);
        // Zacaton is already part of the exported fused mesh. Never append the
        // old standalone FBX here or the two caves will overlap.

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            float distance = Mathf.Max(bounds.size.x, bounds.size.z) * 0.72f;
            sceneView.LookAt(bounds.center, Quaternion.Euler(35f, 205f, 0f), Mathf.Max(35f, distance));
            sceneView.Repaint();
        }

        Debug.Log("Level 2 sinkhole editor preview created from the imported GLB asset.");
        return root;
    }

    private static Bounds CalculateBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(target.transform.position, Vector3.one * 20f);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void CreateLevel2PreviewWater(Transform parent)
    {
        GameObject water = new GameObject("Single Cave Water Preview");
        water.transform.SetParent(parent, false);
        // The imported editor prefab keeps Sketchfab's source-space root, whose
        // origin is at the sinkhole rim. Runtime GLTFast uses a different basis,
        // so runtime world coordinates must never be reused for this preview.
        water.transform.position = Level2SinkholeBootstrap.FusedWaterCenter;
        water.transform.localScale = new Vector3(.5f,1f,.5f);

        MeshFilter filter = water.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateOvalPreviewMesh(72, 12f, 12f);
        MeshRenderer waterRenderer = water.AddComponent<MeshRenderer>();

        Shader shader = Shader.Find("GP1/Level2/Kelkaya Water");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader) { name = "Kelkaya Preview Water" };
        if (material.HasProperty("_ShallowColor"))
        {
            material.SetColor("_ShallowColor", new Color(0.04f, 0.58f, 0.68f, 0.88f));
            material.SetColor("_DeepColor", new Color(0.005f, 0.12f, 0.22f, 0.94f));
            material.SetColor("_FresnelColor", new Color(0.55f, 0.96f, 1f, 1f));
            material.SetFloat("_WaveHeight", 0.09f);
            material.SetFloat("_WaveScale", 0.26f);
            material.SetFloat("_WaveSpeed", 0.65f);
        }
        else
        {
            material.color = new Color(0.025f, 0.22f, 0.27f, 1f);
            material.SetFloat("_Metallic", 0.05f);
            material.SetFloat("_Smoothness", 0.78f);
        }
        waterRenderer.sharedMaterial = material;
    }

    private static Mesh CreateOvalPreviewMesh(int segments, float radiusX, float radiusZ)
    {
        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uv = new Vector2[segments + 1];
        int[] triangles = new int[segments * 3];
        uv[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radiusX, 0f, Mathf.Sin(angle) * radiusZ);
            uv[i + 1] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f + Vector2.one * 0.5f;
            int triangle = i * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = i + 1;
            triangles[triangle + 2] = i == segments - 1 ? 1 : i + 2;
        }

        Mesh mesh = new Mesh { name = "Kelkaya Sinkhole Preview Water Mesh" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void CleanupLevel2LegacyObjects()
    {
        Transform[] items = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform item in items)
        {
            if (item == null || item.parent != null)
                continue;

            GameObject target = item.gameObject;
            if (target.name == Level2SinkholeBootstrap.RootName ||
                target.name == Level2SinkholeBootstrap.PreviewRootName ||
                target.name.StartsWith("glTF-StableFramerate", System.StringComparison.Ordinal))
            {
                Object.DestroyImmediate(target);
            }
        }
    }

    private static bool NeedsLevel3Rebuild()
    {
        GameObject root = GameObject.Find(Level3AutoBootstrap.RootName);
        GameObject recoveryBody = GameObject.Find("Recovery Body");
        Level3SharedMapMarker marker = root != null ? root.GetComponent<Level3SharedMapMarker>() : null;
        return GameObject.Find(Level2SinkholeBootstrap.PreviewRootName) == null || root == null ||
            marker == null || marker.Version != Level3AutoBootstrap.SharedMapVersion ||
            root.transform.childCount == 0 || recoveryBody == null ||
            recoveryBody.GetComponent<Level3DiverModelLoader>() == null;
    }

    private static bool NeedsLevelSelectRebuild()
    {
        GameObject root = GameObject.Find("Tactical 3D Mission Map");
        if (root == null)
            return true;

        LevelSelectPreviewMarker marker = root.GetComponent<LevelSelectPreviewMarker>();
        return marker == null || marker.Version != LevelSelectMapBootstrap.PreviewVersion;
    }

    [MenuItem("Tools/Scene Preview/Rebuild Level Select Map")]
    private static void RebuildLevelSelect()
    {
        if (SceneManager.GetActiveScene().name != "LevelSelect")
        {
            Debug.LogWarning("Open LevelSelect before rebuilding its preview.");
            return;
        }

        GameObject existing = GameObject.Find("Tactical 3D Mission Map");
        if (existing != null)
            Object.DestroyImmediate(existing);

        LevelSelectMapBootstrap.BuildMapInCurrentScene();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    [MenuItem("Tools/Scene Preview/Rebuild Level 2 Preview")]
    private static void RebuildLevel2()
    {
        if (SceneManager.GetActiveScene().name != "Level2")
        {
            Debug.LogWarning("Open Level2 before rebuilding its preview.");
            return;
        }

        GameObject root = BuildLevel2Preview(true);
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    [MenuItem("Tools/Scene Preview/Rebuild Level 1 Training Map")]
    private static void RebuildLevel1()
    {
        if (SceneManager.GetActiveScene().name != "Level1")
        {
            Debug.LogWarning("Open Level1 before rebuilding its markers.");
            return;
        }

        GameObject map = BuildLevel1TrainingMap();
        Selection.activeGameObject = map;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    [MenuItem("Tools/Scene Preview/Rebuild Level 3 Environment")]
    private static void RebuildLevel3()
    {
        if (SceneManager.GetActiveScene().name != "Level3")
        {
            Debug.LogWarning("Open Level3 before rebuilding its environment.");
            return;
        }

        BuildLevel2Preview(false);
        GameObject root = Level3AutoBootstrap.RebuildScene();
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }
}

