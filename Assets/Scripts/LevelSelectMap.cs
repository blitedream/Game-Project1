using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelSelectMapBootstrap
{
    public const int PreviewVersion = 13;
    private const float MapWidth = 40f;
    private const float MapDepth = 40f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBuildMapAtRuntime()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnBuildMapAtRuntimeSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnBuildMapAtRuntimeSceneLoaded;
    }

    private static void OnBuildMapAtRuntimeSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        BuildMapAtRuntime();
    }

    private static void BuildMapAtRuntime()
    {
        if (SceneManager.GetActiveScene().name != "LevelSelect") return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (GameObject.Find("Tactical 3D Mission Map") != null)
        {
            LevelSelectNode.ClearSelection();
            return;
        }

        BuildMapInCurrentScene();
    }

    public static void BuildMapInCurrentScene()
    {
        GameObject existing = GameObject.Find("Tactical 3D Mission Map");
        if (existing != null) DestroyGenerated(existing);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.009f;
        RenderSettings.fogColor = new Color(0.025f, 0.095f, 0.115f);
        RenderSettings.ambientLight = new Color(0.1f, 0.2f, 0.22f);

        if (Camera.main != null) DestroyGenerated(Camera.main.gameObject);
        GameObject oldLight = GameObject.Find("Directional Light");
        if (oldLight != null) DestroyGenerated(oldLight);

        GameObject root = new GameObject("Tactical 3D Mission Map");
        root.AddComponent<LevelSelectPreviewMarker>().Version = PreviewVersion;
        CreateCamera(root.transform);
        CreateLighting(root.transform);
        CreateWaterPlane(root.transform);
        CreateTerrain(root.transform);

        // Keep every beacon just above its local terrain. The terrain itself carries
        // the high -> level -> deep progression, so no point can disappear inside it.
        Vector3 level1 = MissionPosition(-10.8f, 11.5f);
        Vector3 level2 = MissionPosition(3.1f, 2.1f);
        Vector3 level3 = MissionPosition(0.15f, 3.75f);
        CreateRoute(root.transform, new[] { level1, level2, level3 });

        CreateMissionNode(root.transform, "TRAINING POOL", "Level1", level1,
            new Color(0.2f, 0.95f, 0.82f), "01", "Equipment and movement training", "LOW RISK");
        CreateMissionNode(root.transform, "KELKAYA SINKHOLE", "Level2", level2,
            new Color(0.15f, 0.9f, 0.64f), "02", "Vertical cave and buoyancy training", "MEDIUM RISK");
        CreateMissionNode(root.transform, "ZACATON RECOVERY", "Level3", level3,
            new Color(0.25f, 0.68f, 1f), "03", "285 m body recovery operation", "EXTREME RISK");

        LevelSelectNode.ClearSelection();
    }

    private static void DestroyGenerated(Object target)
    {
        if (target == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(target);
            return;
        }
#endif
        Object.Destroy(target);
    }

    private static float TerrainHeight(float x, float z)
    {
        // Broad physical relief matching the main American cordillera. The real
        // shaded-relief texture supplies the fine geographic detail.
        float northMask = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-3f, 5f, z));
        float southMask = 1f - northMask;
        float rockiesCenter = Mathf.Lerp(-7f, -10f, Mathf.InverseLerp(0f, 20f, z));
        float andesCenter = Mathf.Lerp(-4f, -7f, Mathf.InverseLerp(-20f, 0f, z));
        float rockies = Mathf.Exp(-Mathf.Pow((x - rockiesCenter) / 3.5f, 2f)) * 0.62f * northMask;
        float andes = Mathf.Exp(-Mathf.Pow((x - andesCenter) / 2.5f, 2f)) * 0.78f * southMask;
        float appalachians = Gaussian(x, z, 6f, 11f, 5.5f) * 0.16f;
        float highlands = Gaussian(x, z, 5f, -8f, 8f) * 0.12f;
        float detail = (Mathf.PerlinNoise(x * 0.16f + 9f, z * 0.16f + 3f) - 0.5f) * 0.08f;
        return Mathf.Max(0f, rockies + andes + appalachians + highlands + detail);
    }

    private static float Gaussian(float x, float z, float cx, float cz, float radius)
    {
        float dx = x - cx;
        float dz = z - cz;
        return Mathf.Exp(-(dx * dx + dz * dz) / (radius * radius));
    }

    private static Vector3 MissionPosition(float x, float z)
    {
        return new Vector3(x, TerrainHeight(x, z) + 0.2f, z);
    }

    private static void CreateCamera(Transform parent)
    {
        GameObject cameraObject = new GameObject("Tactical Map Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(parent);
        cameraObject.transform.position = new Vector3(0f, 28f, -16f);
        cameraObject.transform.LookAt(new Vector3(0f, 0.25f, 4.5f));

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = false;
        camera.fieldOfView = 40f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 140f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.027f, 0.133f, 0.173f);
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<MapCameraController>();
        cameraObject.AddComponent<MapMissionOverlay>();
    }

    private static void CreateLighting(Transform parent)
    {
        GameObject sunObject = new GameObject("Map Sun");
        sunObject.transform.SetParent(parent);
        sunObject.transform.rotation = Quaternion.Euler(52f, -38f, 0f);
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.58f, 0.83f, 0.88f);
        sun.intensity = 1.55f;
        sun.shadows = LightShadows.Soft;

        GameObject fillObject = new GameObject("Map Fill");
        fillObject.transform.SetParent(parent);
        fillObject.transform.position = new Vector3(7f, 7f, 3f);
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.color = new Color(0.05f, 0.55f, 0.62f);
        fill.intensity = 5f;
        fill.range = 32f;
    }

    private static void CreateTerrain(Transform parent)
    {
        const int xSegments = 100;
        const int zSegments = 70;
        Vector3[] vertices = new Vector3[(xSegments + 1) * (zSegments + 1)];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[xSegments * zSegments * 6];

        for (int z = 0; z <= zSegments; z++)
        {
            for (int x = 0; x <= xSegments; x++)
            {
                float worldX = (x / (float)xSegments - 0.5f) * MapWidth;
                float worldZ = (z / (float)zSegments - 0.5f) * MapDepth;
                int index = z * (xSegments + 1) + x;
                vertices[index] = new Vector3(worldX, TerrainHeight(worldX, worldZ), worldZ);
                uv[index] = new Vector2(x / (float)xSegments, z / (float)zSegments);
            }
        }

        int t = 0;
        for (int z = 0; z < zSegments; z++)
        {
            for (int x = 0; x < xSegments; x++)
            {
                int a = z * (xSegments + 1) + x;
                int b = a + 1;
                int c = a + xSegments + 1;
                int d = c + 1;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }
        }

        Mesh mesh = new Mesh { name = "Tactical Bathymetric Terrain" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject terrain = new GameObject("Real Americas Tactical Relief");
        terrain.transform.SetParent(parent);
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer terrainRenderer = terrain.AddComponent<MeshRenderer>();
        terrainRenderer.sharedMaterial = CreateAmericasMaterial();
        terrainRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void CreateWaterPlane(Transform parent)
    {
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Seamless Ocean Backdrop";
        water.transform.SetParent(parent);
        water.transform.position = new Vector3(0f, -0.12f, 15f);
        water.transform.localScale = new Vector3(20f, 1f, 20f);
        DestroyGenerated(water.GetComponent<Collider>());

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        Color oceanColor = new Color(0.027f, 0.133f, 0.173f);
        Material material = new Material(shader) { name = "Seamless Deep Ocean", color = oceanColor };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", oceanColor);
        water.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void CreateContours(Transform parent)
    {
        Material contourMaterial = CreateMaterial("Terrain Contours", new Color(0.18f, 0.58f, 0.58f), 0.3f, true);
        for (int row = 0; row < 10; row++)
        {
            float z = -11f + row * 2.35f;
            GameObject lineObject = new GameObject("Topographic Contour");
            lineObject.transform.SetParent(parent);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.positionCount = 65;
            line.startWidth = 0.025f;
            line.endWidth = 0.025f;
            line.sharedMaterial = contourMaterial;
            for (int i = 0; i < line.positionCount; i++)
            {
                float x = Mathf.Lerp(-20f, 20f, i / (float)(line.positionCount - 1));
                line.SetPosition(i, new Vector3(x, TerrainHeight(x, z) + 0.08f, z));
            }
        }
    }

    private static void CreateRoute(Transform parent, Vector3[] positions)
    {
        GameObject routeObject = new GameObject("Operation Route");
        routeObject.transform.SetParent(parent);
        LineRenderer line = routeObject.AddComponent<LineRenderer>();
        const int samplesPerSection = 24;
        Vector3[] routePoints = new Vector3[(positions.Length - 1) * samplesPerSection + 1];
        int point = 0;
        for (int section = 0; section < positions.Length - 1; section++)
        {
            Vector3 start = positions[section];
            Vector3 end = positions[section + 1];
            Vector3 direction = end - start;
            Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x).normalized;
            float bend = section % 2 == 0 ? 2.4f : -1.35f;
            Vector3 control = (start + end) * 0.5f + perpendicular * bend;
            int firstSample = section == 0 ? 0 : 1;
            for (int sample = firstSample; sample <= samplesPerSection; sample++)
            {
                float t = sample / (float)samplesPerSection;
                float inverse = 1f - t;
                Vector3 position = inverse * inverse * start + 2f * inverse * t * control + t * t * end;
                position.y = TerrainHeight(position.x, position.z) + 0.38f;
                routePoints[point++] = position;
            }
        }
        line.positionCount = point;
        line.SetPositions(routePoints);
        line.startWidth = 0.18f;
        line.endWidth = 0.18f;
        line.numCapVertices = 8;
        line.numCornerVertices = 8;
        line.sortingOrder = 2;
        line.sharedMaterial = CreateMaterial("Operation Route Core", new Color(0.35f, 2.2f, 1.35f), 0.8f, true);

        GameObject glowObject = new GameObject("Operation Route Glow");
        glowObject.transform.SetParent(routeObject.transform, false);
        LineRenderer glow = glowObject.AddComponent<LineRenderer>();
        glow.positionCount = point;
        glow.SetPositions(routePoints);
        glow.startWidth = 0.48f;
        glow.endWidth = 0.48f;
        glow.numCapVertices = 8;
        glow.numCornerVertices = 8;
        glow.sortingOrder = 1;
        glow.sharedMaterial = CreateMaterial("Operation Route Aura", new Color(0.06f, 0.72f, 0.52f), 0.25f, true);
    }

    private static void CreateMissionNode(Transform parent, string title, string sceneName, Vector3 position,
        Color color, string number, string description, string risk)
    {
        GameObject root = new GameObject(title);
        root.transform.SetParent(parent);
        root.transform.position = position;

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Mission Zone";
        ring.transform.SetParent(root.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        ring.transform.localScale = new Vector3(1.35f, 0.06f, 1.35f);
        ring.GetComponent<Renderer>().sharedMaterial = CreateMaterial(title + " Zone", color * 0.72f, 0.8f, true);

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "Select " + title;
        marker.transform.SetParent(root.transform, false);
        marker.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        marker.transform.localScale = new Vector3(0.78f, 0.78f, 0.78f);
        marker.transform.rotation = Quaternion.Euler(18f, 45f, 18f);
        marker.GetComponent<Renderer>().sharedMaterial = CreateMaterial(title + " Beacon", color, 0.9f, true);
        BoxCollider clickArea = marker.GetComponent<BoxCollider>();
        clickArea.size = new Vector3(4.2f, 4.2f, 4.2f);

        GameObject beam = new GameObject("Beacon Beam");
        beam.transform.SetParent(root.transform, false);
        LineRenderer beamLine = beam.AddComponent<LineRenderer>();
        beamLine.positionCount = 2;
        beamLine.SetPosition(0, new Vector3(0f, 0.2f, 0f));
        beamLine.SetPosition(1, new Vector3(0f, 2.8f, 0f));
        beamLine.startWidth = 0.045f;
        beamLine.endWidth = 0.01f;
        beamLine.sharedMaterial = CreateMaterial(title + " Beam", color, 0.8f, true);

        LevelSelectNode selector = marker.AddComponent<LevelSelectNode>();
        selector.SceneName = sceneName;
        selector.DisplayName = title;
        selector.Description = description;
        selector.Risk = risk;
        selector.Number = number;
        selector.LevelNumber = int.Parse(number);
        selector.BaseColor = color;

        GameObject textObject = new GameObject(title + " Label");
        textObject.transform.SetParent(root.transform, false);
        textObject.transform.localPosition = new Vector3(0f, 2.45f, 0f);
        TextMeshPro label = textObject.AddComponent<TextMeshPro>();
        label.text = title;
        label.fontSize = 2.25f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(14f, 2.5f);
        textObject.AddComponent<MapBillboard>();
    }

    private static Material CreateMaterial(string name, Color color, float smoothness, bool emission = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (emission)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 2f);
        }
        return material;
    }

    private static Material CreateAmericasMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = "Natural Earth Americas Relief", color = Color.white };
        Texture2D relief = Resources.Load<Texture2D>("Map/AmericasRelief");
        if (relief == null)
        {
            Debug.LogWarning("Americas relief texture was not found in Resources/Map.");
            return material;
        }

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", relief);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", relief);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        material.color = Color.white;
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_ZWrite", 1f);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        return material;
    }
}

public static class LevelMapHotkeyBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterAddHotkey()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnAddHotkeySceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnAddHotkeySceneLoaded;
    }

    private static void OnAddHotkeySceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        AddHotkey();
    }

    private static void AddHotkey()
    {
        if (Object.FindFirstObjectByType<LevelMapHotkey>() != null) return;
        GameObject hotkey = new GameObject("Level Map Hotkey");
        hotkey.AddComponent<LevelMapHotkey>();
    }
}
