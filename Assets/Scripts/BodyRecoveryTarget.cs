using TMPro;
using UnityEngine;

[ExecuteAlways]
public class BodyRecoveryTarget : MonoBehaviour
{
    public Transform player;
    public Level3MissionController mission;
    public float interactDistance = 4f;
    public string prompt = "Press E to recover body";

    private TextMeshPro promptText;

    void OnEnable() => EnsurePreview();

    void Start()
    {
        EnsurePreview();
        promptText.gameObject.SetActive(!Application.isPlaying);
    }

    void Update()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        if (!Application.isPlaying)
        {
            EnsurePreview();
            promptText.gameObject.SetActive(true);
            promptText.transform.localPosition = Vector3.up * 2.2f;
            return;
        }

        if (player == null || mission == null || mission.BodyRecovered)
        {
            if (promptText != null)
                promptText.gameObject.SetActive(false);
            return;
        }

        float distance = Vector3.Distance(player.position, transform.position);
        bool canInteract = distance <= interactDistance && mission.CanRecoverBody;

        promptText.gameObject.SetActive(canInteract);

        if (canInteract)
        {
            promptText.transform.position = transform.position + Vector3.up * 2.2f;

            if (Camera.main != null)
            {
                promptText.transform.LookAt(Camera.main.transform);
                promptText.transform.Rotate(0f, 180f, 0f);
            }

            if (Input.GetKeyDown(KeyCode.E))
                mission.RecoverBody();
        }
    }

    public void SetRecovered()
    {
        if (promptText != null)
            promptText.gameObject.SetActive(false);
        enabled = false;
    }

    private void CreatePrompt()
    {
        GameObject textObject = new GameObject("BodyRecoveryPrompt");
        textObject.transform.SetParent(transform, false);

        promptText = textObject.AddComponent<TextMeshPro>();
        promptText.text = prompt;
        promptText.fontSize = 4f;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = new Color(1f, 0.86f, 0.35f);
        promptText.rectTransform.sizeDelta = new Vector2(24f, 5f);
    }

    private void EnsurePreview()
    {
        if (promptText == null)
            promptText = transform.Find("BodyRecoveryPrompt")?.GetComponent<TextMeshPro>();
        if (promptText == null)
            CreatePrompt();
    }
}
