using System;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public sealed class Level1CoachLoader : MonoBehaviour
{
    private const string CoachResourcePath = "Characters/Coach/Casual_Hoodie";
    public bool UseOverridePosition;
    public Vector3 OverridePosition;

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
        if (SceneManager.GetActiveScene().name != "Level1" || FindFirstObjectByType<Level1CoachDialogue>() != null)
            return;

        GameObject coach = new GameObject("Diving Coach");
        coach.AddComponent<Level1CoachLoader>();
        coach.AddComponent<Level1CoachDialogue>();
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        Vector3 playerPosition = playerObject != null ? playerObject.transform.position : Vector3.zero;
        // A full deck slab on the opposite side of the pool, away from every seam.
        Vector3 desiredPosition = UseOverridePosition
            ? OverridePosition
            : Level1PoolExtension.InstructorDeckPosition;

        if (!UseOverridePosition && Physics.Raycast(desiredPosition + Vector3.up * 8f, Vector3.down, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore))
            desiredPosition.y = hit.point.y;
        transform.position = desiredPosition;

        Vector3 lookTarget = new Vector3(playerPosition.x, transform.position.y, playerPosition.z);
        Vector3 direction = lookTarget - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(direction);

        GameObject coachPrefab = Resources.Load<GameObject>(CoachResourcePath);
        if (coachPrefab == null)
        {
            Debug.LogError($"The diving coach resource '{CoachResourcePath}' is missing.");
            return;
        }

        GameObject visual = Instantiate(coachPrefab, transform);
        visual.name = "Coach Visual";
        visual.transform.localPosition = Vector3.zero;

        try
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float scale = 1.82f / Mathf.Max(0.01f, bounds.size.y);
            visual.transform.localScale = Vector3.one * scale;
            ApplyBlueCoachTracksuit(renderers);

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            CoachStandingAnimation standingAnimation = visual.AddComponent<CoachStandingAnimation>();
            standingAnimation.ResourcePath = CoachResourcePath;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            visual.transform.position += new Vector3(
                desiredPosition.x - bounds.center.x,
                desiredPosition.y - bounds.min.y,
                desiredPosition.z - bounds.center.z);

            CreateCoachIdentityKit();

            CapsuleCollider body = gameObject.AddComponent<CapsuleCollider>();
            body.height = 1.82f;
            body.radius = 0.35f;
            body.center = new Vector3(desiredPosition.x, desiredPosition.y + 0.91f, desiredPosition.z) - transform.position;

            CreateLabel(desiredPosition + Vector3.up * 2.15f);
            Debug.Log($"Diving coach placed on the opposite side of the Level 1 pool at {desiredPosition}.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void PoseArmsDown(GameObject visual, Bounds bounds)
    {
        float shoulderY = bounds.min.y + bounds.size.y * 0.72f;
        float armThreshold = bounds.size.y * 0.19f;

        foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh source = filter.sharedMesh;
            if (source == null || !source.isReadable) continue;

            Mesh mesh = Instantiate(source);
            mesh.name = source.name + " - Coach Standing Pose";
            Vector3[] vertices = mesh.vertices;
            bool changed = false;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = filter.transform.TransformPoint(vertices[i]);
                float horizontal = world.x - bounds.center.x;
                if (world.y < shoulderY - bounds.size.y * 0.10f || Mathf.Abs(horizontal) < armThreshold)
                    continue;

                float side = Mathf.Sign(horizontal);
                Vector3 shoulder = new Vector3(
                    bounds.center.x + side * armThreshold,
                    shoulderY,
                    world.z);
                // Rigid 90-degree rotation preserves the complete arm cross-section.
                // Unlike the previous coordinate compression, this does not flatten
                // the arm when viewed from the front.
                Quaternion armRotation = Quaternion.AngleAxis(side > 0f ? -90f : 90f, Vector3.forward);
                world = shoulder + armRotation * (world - shoulder);
                vertices[i] = filter.transform.InverseTransformPoint(world);
                changed = true;
            }

            if (!changed) continue;
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
        }
    }

    private static void ApplyBlueCoachTracksuit(Renderer[] renderers)
    {
        Color suitBlue = new Color(0.025f, 0.16f, 0.46f, 1f);
        Color accentBlue = new Color(0.05f, 0.42f, 0.82f, 1f);
        Color stripeWhite = new Color(0.82f, 0.90f, 0.96f, 1f);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;

                string materialName = material.name.ToLowerInvariant();
                if (materialName.Contains("skin") || materialName.Contains("hair") ||
                    materialName.Contains("eye") || materialName.Contains("eyebrow"))
                    continue;

                if (materialName.Contains("lightblue"))
                {
                    SetMaterialColor(material, accentBlue);
                    continue;
                }

                if (materialName.Contains("white"))
                {
                    SetMaterialColor(material, stripeWhite);
                    continue;
                }

                if (materialName.Contains("purple"))
                {
                    SetMaterialColor(material, suitBlue);
                    continue;
                }

                if ((materialName.Contains("diver_body") || materialName.Contains("diver_objects")) && material.mainTexture is Texture2D sourceTexture)
                {
                    Texture2D blueTexture = RecolorDarkFabric(sourceTexture, suitBlue);
                    material.mainTexture = blueTexture;
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", blueTexture);
                    continue;
                }

                if (!material.HasProperty("_BaseColor") && !material.HasProperty("_Color"))
                    continue;

                Color original = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material.color;
                float saturation = Mathf.Max(original.r, Mathf.Max(original.g, original.b)) -
                                   Mathf.Min(original.r, Mathf.Min(original.g, original.b));
                float brightness = Mathf.Max(original.r, Mathf.Max(original.g, original.b));
                if (brightness > 0.62f || saturation > 0.34f)
                    continue; // Preserve skin, yellow equipment and light accessories.

                Color blue = Color.Lerp(original, suitBlue, 0.78f);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", blue);
                if (material.HasProperty("_Color")) material.SetColor("_Color", blue);
            }
            renderer.materials = materials;
        }
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private static Texture2D RecolorDarkFabric(Texture2D source, Color blue)
    {
        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, temporary);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = temporary;
        Texture2D result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true)
        {
            name = source.name + " - Blue Coach Suit"
        };
        result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        result.Apply(false, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);

        Color[] pixels = result.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color color = pixels[i];
            float maximum = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            float minimum = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            float saturation = maximum - minimum;
            if (maximum > 0.52f || saturation > 0.24f)
                continue;

            float detail = Mathf.InverseLerp(0.02f, 0.52f, maximum);
            Color replacement = Color.Lerp(blue * 0.38f, blue * 1.35f, detail);
            replacement.a = color.a;
            pixels[i] = Color.Lerp(color, replacement, 0.82f);
        }
        result.SetPixels(pixels);
        result.Apply(true, false);
        return result;
    }

    private void CreateCoachIdentityKit()
    {
        Transform equipment = new GameObject("Coach Equipment").transform;
        equipment.SetParent(transform, false);

        Material blue = CreateCoachMaterial("Coach Blue", new Color(0.02f, 0.20f, 0.58f));
        Material yellow = CreateCoachMaterial("Coach Whistle", new Color(1.00f, 0.72f, 0.08f));
        Material board = CreateCoachMaterial("Coach Clipboard", new Color(0.11f, 0.07f, 0.035f));
        Material paper = CreateCoachMaterial("Coach Clipboard Paper", new Color(0.78f, 0.88f, 0.92f));

        CreateCoachPrimitive(equipment, "Coach Cap", PrimitiveType.Sphere,
            new Vector3(0f, 1.73f, 0.01f), new Vector3(0.23f, 0.11f, 0.21f), Vector3.zero, blue);
        CreateCoachPrimitive(equipment, "Coach Cap Brim", PrimitiveType.Cube,
            new Vector3(0f, 1.68f, 0.19f), new Vector3(0.26f, 0.025f, 0.13f), Vector3.zero, blue);

        GameObject lanyardObject = new GameObject("Whistle Lanyard");
        lanyardObject.transform.SetParent(equipment, false);
        LineRenderer lanyard = lanyardObject.AddComponent<LineRenderer>();
        lanyard.useWorldSpace = false;
        lanyard.loop = true;
        lanyard.positionCount = 3;
        lanyard.SetPositions(new[]
        {
            new Vector3(-0.13f, 1.49f, 0.235f),
            new Vector3(0f, 1.28f, 0.255f),
            new Vector3(0.13f, 1.49f, 0.235f)
        });
        lanyard.startWidth = 0.012f;
        lanyard.endWidth = 0.012f;
        lanyard.material = yellow;

        CreateCoachPrimitive(equipment, "Whistle", PrimitiveType.Cube,
            new Vector3(0f, 1.26f, 0.27f), new Vector3(0.07f, 0.04f, 0.045f), Vector3.zero, yellow);
        CreateCoachPrimitive(equipment, "Clipboard", PrimitiveType.Cube,
            new Vector3(-0.37f, 1.02f, 0.12f), new Vector3(0.25f, 0.34f, 0.025f),
            new Vector3(4f, -8f, 7f), board);
        CreateCoachPrimitive(equipment, "Clipboard Paper", PrimitiveType.Cube,
            new Vector3(-0.365f, 1.025f, 0.139f), new Vector3(0.20f, 0.275f, 0.008f),
            new Vector3(4f, -8f, 7f), paper);
    }

    private static GameObject CreateCoachPrimitive(Transform parent, string objectName, PrimitiveType primitiveType,
        Vector3 localPosition, Vector3 localScale, Vector3 localEulerAngles, Material material)
    {
        GameObject primitive = GameObject.CreatePrimitive(primitiveType);
        primitive.name = objectName;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localScale = localScale;
        primitive.transform.localEulerAngles = localEulerAngles;
        Collider primitiveCollider = primitive.GetComponent<Collider>();
        if (primitiveCollider != null) UnityEngine.Object.Destroy(primitiveCollider);
        primitive.GetComponent<Renderer>().material = material;
        return primitive;
    }

    private static Material CreateCoachMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = materialName };
        SetMaterialColor(material, color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.25f);
        return material;
    }

    private void CreateLabel(Vector3 position)
    {
        GameObject labelObject = new GameObject("Coach Label");
        labelObject.transform.SetParent(transform, true);
        labelObject.transform.position = position;
        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = "DIVING COACH";
        label.fontSize = 1.1f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.35f, 0.95f, 1f);
        label.rectTransform.sizeDelta = new Vector2(4f, 0.8f);
        labelObject.AddComponent<FaceMainCamera>();
    }
}

