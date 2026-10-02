using System;
using System.IO;
using GLTFast;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Level2SinkholeBootstrap
{
    public const int PreviewVersion = 26;
    public const string RootName = "Kelkaya Sinkhole Environment";
    public const string PreviewRootName = "Kelkaya Sinkhole Environment Preview";
    public static readonly Vector3 EnvironmentOffset = new Vector3(-280.42f, -477.52f, -67.97f);
    private const string ModelPath = "Level2/KelkayaSinkhole/level2_cave_repaired.glb";
    // GLB (-293,455.6,77), converted by glTFast (negated X), then recentered.
    public static readonly Vector3 FusedWaterCenter = new Vector3(12.58f, -21.92f, 9.03f);
    public static readonly Vector2 CaveWaterRadii = new Vector2(12f, 12f);
    public static void ExpandMap(Transform root)
    {
        root.localScale = new Vector3(2f,1f,2f);
        Vector3 delta = EnvironmentOffset - FusedWaterCenter;
        root.position = FusedWaterCenter + Vector3.Scale(delta, root.localScale);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterLoadLevel2Environment()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnLoadLevel2EnvironmentSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnLoadLevel2EnvironmentSceneLoaded;
    }

    private static void OnLoadLevel2EnvironmentSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        LoadLevel2Environment();
    }

    private static void LoadLevel2Environment()
    {
        if (SceneManager.GetActiveScene().name != "Level2")
            return;

        GameObject preview = GameObject.Find(PreviewRootName);
        if (preview != null)
        {
            preview.SetActive(false);
            UnityEngine.Object.Destroy(preview);
        }

        if (GameObject.Find(RootName) == null)
            CreateRuntimeRootInCurrentScene();
    }

    public static GameObject CreateRuntimeRootInCurrentScene(bool createLevel2Gameplay = true, bool createSharedCave = false)
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            return existing;

        ConfigureAtmosphere();

        GameObject root = new GameObject(RootName);

        // The scan is stored far from its origin. This offset recentres its footprint
        // and places the highest scanned point at water level (Y = 0).
        ExpandMap(root.transform);

        Level2SinkholeRuntimeLoader loader = root.AddComponent<Level2SinkholeRuntimeLoader>();
        loader.ModelUrl = GetModelUrl();
        loader.CreateLevel2Gameplay = createLevel2Gameplay;
        loader.CreateSharedCave = createSharedCave;
        return root;
    }

    private static string GetModelUrl()
    {
        string path = Path.Combine(Application.streamingAssetsPath,
            ModelPath);

        // Windows and macOS standalone/editor builds need a proper file URI.
        // Platforms whose StreamingAssets path is already a URI (for example Android)
        // must keep that URI unchanged.
        if (Uri.TryCreate(path, UriKind.Absolute, out Uri uri) && !string.IsNullOrEmpty(uri.Scheme))
            return uri.IsFile ? uri.AbsoluteUri : path;

        return new Uri(Path.GetFullPath(path)).AbsoluteUri;
    }

    private static void ConfigureAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.012f;
        RenderSettings.fogColor = new Color(0.035f, 0.12f, 0.14f);
        RenderSettings.ambientLight = new Color(0.08f, 0.13f, 0.12f);
    }
}

public sealed class Level2PreviewMarker : MonoBehaviour
{
    public int Version;
}

[DisallowMultipleComponent]
public sealed class Level2SinkholeRuntimeLoader : MonoBehaviour
{
    // The old footprint only covered the centre of the photographed opening.
    // A player entering from the shaded approach could be visibly in the pool
    // while the movement code still classified the position as dry land.
    private static readonly Vector2 WaterRadii = Level2SinkholeBootstrap.CaveWaterRadii;
    private const float Level2PlayableDepth = 36f;

    [SerializeField] private string modelUrl;
    [SerializeField] private bool createLevel2Gameplay = true;
    [SerializeField] private bool createSharedCave = true;
    private bool loading;

