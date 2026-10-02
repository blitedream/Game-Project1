using UnityEngine;

[DisallowMultipleComponent]
public sealed class DiverFlashlight : MonoBehaviour
{
    public Light Beam { get; private set; }
    public bool IsOn => Beam != null && Beam.enabled;
    private AudioSource switchAudio;

    private void Awake()
    {
        var lamp = new GameObject("Diver Flashlight Beam");
        lamp.transform.SetParent(transform, false);
        lamp.transform.localPosition = new Vector3(.16f, -.16f, .12f);
        Beam = lamp.AddComponent<Light>();
        Beam.type = LightType.Spot;
        Beam.color = new Color(.88f, .95f, 1f);
        Beam.intensity = 12f;
        Beam.range = 32f;
        Beam.spotAngle = 52f;
        Beam.innerSpotAngle = 26f;
        Beam.shadows = LightShadows.Soft;
        Beam.shadowBias = .02f;
        Beam.shadowNormalBias = .08f;
        Beam.enabled = false;
        switchAudio = lamp.AddComponent<AudioSource>();
        switchAudio.playOnAwake = false;
        switchAudio.spatialBlend = 0;
        switchAudio.volume = .45f;
        switchAudio.clip = Resources.Load<AudioClip>("Audio/Effects/FlashlightSwitch");
    }

    public void Toggle()
    {
        if (Beam == null) return;
        Beam.enabled = !Beam.enabled;
        if (switchAudio != null && switchAudio.clip != null) switchAudio.Play();
    }

    private void Update()
    {
        if (Time.timeScale > 0 && !SceneTransitionManager.IsTransitioning && Input.GetMouseButtonDown(1)) Toggle();
    }

    private void OnGUI()
    {
        if (UnderwaterPanoramaMode.IsActive) return;
        GUI.Label(new Rect(Screen.width - 230, Screen.height - 58, 225, 22), "RMB  FLASHLIGHT " + (IsOn ? "ON" : "OFF"));
    }

    private void OnDestroy() { if (Beam != null) Destroy(Beam.gameObject); }
}
