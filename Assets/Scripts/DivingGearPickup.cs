using UnityEngine;
using TMPro;

[ExecuteAlways]
public class DivingGearPickup : MonoBehaviour
{
    public Transform player;
    public float interactDistance = 5f;

    public string hintText = "Press E to wear it";
    public float textHeight = 3.5f;
    public float textForwardDistance = 2.5f;
    public float textSize = 6f;

    private TextMeshPro textObj;
    private PlayerGearState gearState;

    void OnEnable() => EnsurePreview();

    void Start()
    {
        if (player != null)
            gearState = player.GetComponent<PlayerGearState>();

        EnsurePreview();
        textObj.gameObject.SetActive(!Application.isPlaying);
    }

    void Update()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        if (!Application.isPlaying)
        {
            EnsurePreview();
            textObj.gameObject.SetActive(true);
            textObj.transform.localPosition = Vector3.up * textHeight;
            return;
        }

        if (player == null || textObj == null) return;

        var level2Mission = player.GetComponent<Level2MissionController>();
        if (level2Mission != null && !level2Mission.CanCollectGear)
        {
            textObj.gameObject.SetActive(false);
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        bool near = distance <= interactDistance;

        textObj.gameObject.SetActive(near);

        if (!near) return;

        Vector3 dirToPlayer = (player.position - transform.position).normalized;

        textObj.transform.position =
            transform.position + Vector3.up * textHeight + dirToPlayer * textForwardDistance;

        if (Camera.main != null)
        {
            textObj.transform.LookAt(Camera.main.transform);
            textObj.transform.Rotate(0, 180, 0);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log("Picked up diving gear.");

            if (gearState != null)
                gearState.EquipDivingGear();

            gameObject.SetActive(false);
        }
    }

    void CreateText()
    {
        GameObject t = new GameObject("PickupText");
        t.transform.SetParent(transform, false);

        textObj = t.AddComponent<TextMeshPro>();
        textObj.text = hintText;
        textObj.fontSize = textSize;
        textObj.alignment = TextAlignmentOptions.Center;
        textObj.color = Color.yellow;

        textObj.rectTransform.sizeDelta = new Vector2(20f, 5f);
    }

    private void EnsurePreview()
    {
        if (textObj == null)
            textObj = transform.Find("PickupText")?.GetComponent<TextMeshPro>();
        if (textObj == null)
            CreateText();
    }
}