    public string ModelUrl
    {
        get => modelUrl;
        set => modelUrl = value;
    }

    public bool CreateLevel2Gameplay
    {
        get => createLevel2Gameplay;
        set => createLevel2Gameplay = value;
    }

    public bool CreateSharedCave
    {
        get => createSharedCave;
        set => createSharedCave = value;
    }

    private async void Start()
    {
        if (!Application.isPlaying || loading || string.IsNullOrEmpty(ModelUrl))
            return;

        loading = true;
        GltfAsset asset = gameObject.AddComponent<GltfAsset>();
        asset.LoadOnStartup = false;
        asset.SceneId = -1;
        asset.PlayAutomatically = false;

        Debug.Log($"Loading fused Kelkaya/Zacaton cave from: {ModelUrl}");

        try
        {
            bool success = await asset.Load(ModelUrl);

            if (success)
            {
                Debug.Log("Shared fused Kelkaya/Zacaton cave loaded successfully.");
                Bounds bounds = GetModelBounds();
                EnhanceEnvironmentMaterials();
                AddEnvironmentColliders();
                CenoteLandscape.Build(transform, bounds);
                Vector3 waterCenter = CreateWaterSurface(bounds);
                if (CreateSharedCave)
                    CreateOriginalZacatonBelow(waterCenter);
                if (CreateLevel2Gameplay)
                {
                    // The shared cave mesh supplies walls and its real bottom.
                    // Do not seal the connection again with the old 36 m box.
                    CreateSurveyMarker(waterCenter);
                    CreatePlayerAtEntrance(bounds, waterCenter);
                }
                else if (SceneManager.GetActiveScene().name == "Level3")
                {
                    CreateSurveyMarker(waterCenter);
                    Level3AutoBootstrap.PlacePlayerAtLoadedEntrance(bounds, waterCenter);
                }
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
            }
            else
                Debug.LogError($"Level 2 sinkhole failed to load: {ModelUrl}");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            loading = false;
        }
    }

    private void CreateOriginalZacatonBelow(Vector3 waterCenter)
    {
        GameObject cave = new GameObject("Zacaton Cave - Directly Below Kelkaya");
        cave.transform.SetParent(transform, true);
        Level3AutoBootstrap builder = cave.AddComponent<Level3AutoBootstrap>();
        GameObject model = builder.CreateOriginalZacatonModel(waterCenter);
        if (model != null)
        {
            model.SetActive(true);
            Debug.Log($"Original Zacaton FBX is active directly below the Kelkaya pool at {waterCenter}.");
        }
    }

    private Bounds GetModelBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogError("The sinkhole loaded, but no renderers were instantiated.");
            return new Bounds(transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Debug.Log($"Sinkhole renderers: {renderers.Length}, bounds center: {bounds.center}, size: {bounds.size}");
        return bounds;
    }

