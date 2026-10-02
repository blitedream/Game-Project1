using TMPro;
using UnityEngine;

public sealed class LevelSelectNode : MonoBehaviour
{
    public static LevelSelectNode Current { get; private set; }

    public string SceneName;
    public string DisplayName;
    public string Description;
    public string Risk;
    public string Number;
    public Color BaseColor;
    public int LevelNumber;

    private Vector3 baseScale;
    private bool hovered;

    public bool IsUnlocked => GameProgression.IsUnlocked(LevelNumber);

    private void Start()
    {
        baseScale = transform.localScale;
        if (!IsUnlocked && TryGetComponent(out Renderer targetRenderer))
        {
            Material locked = new Material(targetRenderer.sharedMaterial);
            locked.color = new Color(0.16f, 0.2f, 0.22f, 0.75f);
            targetRenderer.material = locked;
            TextMeshPro label = transform.parent.GetComponentInChildren<TextMeshPro>();
            if (label != null)
            {
                label.text = DisplayName + "\nLOCKED";
                label.color = new Color(0.55f, 0.62f, 0.64f);
            }
        }
    }

    private void Update()
    {
        bool active = hovered || Current == this;
        Vector3 target = baseScale * (active ? 1.3f : 1f);
        transform.localScale = Vector3.Lerp(transform.localScale, target, Time.deltaTime * 9f);
        transform.Rotate(Vector3.up, (active ? 65f : 24f) * Time.deltaTime, Space.World);
    }

    private void OnMouseEnter() => hovered = true;
    private void OnMouseExit() => hovered = false;

    private void OnMouseDown() => Select();

    public void Select()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        if (IsUnlocked)
        {
            SceneTransitionManager.LoadLevel(LevelNumber);
            return;
        }
        Current = this;
        if (Camera.main != null)
            Camera.main.GetComponent<MapCameraController>()?.FocusOn(transform.parent.position);
    }

    public static void ClearSelection() => Current = null;
}
