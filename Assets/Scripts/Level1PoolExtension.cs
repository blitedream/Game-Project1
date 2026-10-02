using UnityEngine;
using UnityEngine.SceneManagement;

[ExecuteAlways]
public sealed class Level1PoolExtension : MonoBehaviour
{
    public const int CurrentMapVersion = 14;
    public const float WaterSurfaceY = -0.5f;
    public const float BottomSurfaceY = -60.5f;
    public const float PoolWidth = 12f;
    public const float PoolLength = 14f;
    private const float RoomMinX = -19.5f;
    private const float RoomMaxX = 19.5f;
    private const float RoomMinZ = -14.5f;
    private const float RoomMaxZ = 14.5f;
    private const float RoomHeight = 10f;
    public static readonly Vector3 PoolCenter = new Vector3(2.43f, WaterSurfaceY, -7.13f);
    public static readonly Vector3 BottomTarget = new Vector3(2.43f, -59.35f, -7.13f);
    public static readonly Vector3 PlayerDeckSpawn = new Vector3(-12f, 1.1f, 2f);
    public static readonly Vector3 InstructorDeckPosition = new Vector3(12f, 0f, 1.5f);

    [SerializeField] private int mapVersion;
    public int MapVersion => mapVersion;

    private void OnEnable()
    {
        if (gameObject.scene.IsValid() && gameObject.scene.name == "Level1" &&
            (mapVersion != CurrentMapVersion || transform.childCount < 8))
            Build();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnBootstrapSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnBootstrapSceneLoaded;
    }