public sealed class CoachStandingAnimation : MonoBehaviour
{
    public string ResourcePath;
    private PlayableGraph graph;

    private void Start()
    {
        AnimationClip[] clips = Resources.LoadAll<AnimationClip>(ResourcePath);
        AnimationClip standing = null;
        foreach (AnimationClip clip in clips)
        {
            if (clip.name.Equals("Idle_Neutral", StringComparison.OrdinalIgnoreCase) ||
                clip.name.IndexOf("Man_Standing", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                standing = clip;
                break;
            }
            if (standing == null && (clip.name.Equals("Idle", StringComparison.OrdinalIgnoreCase) ||
                                     clip.name.IndexOf("Man_Idle", StringComparison.OrdinalIgnoreCase) >= 0))
                standing = clip;
        }

        if (standing == null)
        {
            Debug.LogWarning("The coach standing animation was not found; the imported bind pose will be used.");
            return;
        }

        Animator animator = GetComponent<Animator>();
        if (animator == null)
            animator = gameObject.AddComponent<Animator>();

        graph = PlayableGraph.Create("Level 1 Coach Standing Pose");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Coach", animator);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, standing);
        playable.SetApplyFootIK(true);
        output.SetSourcePlayable(playable);
        graph.Play();
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
}

public sealed class Level1CoachDialogue : MonoBehaviour
{
    public bool Completed { get; private set; }
    public bool FinalCompleted { get; private set; }
    private bool finalDebriefAvailable;
    private readonly string[] initialLines =
    {
        "Coach: Welcome to basic dive training.",
        "Coach: Collect your equipment first. Never enter the water without checking it.",
        "Coach: The training pool has a changing eight-direction current. It can push you forward, back, sideways, upward or downward without warning.",
        "Coach: Use C to descend and Space to ascend. Counter the current, hold at 20 and 40 metres, then touch the 60-metre bottom.",
        "Coach: After bottom contact, return to the surface, get onto the deck and report back to me."
    };
    private readonly string[] finalLines =
    {
        "Coach: Good return. You held your depth while the current kept changing.",
        "Coach: Remember that open water will be less predictable than this pool. Never fight the water blindly; correct early and stay calm.",
        "Coach: Training complete. You are cleared for the next assignment."
    };
    private Transform player;
    private SimpleMove movement;
    private bool talking;
    private bool movementWasEnabled;
    private int lineIndex;

    private string[] ActiveLines => finalDebriefAvailable ? finalLines : initialLines;
    private CoachVoice voice;

    private void LateUpdate()
    {
        if (voice == null) voice = gameObject.AddComponent<CoachVoice>();
        bool audible = talking && isActiveAndEnabled && !UnderwaterPanoramaMode.IsActive;
        voice.SetLine(audible ? ActiveLines[Mathf.Clamp(lineIndex, 0, ActiveLines.Length - 1)] : null);
    }

    private void OnDisable()
    {
        if (voice != null) voice.SetLine(null);
    }


    public void BeginFinalDebrief()
    {
        if (FinalCompleted)
            return;

        finalDebriefAvailable = true;
        talking = false;
        lineIndex = 0;
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null) return;
        player = playerObject.transform;
        movement = playerObject.GetComponent<SimpleMove>();
    }

    private void Update()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        if (player == null || UnderwaterPanoramaMode.IsActive ||
            (!finalDebriefAvailable && Completed) || (finalDebriefAvailable && FinalCompleted)) return;
        bool near = Vector3.Distance(player.position, transform.position) <= 4.2f;

        if (!talking && near && Input.GetKeyDown(KeyCode.E))
        {
            talking = true;
            lineIndex = 0;
            movementWasEnabled = movement != null && movement.enabled;
            if (movement != null) movement.enabled = false;
        }
        else if (talking && Input.GetKeyDown(KeyCode.E))
        {
            lineIndex++;
            if (lineIndex >= ActiveLines.Length)
            {
                talking = false;
                if (finalDebriefAvailable)
                    FinalCompleted = true;
                else
                    Completed = true;
                if (movement != null) movement.enabled = movementWasEnabled;
            }
        }
    }

