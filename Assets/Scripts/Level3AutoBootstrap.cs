using UnityEngine;
using UnityEngine.SceneManagement;

public class Level3AutoBootstrap : MonoBehaviour
{
    public const string RootName = "Level3BootstrapRoot";
    public const int SharedMapVersion = 16;
    public static readonly Vector3 RuntimeCaveAnchor = Level2SinkholeBootstrap.FusedWaterCenter;
    public static readonly Vector3 EditorCaveAnchor = RuntimeCaveAnchor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBootstrapLevel3()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnBootstrapLevel3SceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnBootstrapLevel3SceneLoaded;
    }

    private static void OnBootstrapLevel3SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        BootstrapLevel3();
    }

    private static void BootstrapLevel3()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.name != "Level3")
            return;

        GameObject exteriorPreview = GameObject.Find(Level2SinkholeBootstrap.PreviewRootName);
        if (exteriorPreview != null)
        {
            exteriorPreview.SetActive(false);
            DestroySceneObject(exteriorPreview);
        }

        if (GameObject.Find(Level2SinkholeBootstrap.RootName) == null)
            Level2SinkholeBootstrap.CreateRuntimeRootInCurrentScene(false, false);

        GameObject root = GameObject.Find(RootName);
        if (root != null)
        {
            root.SetActive(false);
            DestroySceneObject(root);
        }
        root = new GameObject(RootName);

        Level3AutoBootstrap bootstrap = root.GetComponent<Level3AutoBootstrap>();
        if (bootstrap == null)
            bootstrap = root.AddComponent<Level3AutoBootstrap>();

        bootstrap.Build();
    }

    public static GameObject RebuildScene()
    {
        GameObject existingRoot = GameObject.Find(RootName);
        if (existingRoot != null)
            DestroySceneObject(existingRoot);

        GameObject root = new GameObject(RootName);
        Level3AutoBootstrap bootstrap = root.AddComponent<Level3AutoBootstrap>();
        bootstrap.Build();

        return root;
    }

    public void Build()
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.01f, 0.09f, 0.12f);
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.018f;
        RenderSettings.ambientLight = new Color(0.02f, 0.08f, 0.1f);

        gameObject.AddComponent<Level3SharedMapMarker>().Version = SharedMapVersion;
        transform.position = Vector3.zero;
        CreateLightRig();
        Transform player = CreatePlayer();
        // Kelkaya and Zacaton now arrive as one sealed fused GLB through the
        // shared Level 2 environment loader. Do not instantiate the old FBX.

        Vector3 caveAnchor = Application.isPlaying ? RuntimeCaveAnchor : EditorCaveAnchor;
        // Runtime placement is finalized by the Kelkaya loader after its meshes
        // and colliders exist. This temporary pose is never used for gameplay.
        player.position = Application.isPlaying
            ? new Vector3(-29.63f, 4f, 7.95f)
            : EditorCaveAnchor + new Vector3(0f, 3f, -27f);
        PlayerGearState gear = player.GetComponent<PlayerGearState>();
        gear.hasDivingGear = false;
        SimpleMove move = player.GetComponent<SimpleMove>();
        move.EnvironmentReady = !Application.isPlaying;
        move.waterSurfaceY = caveAnchor.y;
        move.useBoundedWaterArea = true;
        move.waterAreaCenterXZ = new Vector2(caveAnchor.x, caveAnchor.z);
        move.waterAreaRadii = Level2SinkholeBootstrap.CaveWaterRadii;
        move.waterBottomY = caveAnchor.y - 322f;

        Level3MissionController mission = gameObject.AddComponent<Level3MissionController>();
        mission.player = player;
        mission.movement = move;
        mission.waterSurfaceY = caveAnchor.y;
        mission.startingPosition = player.position;
        mission.recoveryTarget = CreateRecoveryBody(player, mission);
        mission.coachDialogue = CreateRecoveryCoach(player, caveAnchor);
        DivingGearPickup recoveryGear = CreateDiveGearStation(player);
        mission.gearStation = recoveryGear != null ? recoveryGear.transform : null;
        mission.ConfigureSurfaceSetup(player.position, caveAnchor);
        CenoteLandscape.ConfigureSky();
    }

    public static void PlacePlayerAtLoadedEntrance(Bounds bounds, Vector3 waterCenter)
    {
        GameObject root = GameObject.Find(RootName);
        if (root == null)
            return;

        Transform player = root.transform.Find("Player");
        if (player == null)
            return;

        // The old 18 m offset hits the lower cave floor in the expanded scan
        // (19 m below the surface). Use the verified dry approach shared by Level 2.
        Level2SinkholeRuntimeLoader.GetShadowSideSpawnPose(
            bounds,
            waterCenter,
            out Vector3 entrance,
            out Vector3 spawnPosition);
        player.position = spawnPosition;
        player.LookAt(new Vector3(waterCenter.x, spawnPosition.y, waterCenter.z));

        MouseLook look = player.GetComponentInChildren<MouseLook>();
        if (look != null)
            look.SetPitch(14f);

        SimpleMove movement = player.GetComponent<SimpleMove>();
        if (movement != null)
        {
            movement.waterSurfaceY = waterCenter.y;
            movement.waterAreaCenterXZ = new Vector2(waterCenter.x, waterCenter.z);
            movement.waterAreaRadii = Level2SinkholeBootstrap.CaveWaterRadii;
            movement.deepWaterAreaRadii = new Vector2(42f, 34f);
            movement.waterBottomY = bounds.min.y - 1f;
            movement.constrainToWaterVolume = false;
            movement.ResetVerticalVelocity();
            movement.requireVisibleCaveGround = true;
        }

        Level3MissionController mission = root.GetComponent<Level3MissionController>();
        if (mission != null)
        {
            mission.waterSurfaceY = waterCenter.y;
            mission.ConfigureSurfaceSetup(spawnPosition, waterCenter);

            Physics.SyncTransforms();
            Vector3 recoveryPoint = FindRecoveryFloor(waterCenter, bounds);
            if (mission.recoveryTarget != null)
            {
                mission.recoveryTarget.position = recoveryPoint;
                mission.recoveryTarget.GetComponent<Level3DiverModelLoader>().SetFloorHeight(recoveryPoint.y);
            }
            mission.targetDepthMeters = Mathf.Max(25f, waterCenter.y - recoveryPoint.y);
        }

        Physics.SyncTransforms();
        if (movement != null)
            movement.EnvironmentReady = true;
        Debug.Log($"Level 3 player spawned outside the fused cave at {spawnPosition}; recovery target aligned to its bottom.");
    }

    private Level3CoachDialogue CreateRecoveryCoach(Transform player, Vector3 caveAnchor)
    {
        Vector3 forward = Vector3.ProjectOnPlane(caveAnchor - player.position, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 coachPosition = player.position + right * 3.2f + forward * 1.2f;

        GameObject coach = new GameObject("Level 3 Recovery Coach");
        coach.transform.SetParent(transform, true);
        Level1CoachLoader loader = coach.AddComponent<Level1CoachLoader>();
        loader.UseOverridePosition = true;
        loader.OverridePosition = coachPosition;
        return coach.AddComponent<Level3CoachDialogue>();
    }

    public GameObject CreateOriginalZacatonModel(Vector3 worldAnchor)
    {
        GameObject source = Resources.Load<GameObject>("Level3/OriginalZacaton");
        if (source == null)
        {
            Debug.LogError("Original Zacaton FBX was not found at Resources/Level3/OriginalZacaton.fbx.");
            return null;
        }

        GameObject model = Instantiate(source, worldAnchor, Quaternion.identity, transform);
        model.name = "Original Zacaton Cave Model";
        model.transform.localScale = Vector3.one;

        // The source FBX contains an authoring camera. It is part of the asset,
        // not gameplay, and must never render over the first-person camera.
        foreach (Camera importedCamera in model.GetComponentsInChildren<Camera>(true))
            importedCamera.enabled = false;
        foreach (AudioListener importedListener in model.GetComponentsInChildren<AudioListener>(true))
            importedListener.enabled = false;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            // FBX preview/import state must never make the shared Zacaton cave
            // disappear at runtime. Only its authoring camera is disabled.
            foreach (Renderer renderer in renderers)
                renderer.enabled = true;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            // Preserve the original FBX rotation and 1:1 scale. Only translate it:
            // horizontal centre matches the pool and its highest point starts just
            // below the water surface, regardless of the FBX author's pivot.
            Vector3 correction = new Vector3(
                worldAnchor.x - bounds.center.x,
                worldAnchor.y - 0.5f - bounds.max.y,
                worldAnchor.z - bounds.center.z);
            model.transform.position += correction;

            Bounds finalBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                finalBounds.Encapsulate(renderers[i].bounds);

            Debug.Log($"Original Zacaton is visible below Kelkaya. Final bounds: center {finalBounds.center}, size {finalBounds.size}, top {finalBounds.max.y:0.00}, water {worldAnchor.y:0.00}.");
        }
        else
        {
            Debug.LogError("Original Zacaton FBX contains no renderers.");
        }

        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null)
                continue;
            MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
        }
        return model;
    }

    private Transform CreatePlayer()
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.SetParent(transform, false);
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 20.4f, 31f);
        player.transform.localScale = new Vector3(1f, 1f, 1f);
        player.layer = LayerMask.NameToLayer("Default");

        MeshRenderer playerRenderer = player.GetComponent<MeshRenderer>();
        if (playerRenderer != null)
            playerRenderer.enabled = false;

        Rigidbody rb = player.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        PlayerGearState gear = player.AddComponent<PlayerGearState>();
        gear.hasDivingGear = false;

        SimpleMove move = player.AddComponent<SimpleMove>();
        move.ApplyLevel1ControlProfile();
        move.ApplyOpenWaterCurrent();
        move.waterSurfaceY = 0f;
        move.groundLayer = LayerMask.GetMask("Default");

        GameObject cameraObject = Camera.main != null
            ? Camera.main.gameObject
            : new GameObject("Main Camera");

        cameraObject.name = "Main Camera";
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        cameraObject.transform.localRotation = Quaternion.identity;

        Camera camera = cameraObject.GetComponent<Camera>();
        if (camera == null)
            camera = cameraObject.AddComponent<Camera>();

        camera.clearFlags = CameraClearFlags.Skybox;
        AudioListener playerListener = cameraObject.GetComponent<AudioListener>();
        if (playerListener == null) playerListener = cameraObject.AddComponent<AudioListener>();
        playerListener.enabled = true;
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 900f;

        MouseLook look = cameraObject.GetComponent<MouseLook>();
        if (look == null)
            look = cameraObject.AddComponent<MouseLook>();

        look.playerBody = player.transform;
        look.ApplyLevel1ControlProfile();

        return player.transform;
    }

    public static Vector3 FindRecoveryFloor(Vector3 center, Bounds bounds)
    {
        Vector3 best = new Vector3(center.x, bounds.min.y, center.z);
        bool found = false;
        // Start near the deepest mesh section so upper ledges cannot intercept.
        for (float x = -32; x <= 32; x += 2)
        for (float z = -28; z <= 28; z += 2)
        {
            if (x*x/(32*32)+z*z/(28*28)>1) continue;
            Vector3 origin = new Vector3(center.x+x, bounds.min.y+18, center.z+z);
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 20, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponent<CaveGroundSurface>() == null || hit.normal.y < .6f) continue;
                if (!found || hit.point.y < best.y)
                { best = hit.point; found = true; }
            }
        }
        if (!found) Debug.LogError("No recovery floor found in the deepest cave section.");
        return best;
    }

    private void AddPlayerLamp(Transform cameraTransform)
    {
        GameObject lampObject = new GameObject("Dive Lamp");
        lampObject.transform.SetParent(cameraTransform, false);
        lampObject.transform.localPosition = Vector3.zero;
        lampObject.transform.localRotation = Quaternion.identity;

        Light lamp = lampObject.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.color = new Color(0.75f, 0.95f, 1f);
        lamp.intensity = 5f;
        lamp.range = 85f;
        lamp.spotAngle = 48f;
    }

    private void CreateLightRig()
    {
        Light existingLight = FindFirstObjectByType<Light>();
        if (existingLight != null)
        {
            existingLight.type = LightType.Directional;
            existingLight.intensity = 0.6f;
            existingLight.color = new Color(0.55f, 0.75f, 0.85f);
            existingLight.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            return;
        }

        GameObject sunObject = new GameObject("Surface Light");
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0.6f;
        sun.color = new Color(0.55f, 0.75f, 0.85f);
        sunObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
    }

    private void CreateCliffRimTerrain()
    {
        GameObject terrainObject = new GameObject("High Cliff Shore");
        terrainObject.transform.SetParent(transform, false);

        Mesh mesh = CreateCliffRimMesh(144, 30f, 92f, 18f);

        MeshFilter filter = terrainObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = terrainObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateMaterial("Zacaton Limestone Cliff", new Color(0.19f, 0.17f, 0.13f, 1f), true);

        MeshCollider collider = terrainObject.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;

        CreateEntryPlatform();
        CreateVerticalCliffFace();
        CreateLimestoneStrata();
        CreateRockScatter();
        CreateCliffDepthMarkers();
    }

    private Mesh CreateCliffRimMesh(int segments, float innerRadius, float outerRadius, float rimHeight)
    {
        int rings = 8;
        Vector3[] vertices = new Vector3[(rings + 1) * segments];
        int[] triangles = new int[rings * segments * 6];

        for (int ring = 0; ring <= rings; ring++)
        {
            float t = ring / (float)rings;
            float radius = Mathf.Lerp(innerRadius, outerRadius, t);
            float height = Mathf.Lerp(rimHeight, rimHeight + 10f, Mathf.SmoothStep(0f, 1f, t));

            if (ring == 0)
                height = rimHeight;

            for (int segment = 0; segment < segments; segment++)
            {
                float angle = (segment / (float)segments) * Mathf.PI * 2f;
                float oval = 1f + Mathf.Cos(angle - 0.55f) * 0.16f + Mathf.Sin(angle * 2.0f + 0.4f) * 0.07f;
                float roughness = Mathf.Sin(segment * 0.43f + ring * 0.9f) * 2.3f
                    + Mathf.Sin(segment * 1.37f - ring * 0.3f) * 0.95f;
                float terrace = Mathf.Sin(t * Mathf.PI * 5f) * 1.5f * (1f - t * 0.35f);
                float edgeDrop = ring == 0 ? Mathf.Sin(segment * 1.3f) * 3.8f : 0f;

                vertices[ring * segments + segment] = new Vector3(
                    Mathf.Cos(angle) * (radius * oval + roughness),
                    height + terrace + edgeDrop,
                    Mathf.Sin(angle) * (radius / oval + roughness)
                );
            }
        }

        int index = 0;
        for (int ring = 0; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int current = ring * segments + segment;
                int next = ring * segments + (segment + 1) % segments;
                int outer = (ring + 1) * segments + segment;
                int outerNext = (ring + 1) * segments + (segment + 1) % segments;

                triangles[index++] = current;
                triangles[index++] = next;
                triangles[index++] = outer;
                triangles[index++] = next;
                triangles[index++] = outerNext;
                triangles[index++] = outer;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "High Cliff Shore"
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void CreateEntryPlatform()
    {
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "Diver Entry Ledge";
        platform.transform.SetParent(transform, false);
        platform.transform.position = new Vector3(0f, 18.9f, 34f);
        platform.transform.rotation = Quaternion.Euler(0f, -7f, 0f);
        platform.transform.localScale = new Vector3(18f, 1.2f, 15f);

        Renderer renderer = platform.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateMaterial("Entry Ledge Rock", new Color(0.22f, 0.2f, 0.15f, 1f));
    }

    private void CreateVerticalCliffFace()
    {
        GameObject cliffFace = new GameObject("Sheer Waterline Cliff Face");
        cliffFace.transform.SetParent(transform, false);

        Mesh mesh = CreateCliffFaceMesh(144, 30f, 18f, 0f);

        MeshFilter filter = cliffFace.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = cliffFace.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateMaterial("Wet Limestone Drop", new Color(0.09f, 0.1f, 0.085f, 1f), true);

        MeshCollider collider = cliffFace.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    private Mesh CreateCliffFaceMesh(int segments, float radius, float topY, float bottomY)
    {
        int verticalSteps = 14;
        Vector3[] vertices = new Vector3[(verticalSteps + 1) * segments];
        int[] triangles = new int[verticalSteps * segments * 6];

        for (int step = 0; step <= verticalSteps; step++)
        {
            float t = step / (float)verticalSteps;
            float y = Mathf.Lerp(topY, bottomY, t);

            for (int segment = 0; segment < segments; segment++)
            {
                float angle = (segment / (float)segments) * Mathf.PI * 2f;
                float oval = 1f + Mathf.Cos(angle - 0.55f) * 0.16f + Mathf.Sin(angle * 2.0f + 0.4f) * 0.07f;
                float roughness = Mathf.Sin(segment * 0.8f + step * 0.4f) * 1.2f
                    + Mathf.Sin(segment * 2.6f) * 0.45f
                    + Mathf.Sin(step * 2.4f) * 0.7f;
                float r = radius + roughness;

                vertices[step * segments + segment] = new Vector3(
                    Mathf.Cos(angle) * r * oval,
                    y,
                    Mathf.Sin(angle) * r / oval
                );
            }
        }

        int index = 0;
        for (int step = 0; step < verticalSteps; step++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int current = step * segments + segment;
                int next = step * segments + (segment + 1) % segments;
                int below = (step + 1) * segments + segment;
                int belowNext = (step + 1) * segments + (segment + 1) % segments;

                triangles[index++] = current;
                triangles[index++] = below;
                triangles[index++] = next;
                triangles[index++] = next;
                triangles[index++] = below;
                triangles[index++] = belowNext;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "Sheer Waterline Cliff Face"
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void CreateLimestoneStrata()
    {
        for (int i = 0; i < 9; i++)
        {
            float y = Mathf.Lerp(1.4f, 18.2f, i / 8f);
            float radius = Mathf.Lerp(29.2f, 31.4f, i / 8f);
            CreateStrataLine("Waterline Limestone Strata", y, radius, 0.05f + i * 0.005f, new Color(0.33f, 0.31f, 0.25f, 1f));
        }

        for (int i = 0; i < 6; i++)
        {
            float y = 20f + i * 1.25f;
            float radius = 42f + i * 5.5f;
            CreateStrataLine("Upper Shore Limestone Strata", y, radius, 0.06f, new Color(0.28f, 0.25f, 0.18f, 1f));
        }
    }

    private void CreateStrataLine(string name, float y, float radius, float width, Color color)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = 144;
        line.startWidth = width;
        line.endWidth = width;
        line.sharedMaterial = CreateMaterial(name + " Material", color);

        for (int segment = 0; segment < line.positionCount; segment++)
        {
            float angle = (segment / (float)line.positionCount) * Mathf.PI * 2f;
            float oval = 1f + Mathf.Cos(angle - 0.55f) * 0.16f + Mathf.Sin(angle * 2.0f + 0.4f) * 0.07f;
            float roughness = Mathf.Sin(segment * 0.53f + y) * 0.65f + Mathf.Sin(segment * 1.7f) * 0.18f;
            float r = radius + roughness;

            line.SetPosition(segment, new Vector3(
                Mathf.Cos(angle) * r * oval,
                y + Mathf.Sin(segment * 0.41f) * 0.12f,
                Mathf.Sin(angle) * r / oval
            ));
        }
    }

    private void CreateRockScatter()
    {
        for (int i = 0; i < 34; i++)
        {
            float angle = i * 2.39996f;
            float radius = 37f + Mathf.Repeat(i * 11.7f, 40f);
            Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 19.3f + Mathf.Sin(i) * 1.8f, Mathf.Sin(angle) * radius);

            GameObject rock = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Capsule : PrimitiveType.Cube);
            rock.name = "Broken Limestone Block";
            rock.transform.SetParent(transform, false);
            rock.transform.position = position;
            rock.transform.rotation = Quaternion.Euler(i * 19f, i * 47f, i * 31f);
            rock.transform.localScale = new Vector3(
                1.8f + Mathf.Repeat(i * 0.73f, 3f),
                0.7f + Mathf.Repeat(i * 0.41f, 1.4f),
                1.3f + Mathf.Repeat(i * 0.59f, 2.8f)
            );

            Renderer renderer = rock.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial("Scattered Limestone", new Color(0.16f, 0.15f, 0.12f, 1f));
        }
    }

    private DivingGearPickup CreateDiveGearStation(Transform player)
    {
        GameObject gearRoot = new GameObject("DivingGear");
        gearRoot.transform.SetParent(transform, false);
        gearRoot.transform.position = new Vector3(-5.2f, 20.2f, 35.5f);
        gearRoot.transform.rotation = Quaternion.Euler(0f, 18f, 0f);

        var model = Resources.Load<GameObject>("Equipment/Level1DivingGear");
        if (model == null) throw new System.InvalidOperationException("Missing shared diving gear prefab");
        var visual = Instantiate(model, gearRoot.transform, false);
        visual.name = "Level1 Diving Gear Model";
        visual.transform.localScale = Vector3.one * .5f;
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.position += Vector3.up * (gearRoot.transform.position.y - bounds.min.y);
        }

        DivingGearPickup pickup = gearRoot.AddComponent<DivingGearPickup>();
        pickup.player = player;
        pickup.interactDistance = 4.5f;
        pickup.hintText = "Press E to equip the recovery diving rig";
        pickup.textHeight = 2.6f;
        pickup.textSize = 4f;
        return pickup;
    }

    private void CreateWaterEntryGuard(Transform player)
    {
        GameObject guardObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        guardObject.name = "Water Entry Guard";
        guardObject.transform.SetParent(transform, false);
        guardObject.transform.position = new Vector3(0f, 8.5f, 0f);
        guardObject.transform.localScale = new Vector3(28f, 9f, 28f);

        Renderer renderer = guardObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.enabled = false;

        Collider collider = guardObject.GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = true;

        WaterEntryGuard guard = guardObject.AddComponent<WaterEntryGuard>();
        guard.player = player;
        guard.gearState = player.GetComponent<PlayerGearState>();
        guard.playerMove = player.GetComponent<SimpleMove>();
        guard.warningMessage = "Wear the diving gear first.";
        guard.warningHeight = 13f;
        guard.warningSize = 4f;
        guard.warningDistance = 34f;
    }

    private void CreateGearCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color, Quaternion? localRotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation ?? Quaternion.identity;
        part.transform.localScale = localScale;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateMaterial(name + " Material", color);
    }

    private void CreateGearBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateMaterial(name + " Material", color);
    }

    private void CreateCliffDepthMarkers()
    {
        for (int i = 1; i <= 4; i++)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = $"{i * 75}m Cliff Side Marker";
            marker.transform.SetParent(transform, false);
            marker.transform.position = new Vector3(28.5f, -i * 75f, 0f);
            marker.transform.localScale = new Vector3(0.35f, 0.35f, 7f);

            Renderer renderer = marker.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial("Depth Marker", new Color(0.72f, 0.78f, 0.78f, 1f));
        }
    }

    private void CreateCavePointCloud()
    {
        TextAsset source = Resources.Load<TextAsset>("Level3/may16.drop");
        Mesh pointCloud = ZacatonPointCloud.CreateMesh(source, 0.45f, 8.9f);

        if (pointCloud == null)
        {
            Debug.LogWarning("Could not load Zacaton point cloud from Resources/Level3/may16.drop.bytes.");
            return;
        }

        GameObject pointObject = new GameObject("Zacaton Real Point Cloud");
        pointObject.transform.SetParent(transform, false);

        MeshFilter filter = pointObject.AddComponent<MeshFilter>();
        filter.sharedMesh = pointCloud;

        MeshRenderer renderer = pointObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateMaterial("Zacaton Point Glow", new Color(0.1f, 0.85f, 1f, 1f));
    }

    private void CreateDiveShaftCollider()
    {
        GameObject shaft = new GameObject("Measured Zacaton Cave Wall");
        shaft.transform.SetParent(transform, false);

        TextAsset source = Resources.Load<TextAsset>("Level3/may16.drop");
        Mesh mesh = CreateMeasuredCaveWallMesh(source, 96, 96, 318f);

        if (mesh == null)
            mesh = CreateShaftMesh(96, 64, 318f);

        MeshFilter filter = shaft.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = shaft.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateMaterial("Wet Measured Limestone Wall", new Color(0.055f, 0.07f, 0.06f, 1f), true);

        MeshCollider collider = shaft.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    private Mesh CreateMeasuredCaveWallMesh(TextAsset source, int segments, int rings, float depth)
    {
        Vector3[] points = ZacatonPointCloud.CreateVertices(source, 0.45f, 8.9f);
        if (points == null || points.Length == 0)
            return null;

        float[,] radii = new float[rings + 1, segments];
        int[,] counts = new int[rings + 1, segments];

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 point = points[i];
            if (point.y > 1f || point.y < -depth)
                continue;

            float normalizedDepth = Mathf.InverseLerp(0f, -depth, point.y);
            int ring = Mathf.Clamp(Mathf.RoundToInt(normalizedDepth * rings), 0, rings);
            float angle = Mathf.Atan2(point.z, point.x);
            if (angle < 0f)
                angle += Mathf.PI * 2f;

            int segment = Mathf.Clamp(Mathf.FloorToInt(angle / (Mathf.PI * 2f) * segments), 0, segments - 1);
            float radius = new Vector2(point.x, point.z).magnitude;

            if (radius > radii[ring, segment])
                radii[ring, segment] = radius;

            counts[ring, segment]++;
        }

        for (int ring = 0; ring <= rings; ring++)
        {
            float t = ring / (float)rings;
            float fallbackRadius = Mathf.Lerp(27f, 9f, Mathf.Pow(t, 0.85f));

            for (int segment = 0; segment < segments; segment++)
            {
                if (counts[ring, segment] == 0 || radii[ring, segment] < 3f)
                    radii[ring, segment] = fallbackRadius;

                radii[ring, segment] = Mathf.Clamp(radii[ring, segment], 5.5f, 38f);
            }
        }

        for (int pass = 0; pass < 4; pass++)
        {
            float[,] smoothed = new float[rings + 1, segments];

            for (int ring = 0; ring <= rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int prevSegment = (segment - 1 + segments) % segments;
                    int nextSegment = (segment + 1) % segments;
                    int prevRing = Mathf.Max(0, ring - 1);
                    int nextRing = Mathf.Min(rings, ring + 1);

                    smoothed[ring, segment] =
                        radii[ring, segment] * 0.42f +
                        radii[ring, prevSegment] * 0.16f +
                        radii[ring, nextSegment] * 0.16f +
                        radii[prevRing, segment] * 0.13f +
                        radii[nextRing, segment] * 0.13f;
                }
            }

            radii = smoothed;
        }

        Vector3[] vertices = new Vector3[(rings + 1) * segments];
        int[] triangles = new int[rings * segments * 6];

        for (int ring = 0; ring <= rings; ring++)
        {
            float t = ring / (float)rings;
            float y = -depth * t;

            for (int segment = 0; segment < segments; segment++)
            {
                float angle = (segment / (float)segments) * Mathf.PI * 2f;
                float strata = Mathf.Sin(t * Mathf.PI * 34f) * 0.28f;
                float fracture = Mathf.Sin(segment * 5.1f + ring * 0.37f) * 0.18f;
                float radius = radii[ring, segment] + strata + fracture;

                vertices[ring * segments + segment] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    y,
                    Mathf.Sin(angle) * radius
                );
            }
        }

        int index = 0;
        for (int ring = 0; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int current = ring * segments + segment;
                int next = ring * segments + (segment + 1) % segments;
                int below = (ring + 1) * segments + segment;
                int belowNext = (ring + 1) * segments + (segment + 1) % segments;

                triangles[index++] = current;
                triangles[index++] = below;
                triangles[index++] = next;
                triangles[index++] = next;
                triangles[index++] = below;
                triangles[index++] = belowNext;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "Measured Zacaton Cave Wall"
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void CreateUnderwaterAtmosphere()
    {
        CreateSuspendedSilt("Upper Suspended Silt", new Vector3(0f, -45f, 0f), 48f, 70f, 450);
        CreateSuspendedSilt("Mid Depth Suspended Silt", new Vector3(0f, -155f, 0f), 34f, 110f, 620);
        CreateSuspendedSilt("Deep Suspended Silt", new Vector3(0f, -265f, 0f), 22f, 80f, 520);
        CreateDepthLight("Faint Surface Shaft Light", new Vector3(-8f, -38f, 9f), 1.8f, 95f, new Color(0.2f, 0.65f, 0.78f));
        CreateDepthLight("Deep Blue Falloff", new Vector3(7f, -190f, -6f), 0.9f, 70f, new Color(0.04f, 0.28f, 0.36f));
    }

    private void CreateSuspendedSilt(string name, Vector3 position, float radius, float height, int maxParticles)
    {
        GameObject siltObject = new GameObject(name);
        siltObject.transform.SetParent(transform, false);
        siltObject.transform.position = position;

        ParticleSystem particles = siltObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.startLifetime = 18f;
        main.startSpeed = 0.08f;
        main.startSize = 0.11f;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new Color(0.55f, 0.72f, 0.72f, 0.28f);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = maxParticles / 12f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(radius * 2f, height, radius * 2f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(0.015f, 0.06f);
        velocity.x = new ParticleSystem.MinMaxCurve(-0.035f, 0.035f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.035f, 0.035f);
    }

    private void CreateDepthLight(string name, Vector3 position, float intensity, float range, Color color)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.position = position;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
    }

    private Mesh CreateShaftMesh(int segments, int rings, float depth)
    {
        Vector3[] vertices = new Vector3[(rings + 1) * segments];
        int[] triangles = new int[rings * segments * 6];

        for (int ring = 0; ring <= rings; ring++)
        {
            float t = ring / (float)rings;
            float y = -depth * t;
            float baseRadius = Mathf.Lerp(25f, 10f, Mathf.Pow(t, 0.75f));
            float chamber = Mathf.Exp(-Mathf.Pow((t - 0.25f) * 5.2f, 2f)) * 10f;
            float pinch = Mathf.Exp(-Mathf.Pow((t - 0.58f) * 7.5f, 2f)) * -5.5f;
            float radius = baseRadius + chamber + pinch
                + Mathf.Sin(t * Mathf.PI * 4.2f) * 2.6f
                + Mathf.Sin(t * Mathf.PI * 13.5f) * 0.85f;

            for (int segment = 0; segment < segments; segment++)
            {
                float angle = (segment / (float)segments) * Mathf.PI * 2f;
                float oval = 1f + Mathf.Sin(angle + t * 3f) * 0.18f + Mathf.Cos(angle * 2f - t) * 0.08f;
                float roughness = Mathf.Sin(segment * 1.1f + ring * 0.65f) * 1.25f
                    + Mathf.Sin(segment * 3.7f - ring * 0.28f) * 0.45f;
                float r = radius + roughness;
                vertices[ring * segments + segment] = new Vector3(Mathf.Cos(angle) * r * oval, y, Mathf.Sin(angle) * r / oval);
            }
        }

        int index = 0;
        for (int ring = 0; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int current = ring * segments + segment;
                int next = ring * segments + (segment + 1) % segments;
                int below = (ring + 1) * segments + segment;
                int belowNext = (ring + 1) * segments + (segment + 1) % segments;

                triangles[index++] = current;
                triangles[index++] = below;
                triangles[index++] = next;
                triangles[index++] = next;
                triangles[index++] = below;
                triangles[index++] = belowNext;
            }
        }

        Mesh mesh = new Mesh
        {
            name = "Playable Zacaton Shaft"
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private Transform CreateRecoveryBody(Transform player, Level3MissionController mission)
    {
        GameObject bodyRoot = new GameObject("Recovery Body");
        bodyRoot.transform.SetParent(transform, false);
        Vector3 caveAnchor = Application.isPlaying ? RuntimeCaveAnchor : EditorCaveAnchor;
        bodyRoot.transform.position = caveAnchor + new Vector3(4f, -285f, -3f);
        bodyRoot.transform.rotation = Quaternion.Euler(0f, 25f, 90f);

        CreateBodyPart(bodyRoot.transform, "Torso", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.65f, 1.25f, 0.65f), new Color(0.02f, 0.04f, 0.05f));
        CreateBodyPart(bodyRoot.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.35f, 0f), new Vector3(0.48f, 0.48f, 0.48f), new Color(0.02f, 0.04f, 0.05f));
        CreateBodyPart(bodyRoot.transform, "Left Arm", PrimitiveType.Capsule, new Vector3(-0.75f, 0.15f, 0f), new Vector3(0.22f, 0.8f, 0.22f), new Color(0.02f, 0.04f, 0.05f));
        CreateBodyPart(bodyRoot.transform, "Right Arm", PrimitiveType.Capsule, new Vector3(0.75f, 0.15f, 0f), new Vector3(0.22f, 0.8f, 0.22f), new Color(0.02f, 0.04f, 0.05f));

        SphereCollider trigger = bodyRoot.AddComponent<SphereCollider>();
        trigger.radius = 3.2f;
        trigger.isTrigger = true;

        BodyRecoveryTarget target = bodyRoot.AddComponent<BodyRecoveryTarget>();
        target.player = player;
        target.mission = mission;

        bodyRoot.AddComponent<Level3DiverModelLoader>();

        return bodyRoot.transform;
    }

    private void CreateBodyPart(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            DestroySceneObject(collider);

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateMaterial(name + " Material", color);
    }

    private Material CreateMaterial(string name, Color color, bool doubleSided = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            name = name,
            color = color
        };

        if (doubleSided && material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        if (ShouldUseRockTexture(name))
        {
            Texture2D texture = CreateNoiseTexture(color, 64);

            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            else if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
        }

        return material;
    }

    private bool ShouldUseRockTexture(string materialName)
    {
        return materialName.Contains("Limestone")
            || materialName.Contains("Rock")
            || materialName.Contains("Wall")
            || materialName.Contains("Block")
            || materialName.Contains("Drop");
    }

    private Texture2D CreateNoiseTexture(Color baseColor, int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            name = "Procedural Wet Limestone Texture",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float largeNoise = Mathf.PerlinNoise(x * 0.065f, y * 0.065f);
                float fineNoise = Mathf.PerlinNoise(x * 0.31f + 19.7f, y * 0.31f + 7.3f);
                float strata = Mathf.Sin(y * 0.42f + largeNoise * 2.5f) * 0.5f + 0.5f;
                float value = 0.72f + largeNoise * 0.28f + fineNoise * 0.12f + strata * 0.11f;

                Color wetTint = Color.Lerp(baseColor, new Color(0.03f, 0.055f, 0.05f, baseColor.a), 0.35f);
                Color dryTint = Color.Lerp(baseColor, new Color(0.34f, 0.31f, 0.24f, baseColor.a), 0.25f);
                pixels[y * size + x] = Color.Lerp(wetTint, dryTint, value);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    private static void DestroySceneObject(Object target)
    {
        if (target == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(target);
            return;
        }
#endif

        Destroy(target);
    }
}

public sealed class Level3SharedMapMarker : MonoBehaviour
{
    public int Version;
}