    private static void OnBootstrapSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Bootstrap();
    }

    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != "Level1" || GameObject.Find("Extended Training Pool") != null)
            return;

        GameObject root = new GameObject("Extended Training Pool");
        root.AddComponent<Level1PoolExtension>();
    }

    public void Build()
    {
        ClearGeneratedChildren();
        mapVersion = CurrentMapVersion;

        Material wallMaterial = CreateMaterial(
            "Training Pool Deep Wall",
            new Color(0.055f, 0.18f, 0.22f),
            0.38f);
        Material bottomMaterial = CreateMaterial(
            "Training Pool Bottom",
            new Color(0.035f, 0.11f, 0.14f),
            0.24f);
        Material trimMaterial = CreateMaterial(
            "Training Pool Flush Trim",
            new Color(0.12f, 0.24f, 0.28f),
            0.58f);

        // Original WaterVisual footprint. All replacement geometry uses this
        // already-calibrated opening rather than inferring a centre from floor gaps.
        const float width = PoolWidth;
        const float length = PoolLength;
        const float wallThickness = 0.35f;
        float shaftHeight = WaterSurfaceY - BottomSurfaceY;
        float shaftCenterY = (WaterSurfaceY + BottomSurfaceY) * 0.5f;

        Material deckMaterial = GetOriginalDeckMaterial();
        AlignIndoorRoom();
        DisableOriginalDeckPieces();
        CreateAlignedDeck(width, length, deckMaterial);

        CreateBlock("Deep Pool Bottom", new Vector3(PoolCenter.x, BottomSurfaceY - 0.5f, PoolCenter.z),
            new Vector3(width, 1f, length), bottomMaterial);
        CreateBlock("Deep Wall Left", new Vector3(PoolCenter.x - width * 0.5f, shaftCenterY, PoolCenter.z),
            new Vector3(wallThickness, shaftHeight, length), wallMaterial);
        CreateBlock("Deep Wall Right", new Vector3(PoolCenter.x + width * 0.5f, shaftCenterY, PoolCenter.z),
            new Vector3(wallThickness, shaftHeight, length), wallMaterial);
        CreateBlock("Deep Wall Front", new Vector3(PoolCenter.x, shaftCenterY, PoolCenter.z - length * 0.5f),
            new Vector3(width, shaftHeight, wallThickness), wallMaterial);
        CreateBlock("Deep Wall Back", new Vector3(PoolCenter.x, shaftCenterY, PoolCenter.z + length * 0.5f),
            new Vector3(width, shaftHeight, wallThickness), wallMaterial);

        // Invisible overlapping supports bridge the seams between the four deck
        // slabs and the vertical shaft without creating raised, misaligned steps.
        const float rimWidth = 0.55f;
        const float supportHeight = 0.5f;
        CreateCollisionBlock("Pool Edge Support Left", new Vector3(PoolCenter.x - width * 0.5f, -supportHeight * 0.5f, PoolCenter.z),
            new Vector3(rimWidth, supportHeight, length + rimWidth));
        CreateCollisionBlock("Pool Edge Support Right", new Vector3(PoolCenter.x + width * 0.5f, -supportHeight * 0.5f, PoolCenter.z),
            new Vector3(rimWidth, supportHeight, length + rimWidth));
        CreateCollisionBlock("Pool Edge Support Front", new Vector3(PoolCenter.x, -supportHeight * 0.5f, PoolCenter.z - length * 0.5f),
            new Vector3(width + rimWidth, supportHeight, rimWidth));
        CreateCollisionBlock("Pool Edge Support Back", new Vector3(PoolCenter.x, -supportHeight * 0.5f, PoolCenter.z + length * 0.5f),
            new Vector3(width + rimWidth, supportHeight, rimWidth));

        // A four-centimetre flush trim gives the opening one coherent outline.
        const float trimWidth = 0.34f;
        const float trimHeight = 0.04f;
        CreateVisualBlock("Pool Trim Left", new Vector3(PoolCenter.x - width * 0.5f, trimHeight * 0.5f, PoolCenter.z),
            new Vector3(trimWidth, trimHeight, length), trimMaterial);
        CreateVisualBlock("Pool Trim Right", new Vector3(PoolCenter.x + width * 0.5f, trimHeight * 0.5f, PoolCenter.z),
            new Vector3(trimWidth, trimHeight, length), trimMaterial);
        CreateVisualBlock("Pool Trim Front", new Vector3(PoolCenter.x, trimHeight * 0.5f, PoolCenter.z - length * 0.5f),
            new Vector3(width, trimHeight, trimWidth), trimMaterial);
        CreateVisualBlock("Pool Trim Back", new Vector3(PoolCenter.x, trimHeight * 0.5f, PoolCenter.z + length * 0.5f),
            new Vector3(width, trimHeight, trimWidth), trimMaterial);

        CreateDepthMarker(20f, new Color(0.1f, 0.9f, 1f), true);
        CreateDepthMarker(40f, new Color(0.12f, 1f, 0.45f), true);
        CreateDepthMarker(60f, new Color(1f, 0.55f, 0.08f), true);

        AlignWaterSurface(width, length);
        DisableOriginalShallowPoolCollider();
        SaveEditorRebuild();
    }

    private void SaveEditorRebuild()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || !gameObject.scene.IsValid())
            return;

        Scene rebuiltScene = gameObject.scene;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rebuiltScene);
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode &&
                rebuiltScene.IsValid() && rebuiltScene.isLoaded && rebuiltScene.isDirty)
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(rebuiltScene);
        };