    private void OnGUI()
    {
        if (player == null || UnderwaterPanoramaMode.IsActive ||
            (!finalDebriefAvailable && Completed) || (finalDebriefAvailable && FinalCompleted)) return;
        Vector3 flatCoach = new Vector3(transform.position.x, player.position.y, transform.position.z);
        bool near = Vector3.Distance(player.position, flatCoach) <= 4.2f;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        style.normal.textColor = Color.white;

        if (!talking && near)
            GUI.Label(new Rect((Screen.width - 340f) * 0.5f, Screen.height - 86f, 340f, 28f),
                finalDebriefAvailable ? "E  REPORT BACK TO COACH" : "E  TALK TO COACH", style);
        if (!talking) return;

        float width = Mathf.Min(720f, Screen.width - 60f);
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height - 150f;
        GUI.Box(new Rect(x, y, width, 112f), GUIContent.none);
        string[] lines = ActiveLines;
        GUI.Label(new Rect(x + 22f, y + 18f, width - 44f, 54f), lines[Mathf.Clamp(lineIndex, 0, lines.Length - 1)], style);
        GUI.Label(new Rect(x + 22f, y + 78f, width - 44f, 24f), "E  CONTINUE", style);
    }
}

public sealed class FaceMainCamera : MonoBehaviour
{
    private void LateUpdate()
    {
        if (Camera.main == null) return;
        transform.LookAt(Camera.main.transform);
        transform.Rotate(0f, 180f, 0f);
    }
}