    public void AddEnvironmentColliders()
    {
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>();
        int colliderCount = 0;

        // PhysX cannot represent the shear produced by the imported hierarchy
        // under a nonuniform root scale. Bake the rendered world vertices and
        // keep collision transforms axis-aligned instead.
        Transform collisionRoot = new GameObject("Cave World Collision").transform;
        collisionRoot.SetParent(transform, false);
        collisionRoot.position = Vector3.zero;
        collisionRoot.rotation = Quaternion.identity;
        Vector3 scale = transform.lossyScale;
        collisionRoot.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);

        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null)
                continue;

            foreach (Collider oldCollider in filter.GetComponents<Collider>()) oldCollider.enabled = false;
            Vector3[] vertices = filter.sharedMesh.vertices;
            Matrix4x4 matrix = filter.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
            int[] triangles = filter.sharedMesh.triangles;
            if (matrix.determinant < 0f)
                for (int i = 0; i < triangles.Length; i += 3)
                { int swap = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = swap; }
            Mesh mesh = new Mesh { name = filter.name + " World Collision", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            GameObject surface = new GameObject(filter.name + " Collision");
            surface.transform.SetParent(collisionRoot, false);
            var ground = surface.AddComponent<CaveGroundSurface>();
            ground.source = filter.GetComponent<Renderer>();
            ground.ownedMesh = mesh;
            MeshCollider collider = surface.AddComponent<MeshCollider>();
            // The lower scan exceeds PhysX's fast-midphase triangle limit.
            collider.cookingOptions &= ~MeshColliderCookingOptions.UseFastMidphase;
            collider.sharedMesh = mesh;
            colliderCount++;
        }

        Debug.Log($"Level 2 environment colliders created: {colliderCount}");
    }

    private void EnhanceEnvironmentMaterials()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.materials)
            {
                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", 0.36f);
                if (material.HasProperty("_Metallic"))
                    material.SetFloat("_Metallic", 0f);
            }
        }
    }

    public static GameObject CreateMapPerimeter(Transform parent, Bounds bounds)
    {
        const string perimeterName = "Natural Rock Perimeter";
        Transform existing = parent != null ? parent.Find(perimeterName) : null;
        if (existing != null)
            return existing.gameObject;

        GameObject root = new GameObject(perimeterName);
        if (parent != null)
            root.transform.SetParent(parent, true);

        float wallBottom = bounds.min.y - 7f;
        float nominalTop = bounds.max.y + 16f;
        float wallThickness = 8f;

        // The scan edge is protected by an invisible collision cage. Visible
        // primitive mounds looked like a necklace of black spheres when seen
        // from below, so the photogrammetry remains the only visible terrain.
        CreateInvisibleBoundary(root.transform, "North Boundary Collider",
            new Vector3(bounds.center.x, (wallBottom + nominalTop) * 0.5f, bounds.max.z + 2.5f),
            new Vector3(bounds.size.x + 14f, nominalTop - wallBottom, wallThickness));
        CreateInvisibleBoundary(root.transform, "South Boundary Collider",
            new Vector3(bounds.center.x, (wallBottom + nominalTop) * 0.5f, bounds.min.z - 2.5f),
            new Vector3(bounds.size.x + 14f, nominalTop - wallBottom, wallThickness));
        CreateInvisibleBoundary(root.transform, "East Boundary Collider",
            new Vector3(bounds.max.x + 2.5f, (wallBottom + nominalTop) * 0.5f, bounds.center.z),
            new Vector3(wallThickness, nominalTop - wallBottom, bounds.size.z + 14f));
        CreateInvisibleBoundary(root.transform, "West Boundary Collider",
            new Vector3(bounds.min.x - 2.5f, (wallBottom + nominalTop) * 0.5f, bounds.center.z),
            new Vector3(wallThickness, nominalTop - wallBottom, bounds.size.z + 14f));

        CreateInvisibleBoundary(root.transform, "Deep Cave Bottom Guard",
            new Vector3(bounds.center.x, bounds.min.y - 6f, bounds.center.z),
            new Vector3(bounds.size.x + 16f, 10f, bounds.size.z + 16f));

        Debug.Log($"Natural rock perimeter created around the fused cave bounds {bounds.size}.");
        return root;
    }

    private static void CreateInvisibleBoundary(
        Transform parent,
        string boundaryName,
        Vector3 worldPosition,
        Vector3 worldScale)
    {
        GameObject boundary = new GameObject(boundaryName);
        boundary.transform.SetParent(parent, true);
        boundary.transform.position = worldPosition;
        Vector3 parentScale = parent.lossyScale;
        boundary.transform.localScale = new Vector3(worldScale.x / parentScale.x,
            worldScale.y / parentScale.y, worldScale.z / parentScale.z);
        boundary.AddComponent<BoxCollider>();
    }

    private Vector3 CreateWaterSurface(Bounds bounds)
    {
        GameObject water = new GameObject("Single Cave Water Surface");
        // The fused mesh includes the deep Zacaton section. Deriving water
        // height from the combined bounds would move the surface far below the
        // photographed Kelkaya opening, so retain the verified opening anchor.
        // Align the authored water to the closure face in the currently loaded
        // fused model. The map was replaced after the original hard-coded Y was
        // measured, so reusing that old height separated visuals and gameplay.
        Vector3 resolvedCenter = Level2SinkholeBootstrap.FusedWaterCenter;
        water.transform.position = resolvedCenter;

        MeshFilter filter = water.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateCaveWaterMesh(64, WaterRadii.x, WaterRadii.y);

        MeshRenderer renderer = water.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("GP1/Level2/Kelkaya Water");
        if (shader == null)
        {
            Debug.LogError("Level 2 water shader was not found.");
            return water.transform.position;
        }

        renderer.sharedMaterial = ClearWaterMaterial.Create(false);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Debug.Log($"Single cave water surface created at {water.transform.position}.");
        return water.transform.position;
    }

    private Vector3 ResolveWaterCenter(Bounds bounds)
    {
        Vector3 center = Level2SinkholeBootstrap.FusedWaterCenter;
        Vector3 origin = new Vector3(center.x, bounds.max.y + 8f, center.z);
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            bounds.size.y + 30f,
            ~0,
            QueryTriggerInteraction.Ignore);

        float bestDelta = float.PositiveInfinity;
        float bestY = center.y;
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == null || !hit.transform.IsChildOf(transform))
                continue;

            float delta = Mathf.Abs(hit.point.y - center.y);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestY = hit.point.y;
            }
        }

        // Sit above the scan cap so the animated surface wins the depth test.
        // Reject unrelated cave floors when the expected opening was not hit.
        if (bestDelta <= 6f)
            center.y = bestY + 0.28f;
        else
            center.y += 0.28f;

        Debug.Log($"Resolved Level 2 water anchor to {center} (scan delta {bestDelta:0.00} m).");
        return center;
    }

    private static Mesh CreateCaveWaterMesh(int segments, float radiusX, float radiusZ)
    {
        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uv = new Vector2[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            float edgeNoise = 1f + Mathf.Sin(i * 1.73f) * 0.018f + Mathf.Sin(i * 0.41f) * 0.028f;
            vertices[i + 1] = new Vector3(
                Mathf.Cos(angle) * radiusX * edgeNoise,
                0f,
                Mathf.Sin(angle) * radiusZ * edgeNoise
            );
            uv[i + 1] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f + Vector2.one * 0.5f;

            int triangle = i * 3;
            triangles[triangle] = 0;
            // Reverse the old winding so the authored water face and normals
            // point upward into the playable scene, not out of the map.
            triangles[triangle + 1] = i == segments - 1 ? 1 : i + 2;
            triangles[triangle + 2] = i + 1;
        }

        Mesh mesh = new Mesh { name = "Kelkaya Cave Water Mesh" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void CreateLevel2WaterContainment(Vector3 waterCenter)
    {
        GameObject root = new GameObject("Level 2 Water Containment");
        root.transform.SetParent(transform, true);

        float bottomY = waterCenter.y - Level2PlayableDepth;
        float wallTopY = waterCenter.y - 0.55f;
        float wallHeight = wallTopY - bottomY + 2f;
        float wallCenterY = bottomY - 1f + wallHeight * 0.5f;
        float thickness = 1.2f;

        CreateInvisibleBoundary(root.transform, "Water Bottom Collider",
            new Vector3(waterCenter.x, bottomY - 1f, waterCenter.z),
            new Vector3(WaterRadii.x * 2f + 2f, 2f, WaterRadii.y * 2f + 2f));
        CreateInvisibleBoundary(root.transform, "Water North Collider",
            new Vector3(waterCenter.x, wallCenterY, waterCenter.z + WaterRadii.y),
            new Vector3(WaterRadii.x * 2f + 2f, wallHeight, thickness));
        CreateInvisibleBoundary(root.transform, "Water South Collider",
            new Vector3(waterCenter.x, wallCenterY, waterCenter.z - WaterRadii.y),
            new Vector3(WaterRadii.x * 2f + 2f, wallHeight, thickness));
        CreateInvisibleBoundary(root.transform, "Water East Collider",
            new Vector3(waterCenter.x + WaterRadii.x, wallCenterY, waterCenter.z),
            new Vector3(thickness, wallHeight, WaterRadii.y * 2f + 2f));
        CreateInvisibleBoundary(root.transform, "Water West Collider",
            new Vector3(waterCenter.x - WaterRadii.x, wallCenterY, waterCenter.z),
            new Vector3(thickness, wallHeight, WaterRadii.y * 2f + 2f));
    }

    private void CreateSurveyMarker(Vector3 waterCenter)
    {
        foreach (float depth in new[] { 25f, 50f, 75f })
            CreateSurveyMarker(waterCenter, depth);
    }

    private void CreateSurveyMarker(Vector3 waterCenter, float depth)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        string levelNumber = SceneManager.GetActiveScene().name == "Level3" ? "3" : "2";
        marker.name = $"Level {levelNumber} Survey Marker {depth:0}m";
        marker.transform.SetParent(transform, true);
        marker.transform.position = waterCenter + Vector3.down * depth;
        marker.transform.localScale = Vector3.one * 1.25f;
        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
            Destroy(markerCollider);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        Material glow = new Material(shader) { name = "Level 2 Survey Cyan" };
        Color cyan = new Color(0.05f, 0.92f, 1f, 1f);
        if (glow.HasProperty("_BaseColor"))
            glow.SetColor("_BaseColor", cyan);
        if (glow.HasProperty("_Color"))
            glow.SetColor("_Color", cyan);
        marker.GetComponent<MeshRenderer>().sharedMaterial = glow;

        RotatingDepthMarker rotation = marker.AddComponent<RotatingDepthMarker>();
        rotation.speed = 38f;
        DepthTrainingMarker objective = marker.AddComponent<DepthTrainingMarker>();
        objective.Depth = depth;

        GameObject ring = new GameObject($"{depth:0}m Survey Ring");
        ring.transform.SetParent(marker.transform, false);
        ring.transform.localScale = Vector3.one;
        LineRenderer line = ring.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 72;
        line.startWidth = 0.075f;
        line.endWidth = 0.075f;
        line.sharedMaterial = glow;
        line.startColor = cyan;
        line.endColor = cyan;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i / (float)line.positionCount * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * 3f, 0f, Mathf.Sin(angle) * 3f));
        }
        objective.ObjectiveRing = ring;

        Light markerLight = marker.AddComponent<Light>();
        markerLight.type = LightType.Point;
        markerLight.color = cyan;
        markerLight.intensity = 1.35f;
        markerLight.range = 8f;
    }

    public static void GetEntrancePose(Bounds bounds, float outsideDistance, out Vector3 entrance, out Vector3 spawnPosition)
    {
        // The photographed entrance is on the left side of the far rock face.
        // Spawn ten metres outward from it, along the front-facing direction.
        entrance = bounds.center + new Vector3(
            -bounds.extents.x * 0.28f,
            bounds.extents.y * 0.08f,
            bounds.extents.z * 0.28f
        );
        spawnPosition = entrance + Vector3.back * outsideDistance;

        Physics.SyncTransforms();
        Vector3 rayOrigin = new Vector3(spawnPosition.x, bounds.max.y + 10f, spawnPosition.z);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, bounds.size.y + 40f))
            spawnPosition.y = hit.point.y + 1.05f;
        else
            spawnPosition.y = entrance.y + 2f;
    }

    public static void GetWaterEntrancePose(
        Bounds bounds,
        Vector3 waterCenter,
        float outsideDistance,
        out Vector3 entrance,
        out Vector3 spawnPosition)
    {
        // The water placement is the verified opening in this photogrammetry
        // model. The older bounds-derived anchor is roughly 17 m behind it and
        // placed the player below an intervening rise in the terrain.
        entrance = waterCenter;
        spawnPosition = waterCenter + Vector3.back * outsideDistance;

        Physics.SyncTransforms();
        Vector3 rayOrigin = new Vector3(spawnPosition.x, bounds.max.y + 10f, spawnPosition.z);
        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                bounds.size.y + 40f,
                ~0,
                QueryTriggerInteraction.Ignore))
        {
            spawnPosition.y = hit.point.y + 1.05f;
        }
        else
        {
            // Retain a safe fallback, but keep it above the water rather than
            // using the unverified bounds anchor.
            spawnPosition.y = Mathf.Max(waterCenter.y + 2f, bounds.center.y + 1f);
        }
    }

    public static void GetShadowSideSpawnPose(
        Bounds bounds,
        Vector3 waterCenter,
        out Vector3 entrance,
        out Vector3 spawnPosition)
    {
        entrance = waterCenter;

        Vector3 preferred = waterCenter + new Vector3(
            -bounds.extents.x * 0.32f,
            0f,
            -bounds.extents.z * 0.24f);

        Physics.SyncTransforms();
        // A single ray can hit a steep scan fragment, or the cave floor below
        // a hole. Require dry ground across the whole standing footprint.
        int searchRings = Mathf.CeilToInt(Mathf.Max(bounds.size.x, bounds.size.z) / 2f);
        for (int ring = 0; ring <= searchRings; ring++)
        {
            int samples = ring == 0 ? 1 : 24;
            for (int sample = 0; sample < samples; sample++)
            {
                float angle = sample * Mathf.PI * 2f / samples;
                Vector3 candidate = preferred + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * 2f);
                if (TryGetDryStandingPose(bounds, waterCenter, candidate, out spawnPosition))
                    return;
            }
        }
        throw new InvalidOperationException("No supported dry spawn found on the cave approach; player creation stopped.");
    }

    private static bool TryGetDryStandingPose(Bounds bounds, Vector3 waterCenter,
        Vector3 candidate, out Vector3 standingPosition)
    {
        standingPosition = default;
        Vector2 relative = new Vector2(candidate.x - waterCenter.x, candidate.z - waterCenter.z);
        if (relative.magnitude < Mathf.Max(WaterRadii.x, WaterRadii.y) + 3f)
            return false;

        float centerHeight = 0f;
        Vector3[] offsets = { Vector3.zero, Vector3.right * .6f, Vector3.left * .6f,
            Vector3.forward * .6f, Vector3.back * .6f };
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 origin = candidate + offsets[i];
            origin.y = bounds.max.y + 10f;
            if (!TryFindVisibleCaveGround(origin, bounds.size.y + 40f, out RaycastHit ground) ||
                ground.normal.y < .6f || ground.point.y < waterCenter.y + .5f ||
                ground.collider.GetComponentInParent<SimpleMove>() != null)
                return false;
            if (i == 0)
                centerHeight = ground.point.y;
            else if (Mathf.Abs(ground.point.y - centerHeight) > .3f)
                return false;
        }
        standingPosition = new Vector3(candidate.x, centerHeight + 1.05f, candidate.z);
        // The wall probe must also fit without starting inside the scan.
        return !Physics.CheckSphere(standingPosition + Vector3.up * .5f, .53f,
            ~0, QueryTriggerInteraction.Ignore);
    }

    public static bool IsVisibleCaveGround(Collider collider)
    {
        MeshCollider meshCollider = collider as MeshCollider;
        if (meshCollider == null || meshCollider.convex) return false;
        CaveGroundSurface surface = collider.GetComponent<CaveGroundSurface>();
        Renderer renderer = surface != null ? surface.source : null;
        return surface != null &&
            renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy &&
            collider.GetComponentInParent<Level2SinkholeRuntimeLoader>() != null;
    }

    public static bool TryFindVisibleCaveGround(Vector3 origin, float distance, out RaycastHit ground)
    {
        ground = default;
        float nearest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!IsVisibleCaveGround(hit.collider) || hit.distance >= nearest || hit.normal.y <= .01f) continue;
            nearest = hit.distance;
            ground = hit;
        }
        return nearest < float.PositiveInfinity;
    }

    private void CreatePlayerAtEntrance(Bounds bounds, Vector3 waterCenter)
    {
        GetShadowSideSpawnPose(
            bounds,
            waterCenter,
            out Vector3 entrance,
            out Vector3 spawnPosition);

        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.transform.position = spawnPosition;

        // This is a first-person controller. Keeping the generated capsule
        // visible places the camera inside its mesh and produces a large white
        // clipping shape at the edge of the screen.
        MeshRenderer playerRenderer = player.GetComponent<MeshRenderer>();
        if (playerRenderer != null)
            playerRenderer.enabled = false;

        // Face the actual pool rather than the approximate cave anchor. The
        // latter can point at an incomplete edge of the photogrammetry scan.
        Vector3 horizontalTarget = new Vector3(waterCenter.x, spawnPosition.y, waterCenter.z);
        player.transform.LookAt(horizontalTarget);

        Rigidbody body = player.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;

        PlayerGearState gear = player.AddComponent<PlayerGearState>();
        gear.hasDivingGear = false;

        SimpleMove movement = player.AddComponent<SimpleMove>();
        movement.ApplyLevel1ControlProfile();
        movement.ApplyLevel2Pace();
        movement.waterSurfaceY = waterCenter.y;
        movement.useBoundedWaterArea = true;
        movement.waterAreaCenterXZ = new Vector2(waterCenter.x, waterCenter.z);
        movement.waterAreaRadii = WaterRadii;
        movement.deepWaterAreaRadii = new Vector2(42f, 34f);
        movement.waterBottomY = bounds.min.y - 1f;
        movement.constrainToWaterVolume = false;
        Level2MissionController mission = player.AddComponent<Level2MissionController>();
        mission.movement = movement;
        mission.waterSurfaceY = movement.waterSurfaceY;
        mission.startingPosition = spawnPosition;
        movement.groundLayer = ~0;
        movement.requireVisibleCaveGround = true;

        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        // Imported FBX/GLB assets may contain authoring cameras. One Zacaton
        // camera was rendering after the player camera and clearing the whole
        // screen to grey, which made every spawn position appear broken.
        foreach (Camera otherCamera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (otherCamera != camera)
                otherCamera.enabled = false;
        }
        foreach (AudioListener listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            if (listener.gameObject != camera.gameObject)
                listener.enabled = false;
        }

        camera.transform.SetParent(player.transform, false);
        AudioListener playerListener = camera.GetComponent<AudioListener>();
        if (playerListener == null) playerListener = camera.gameObject.AddComponent<AudioListener>();
        playerListener.enabled = true;
        camera.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        camera.transform.localRotation = Quaternion.identity;
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 600f;
        camera.depth = 10f;

        MouseLook look = camera.GetComponent<MouseLook>();
        if (look == null)
            look = camera.gameObject.AddComponent<MouseLook>();
        look.playerBody = player.transform;
        look.ApplyLevel1ControlProfile();

        // Start with the photographed approach and water visible. MouseLook
        // may already have run Start while the GLB was loading, so apply the
        // pitch immediately as well as storing it for future starts.
        look.SetPitch(11f);

        mission.coachDialogue = CreateLevel2Coach(spawnPosition, waterCenter);
        CreateLevel2GearStation(player.transform, spawnPosition, waterCenter);
        CreateLevel2WaterEntryGuard(player.transform, waterCenter);

        Debug.Log($"Level 2 player spawned on the shaded approach at {spawnPosition}, facing the water at {waterCenter}.");
    }

    private static Level2CoachDialogue CreateLevel2Coach(Vector3 spawnPosition, Vector3 waterCenter)
    {
        Vector3 forward = Vector3.ProjectOnPlane(waterCenter - spawnPosition, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 coachPosition = spawnPosition + right * 5f + forward * 3f;

        if (TryFindVisibleCaveGround(coachPosition + Vector3.up * 15f, 35f, out RaycastHit hit))
            coachPosition.y = hit.point.y;

        GameObject coach = new GameObject("Level 2 Diving Coach");
        Level1CoachLoader loader = coach.AddComponent<Level1CoachLoader>();
        loader.UseOverridePosition = true;
        loader.OverridePosition = coachPosition;
        Level2CoachDialogue dialogue = coach.AddComponent<Level2CoachDialogue>();
        return dialogue;
    }

    private static void CreateLevel2GearStation(Transform player, Vector3 spawnPosition, Vector3 waterCenter)
    {
        Vector3 forward = Vector3.ProjectOnPlane(waterCenter - spawnPosition, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 gearPosition = spawnPosition - right * 5f + forward * 3f;

        if (TryFindVisibleCaveGround(gearPosition + Vector3.up * 15f, 35f, out RaycastHit hit))
            gearPosition.y = hit.point.y + 0.08f;

        GameObject gearRoot = new GameObject("Level 2 Shore Diving Gear");
        gearRoot.transform.position = gearPosition;
        gearRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        var model = Resources.Load<GameObject>("Equipment/Level1DivingGear");
        if (model == null) throw new InvalidOperationException("Missing Level1 diving gear prefab");
        var visual = UnityEngine.Object.Instantiate(model, gearRoot.transform, false);
        visual.name = "Level1 Diving Gear Model";
        visual.transform.localScale = Vector3.one * .5f;
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.position += Vector3.up * (gearPosition.y - bounds.min.y);
        }

        DivingGearPickup pickup = gearRoot.AddComponent<DivingGearPickup>();
        pickup.player = player;
        pickup.interactDistance = 3f;
        pickup.hintText = "Press E to wear diving gear";
        pickup.textHeight = 2.4f;
        pickup.textForwardDistance = 0.4f;
        pickup.textSize = 3.2f;
    }

    private static void CreateGearPart(
        Transform parent,
        PrimitiveType type,
        string partName,
        Vector3 localPosition,
        Vector3 localScale,
        Color color)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = partName + " Material" };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void CreateLevel2WaterEntryGuard(Transform player, Vector3 waterCenter)
    {
        GameObject guardObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        guardObject.name = "Level 2 Water Entry Guard";
        guardObject.transform.position = waterCenter + Vector3.down * 1.35f;
        guardObject.transform.localScale = new Vector3(WaterRadii.x * 2f, 3.2f, WaterRadii.y * 2f);
        guardObject.GetComponent<Renderer>().enabled = false;
        guardObject.GetComponent<Collider>().isTrigger = true;

        WaterEntryGuard guard = guardObject.AddComponent<WaterEntryGuard>();
        guard.player = player;
        guard.gearState = player.GetComponent<PlayerGearState>();
        guard.playerMove = player.GetComponent<SimpleMove>();
        guard.warningMessage = "Wear the shore diving gear before entering.";
        guard.warningHeight = 2.5f;
        guard.warningSize = 3.2f;
        guard.warningDistance = 13f;
    }
}

public sealed class CaveGroundSurface : MonoBehaviour
{
    public Renderer source;
    [NonSerialized] public Mesh ownedMesh;
    private void OnDestroy()
    {
        if (ownedMesh == null) return;
        if (Application.isPlaying) Destroy(ownedMesh); else DestroyImmediate(ownedMesh);
    }
}