#endif
    }

    private static void AlignWaterSurface(float width, float length)
    {
        GameObject water = GameObject.Find("WaterVisual");
        if (water == null)
            return;

        water.transform.position = PoolCenter;
        water.transform.localScale = new Vector3(width / 10f, 1f, length / 10f);
        var renderer=water.GetComponent<Renderer>();
        if(renderer!=null)
        {
            renderer.sharedMaterial=ClearWaterMaterial.Create(true);
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private void CreateDepthMarker(float depth, Color color, bool createObjectiveRing)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = $"Glowing Depth Cube {depth:0}m";
        marker.transform.SetParent(transform, true);
        marker.transform.position = new Vector3(PoolCenter.x, WaterSurfaceY - depth + 0.75f, PoolCenter.z);
        marker.transform.localScale = Vector3.one * 0.85f;
        Collider collider = marker.GetComponent<Collider>();
        if (collider != null) DestroyGeneratedObject(collider);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = $"Depth {depth:0}m Glow", color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color * 1.6f);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 4f);
        }
        marker.GetComponent<Renderer>().sharedMaterial = material;
        marker.AddComponent<RotatingDepthMarker>().speed = 36f + depth * 0.2f;
        DepthTrainingMarker depthMarker = marker.AddComponent<DepthTrainingMarker>();
        depthMarker.Depth = depth;

        if (createObjectiveRing)
            depthMarker.ObjectiveRing = CreateObjectiveRing(marker.transform, material);

        GameObject lightObject = new GameObject("Depth Marker Light");
        lightObject.transform.SetParent(marker.transform, false);
        Light markerLight = lightObject.AddComponent<Light>();
        markerLight.type = LightType.Point;
        markerLight.color = color;
        markerLight.range = 7f;
        markerLight.intensity = 2.4f;
    }

    private static GameObject CreateObjectiveRing(Transform parent, Material material)
    {
        GameObject ringObject = new GameObject("Objective Hold Ring");
        ringObject.transform.SetParent(parent, false);
        ringObject.transform.localPosition = Vector3.zero;
        // Cancel the cube's scale so that the ring keeps a predictable world size.
        ringObject.transform.localScale = Vector3.one / parent.localScale.x;

        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.loop = true;
        ring.useWorldSpace = false;
        ring.positionCount = 64;
        ring.startWidth = 0.075f;
        ring.endWidth = 0.075f;
        ring.sharedMaterial = material;

        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i / (float)ring.positionCount * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * 1.55f, 0f, Mathf.Sin(angle) * 1.55f));
        }

        return ringObject;
    }

    private void CreateBlock(string objectName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.layer = 3; // The Level 1 player's configured ground/collision layer.
        block.transform.SetParent(transform, true);
        block.transform.position = position;
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void CreateCollisionBlock(string objectName, Vector3 position, Vector3 scale)
    {
        GameObject support = new GameObject(objectName);
        support.layer = 3;
        support.transform.SetParent(transform, true);
        support.transform.position = position;
        support.transform.localScale = scale;
        support.AddComponent<BoxCollider>();
    }

    private void CreateAlignedDeck(float poolWidth, float poolLength, Material deckMaterial)
    {
        const float deckY = -0.5f;
        const float deckHeight = 1f;

        float poolMinX = PoolCenter.x - poolWidth * 0.5f;
        float poolMaxX = PoolCenter.x + poolWidth * 0.5f;
        float poolMinZ = PoolCenter.z - poolLength * 0.5f;
        float poolMaxZ = PoolCenter.z + poolLength * 0.5f;

        CreateBlock("Aligned Deck Left",
            new Vector3((RoomMinX + poolMinX) * 0.5f, deckY, (RoomMinZ + RoomMaxZ) * 0.5f),
            new Vector3(poolMinX - RoomMinX, deckHeight, RoomMaxZ - RoomMinZ), deckMaterial);
        CreateBlock("Aligned Deck Right",
            new Vector3((poolMaxX + RoomMaxX) * 0.5f, deckY, (RoomMinZ + RoomMaxZ) * 0.5f),
            new Vector3(RoomMaxX - poolMaxX, deckHeight, RoomMaxZ - RoomMinZ), deckMaterial);
        CreateBlock("Aligned Deck Front",
            new Vector3(PoolCenter.x, deckY, (RoomMinZ + poolMinZ) * 0.5f),
            new Vector3(poolWidth, deckHeight, poolMinZ - RoomMinZ), deckMaterial);
        CreateBlock("Aligned Deck Back",
            new Vector3(PoolCenter.x, deckY, (poolMaxZ + RoomMaxZ) * 0.5f),
            new Vector3(poolWidth, deckHeight, RoomMaxZ - poolMaxZ), deckMaterial);
    }

    private static void AlignIndoorRoom()
    {
        GameObject room = GameObject.Find("IndoorRoom");
        if (room == null)
            return;

        float roomWidth = RoomMaxX - RoomMinX;
        float roomLength = RoomMaxZ - RoomMinZ;
        float centerX = (RoomMinX + RoomMaxX) * 0.5f;
        float centerZ = (RoomMinZ + RoomMaxZ) * 0.5f;
        float wallCenterY = RoomHeight * 0.5f;

        // Reuse the original room blocks and place them in world space so the
        // translated IndoorRoom root cannot leave gaps around the rebuilt deck.
        AlignRoomBlock(room.transform, "BackWall",
            new Vector3(centerX, wallCenterY, RoomMaxZ),
            new Vector3(roomWidth + 1f, RoomHeight, 1f));
        AlignRoomBlock(room.transform, "FrontWall",
            new Vector3(centerX, wallCenterY, RoomMinZ),
            new Vector3(roomWidth + 1f, RoomHeight, 1f));
        AlignRoomBlock(room.transform, "LeftWall",
            new Vector3(RoomMinX, wallCenterY, centerZ),
            new Vector3(1f, RoomHeight, roomLength + 1f));
        AlignRoomBlock(room.transform, "RightWall",
            new Vector3(RoomMaxX, wallCenterY, centerZ),
            new Vector3(1f, RoomHeight, roomLength + 1f));
        AlignRoomBlock(room.transform, "Ceiling",
            new Vector3(centerX, RoomHeight, centerZ),
            new Vector3(roomWidth + 1f, 1f, roomLength + 1f));
    }

    private static void AlignRoomBlock(Transform room, string childName, Vector3 worldPosition, Vector3 scale)
    {
        Transform block = room.Find(childName);
        if (block == null)
            return;

        block.position = worldPosition;
        block.rotation = Quaternion.identity;
        block.localScale = scale;
        block.gameObject.SetActive(true);
    }

    private static Material GetOriginalDeckMaterial()
    {
        string[] names = { "Floor_Left", "Floor_Right", "Floor_Front", "Floor_Back" };
        foreach (string objectName in names)
        {
            GameObject floor = GameObject.Find(objectName);
            Renderer renderer = floor != null ? floor.GetComponent<Renderer>() : null;
            if (renderer != null && renderer.sharedMaterial != null)
                return renderer.sharedMaterial;
        }

        return CreateMaterial("Training Deck", new Color(0.38f, 0.46f, 0.52f), 0.34f);
    }

    private static void DisableOriginalDeckPieces()
    {
        string[] names = { "Floor_Left", "Floor_Right", "Floor_Front", "Floor_Back" };
        foreach (string objectName in names)
        {
            GameObject floor = GameObject.Find(objectName);
            if (floor == null)
                continue;

            Renderer renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = false;
            foreach (Collider collider in floor.GetComponents<Collider>())
                collider.enabled = false;
        }
    }

    private void CreateVisualBlock(string objectName, Vector3 position, Vector3 scale, Material material)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = objectName;
        visual.transform.SetParent(transform, true);
        visual.transform.position = position;
        visual.transform.localScale = scale;
        visual.GetComponent<Renderer>().sharedMaterial = material;
        DestroyGeneratedObject(visual.GetComponent<Collider>());
    }

    private static Material CreateMaterial(string materialName, Color color, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = materialName, color = color };
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.05f);
        return material;
    }

    private void ClearGeneratedChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyGeneratedObject(transform.GetChild(i).gameObject);
    }

    private static void DisableOriginalShallowPoolCollider()
    {
        GameObject originalPool = GameObject.Find("pool");
        if (originalPool == null)
            return;

        foreach (MeshCollider meshCollider in originalPool.GetComponentsInChildren<MeshCollider>(true))
            meshCollider.enabled = false;

        // The imported FBX contains the old shallow basin at another local
        // origin. Leaving any part visible produces a second apparent pool.
        originalPool.SetActive(false);
    }

    private static void DestroyGeneratedObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
