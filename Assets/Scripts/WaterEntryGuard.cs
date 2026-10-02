using UnityEngine;
using TMPro;

[ExecuteAlways]
public class WaterEntryGuard : MonoBehaviour
{
    public Transform player;
    public PlayerGearState gearState;
    public SimpleMove playerMove;

    public string warningMessage = "No diving gear.\nEntry prohibited.";
    public float warningHeight = 2f;
    public float warningSize = 4f;
    public float warningDistance = 6f;

    private TextMeshPro warningText;
    private Vector3 lastSafePosition;

    void OnEnable() => EnsurePreview();

    void Start()
    {
        EnsurePreview();
        warningText.gameObject.SetActive(!Application.isPlaying);

        if (player != null)
            lastSafePosition = player.position;
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            EnsurePreview();
            warningText.gameObject.SetActive(true);
            warningText.transform.localPosition = Vector3.up * warningHeight;
            return;
        }

        if (player == null || gearState == null) return;

        float distance = Vector3.Distance(player.position, transform.position);

        if (distance < warningDistance && !gearState.hasDivingGear)
        {
            ShowWarning();
        }
        else
        {
            warningText.gameObject.SetActive(false);
        }

        if (!gearState.hasDivingGear && distance >= warningDistance)
        {
            lastSafePosition = player.position;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.transform != player) return;

        if (!gearState.hasDivingGear)
        {
            player.position = lastSafePosition;

            if (playerMove != null)
                playerMove.ResetVerticalVelocity();

            ShowWarning();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.transform != player) return;

        if (!gearState.hasDivingGear)
        {
            player.position = lastSafePosition;

            if (playerMove != null)
                playerMove.ResetVerticalVelocity();

            ShowWarning();
        }
    }

    void ShowWarning()
    {
        warningText.gameObject.SetActive(true);
        warningText.transform.position = transform.position + Vector3.up * warningHeight;

        if (Camera.main != null)
        {
            warningText.transform.LookAt(Camera.main.transform);
            warningText.transform.Rotate(0, 180, 0);
        }
    }

    void CreateWarningText()
    {
        GameObject t = new GameObject("WaterWarningText");
        t.transform.SetParent(transform, false);

        warningText = t.AddComponent<TextMeshPro>();
        warningText.text = warningMessage;
        warningText.fontSize = warningSize;
        warningText.color = Color.red;
        warningText.alignment = TextAlignmentOptions.Center;
        warningText.rectTransform.sizeDelta = new Vector2(20f, 5f);
    }

    private void EnsurePreview()
    {
        if (warningText == null)
            warningText = transform.Find("WaterWarningText")?.GetComponent<TextMeshPro>();
        if (warningText == null)
            CreateWarningText();
    }
}
