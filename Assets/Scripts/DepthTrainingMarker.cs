using UnityEngine;

public sealed class DepthTrainingMarker : MonoBehaviour
{
    public float Depth;
    public GameObject ObjectiveRing;
    public bool Completed { get; private set; }
    public bool Touched { get; private set; }
    private SimpleMove player;
    private Renderer markerRenderer;
    private Vector3 previousPosition;
    private bool hasPreviousPosition;
    private bool visualsApplied;

    private Material completedMaterial;

    private void Start() { markerRenderer = GetComponent<Renderer>(); }

    public void Touch()
    {
        if (Touched) return;
        Touched = true;
        ApplyCompletedVisuals();
    }

    private void Update()
    {
        if (Touched || Completed) return;
        if (player == null) player = FindFirstObjectByType<SimpleMove>();
        if (player == null || !player.EnvironmentReady || markerRenderer == null) return;
        Vector3 position = player.transform.position;
        Bounds contact = markerRenderer.bounds;
        contact.Expand(new Vector3(player.bodyRadius * 2f, player.groundSnapOffset * 2f, player.bodyRadius * 2f));
        Vector3 travel = position - previousPosition;
        bool hit = contact.Contains(position);
        if (!hit && hasPreviousPosition && travel.sqrMagnitude > .000001f)
            hit = contact.IntersectRay(new Ray(previousPosition, travel.normalized), out float distance) && distance <= travel.magnitude;
        if (hit) Touch();
        previousPosition = position;
        hasPreviousPosition = true;
    }

    public void CompleteObjective()
    {
        if (Completed)
            return;

        Completed = true;
        ApplyCompletedVisuals();
        Debug.Log($"Depth marker {Depth:0} m completed: changed to grey and rotation stopped.");
    }

    private void LateUpdate()
    {
        if ((Completed || Touched) && !visualsApplied)
            ApplyCompletedVisuals();
    }

    private void ApplyCompletedVisuals()
    {
        RotatingDepthMarker rotating = GetComponent<RotatingDepthMarker>();
        if (rotating != null)
        {
            rotating.speed = 0f;
            rotating.enabled = false;
        }

        Color completedGray = new Color(0.24f, 0.27f, 0.29f, 1f);
        if (completedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            completedMaterial = new Material(shader)
            {
                name = $"Completed Depth {Depth:0}m Grey",
                color = completedGray,
                hideFlags = HideFlags.DontSave
            };
            if (completedMaterial.HasProperty("_BaseColor"))
                completedMaterial.SetColor("_BaseColor", completedGray);
            if (completedMaterial.HasProperty("_Color"))
                completedMaterial.SetColor("_Color", completedGray);
            if (completedMaterial.HasProperty("_EmissionColor"))
                completedMaterial.SetColor("_EmissionColor", Color.black);
            completedMaterial.DisableKeyword("_EMISSION");
        }

        if (ObjectiveRing != null && !ObjectiveRing.activeSelf)
            ObjectiveRing.SetActive(true);

        foreach (Renderer targetRenderer in GetComponentsInChildren<Renderer>(true))
        {
            targetRenderer.sharedMaterial = completedMaterial;
            if (targetRenderer is LineRenderer line)
            {
                line.startColor = completedGray;
                line.endColor = completedGray;
            }
        }

        foreach (Light markerLight in GetComponentsInChildren<Light>(true))
        {
            markerLight.color = completedGray;
            markerLight.intensity = 0f;
            markerLight.enabled = false;
        }
        visualsApplied = true;
    }

    private void OnDestroy()
    {
        if (completedMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(completedMaterial);
        else
            DestroyImmediate(completedMaterial);
    }
}
