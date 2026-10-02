using UnityEngine;

// Short voiced syllables form fictional speech; subtitle text remains authoritative.
[DisallowMultipleComponent]
public sealed class CoachVoice : MonoBehaviour
{
    AudioSource source;
    AudioClip[] syllables;
    string line;
    float next, until;
    System.Random random = new System.Random(731);

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        source.volume = .24f;
        syllables = Resources.LoadAll<AudioClip>("Audio/Coach");
    }

    public void SetLine(string text)
    {
        if (line == text) return;
        source.Stop();
        line = text;
        next = Time.unscaledTime;
        until = next + (text == null ? 0 : Mathf.Clamp(text.Length * .035f, 1.1f, 4.5f));
    }

    void Update()
    {
        if (Time.timeScale == 0 || SceneTransitionManager.IsTransitioning || UnderwaterPanoramaMode.IsActive)
        {
            source.Stop();
            return;
        }
        if (line == null || syllables.Length == 0 || Time.unscaledTime >= until || Time.unscaledTime < next) return;
        source.clip = syllables[random.Next(syllables.Length)];
        source.pitch = 1.35f + (float)random.NextDouble() * .4f;
        source.Play();
        next = Time.unscaledTime + source.clip.length / source.pitch + .025f + (random.Next(5) == 0 ? .16f : 0);
    }

    void OnDisable() { line = null; if (source != null) source.Stop(); }
}
