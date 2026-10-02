using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public Transform playerBody;

    public float mouseSensitivity = 200f;

    [Header("Look Limits")]
    public float lookUpLimit = 89.5f;
    public float lookDownLimit = 89.5f;

    [Tooltip("Initial vertical view angle. Positive values look downward.")]
    public float initialPitch = 0f;

    private float xRotation = 0f;

    public void ApplyLevel1ControlProfile()
    {
        mouseSensitivity = 320f;
        lookUpLimit = 89.5f;
        lookDownLimit = 89.5f;
    }

    void Start()
    {
        // Runtime-created/reused cameras are not guaranteed to carry a listener.
        if (GetComponent<Camera>() != null)
        {
            var listener = GetComponent<AudioListener>();
            if (listener == null) listener = gameObject.AddComponent<AudioListener>();
            listener.enabled = true;
            foreach (var other in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (other != listener) other.enabled = false;
        }
        string level = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if ((level == "Level1" || level == "Level2" || level == "Level3") && GetComponent<DiverFlashlight>() == null)
            gameObject.AddComponent<DiverFlashlight>();
        if (level == "Level1" || level == "Level2" || level == "Level3")
            ApplyLevel1ControlProfile();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Existing scenes serialized the old restrictive values. Always allow a
        // near-vertical view so the player can look directly above and below.
        lookUpLimit = Mathf.Max(lookUpLimit, 89.5f);
        lookDownLimit = Mathf.Max(lookDownLimit, 89.5f);
        SetPitch(initialPitch);
    }

    public void SetPitch(float pitch)
    {
        initialPitch = pitch;
        xRotation = Mathf.Clamp(pitch, -lookUpLimit, lookDownLimit);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    void Update()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        float mouseX = Mathf.Clamp(Input.GetAxis("Mouse X"), -3f, 3f) * mouseSensitivity * Time.deltaTime;
        float mouseY = Mathf.Clamp(Input.GetAxis("Mouse Y"), -3f, 3f) * mouseSensitivity * Time.deltaTime;
        
        xRotation -= mouseY;

        xRotation = Mathf.Clamp(
            xRotation,
            -lookUpLimit,
            lookDownLimit
        );

        transform.localRotation = Quaternion.Euler(
            xRotation,
            0f,
            0f
        );

        playerBody.Rotate(
            Vector3.up * mouseX
        );
    }
}
