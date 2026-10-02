using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Unified asynchronous scene loading with a fade, progression checks and a
/// remembered return destination for the mission map.
/// </summary>
public sealed class SceneTransitionManager : MonoBehaviour
{
    private const string ReturnSceneKey = "Navigation.ReturnScene";
    private const float FadeOutDuration = 0.28f;
    private const float FadeInDuration = 0.38f;

    private static SceneTransitionManager instance;

    private Texture2D fadeTexture;
    private GUIStyle loadingStyle;
    private float fadeAlpha;
    private float loadProgress;
    private string loadingLabel;
    private bool transitioning;

    public static bool IsTransitioning => instance != null && instance.transitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
        instance.RecordPlayableScene(SceneManager.GetActiveScene());
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        instance = Object.FindFirstObjectByType<SceneTransitionManager>();
        if (instance == null)
        {
            GameObject host = new GameObject("Scene Transition Manager");
            instance = host.AddComponent<SceneTransitionManager>();
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        fadeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        fadeTexture.name = "Scene Transition Fade";
        fadeTexture.SetPixel(0, 0, new Color(0.006f, 0.025f, 0.035f, 1f));
        fadeTexture.Apply();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (fadeTexture != null)
            Destroy(fadeTexture);

        if (instance == this)
            instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RecordPlayableScene(scene);
    }

    private void RecordPlayableScene(Scene scene)
    {
        if (!IsPlayableScene(scene.name))
            return;

        PlayerPrefs.SetString(ReturnSceneKey, scene.name);
        PlayerPrefs.Save();
    }

    public static bool LoadLevel(int levelNumber)
    {
        if (levelNumber < 1 || levelNumber > 3)
            return false;

        if (!GameProgression.IsUnlocked(levelNumber))
        {
            Debug.LogWarning($"Level {levelNumber} is still locked.");
            return false;
        }

        return LoadScene($"Level{levelNumber}", $"LOADING MISSION {levelNumber:00}");
    }

    public static bool OpenMissionMap()
    {
        EnsureInstance();
        instance.RecordPlayableScene(SceneManager.GetActiveScene());
        return LoadScene("LevelSelect", "OPENING MISSION MAP");
    }

    public static bool ReturnFromMissionMap()
    {
        string returnScene = PlayerPrefs.GetString(ReturnSceneKey, string.Empty);
        int returnLevel = ParseLevelNumber(returnScene);

        if (!IsPlayableScene(returnScene) || !GameProgression.IsUnlocked(returnLevel))
        {
            returnLevel = Mathf.Clamp(GameProgression.HighestUnlocked, 1, 3);
            returnScene = $"Level{returnLevel}";
        }

        return LoadScene(returnScene, $"RETURNING TO MISSION {returnLevel:00}");
    }

    public static bool ReloadCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        int levelNumber = ParseLevelNumber(sceneName);
        string label = levelNumber > 0 ? $"RESTARTING MISSION {levelNumber:00}" : "RELOADING";
        return LoadScene(sceneName, label);
    }

    public static bool LoadScene(string sceneName, string label = null)
    {
        EnsureInstance();
        return instance.BeginLoad(sceneName, label);
    }

    private bool BeginLoad(string sceneName, string label)
    {
        if (sceneName == "LevelSelect" || IsPlayableScene(sceneName))
            sceneName = "Assets/Scenes/" + sceneName + ".unity";
        if (transitioning)
            return false;

        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Scene '{sceneName}' is not available in Build Settings.");
            return false;
        }

        loadingLabel = string.IsNullOrWhiteSpace(label) ? "LOADING" : label;
        StartCoroutine(TransitionRoutine(sceneName));
        return true;
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        transitioning = true;
        loadProgress = 0f;
        Time.timeScale = 1f;

        yield return FadeTo(1f, FadeOutDuration);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (load == null)
        {
            fadeAlpha = 0f;
            transitioning = false;
            yield break;
        }

        load.allowSceneActivation = false;
        while (load.progress < 0.9f)
        {
            loadProgress = Mathf.Clamp01(load.progress / 0.9f);
            yield return null;
        }

        loadProgress = 1f;
        load.allowSceneActivation = true;
        while (!load.isDone)
            yield return null;

        // Give all destination bootstraps one frame before revealing the scene.
        yield return null;
        yield return FadeTo(0f, FadeInDuration);

        loadingLabel = null;
        transitioning = false;
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        float start = fadeAlpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            fadeAlpha = Mathf.Lerp(start, target, t * t * (3f - 2f * t));
            yield return null;
        }

        fadeAlpha = target;
    }

    private void OnGUI()
    {
        if (fadeTexture == null || fadeAlpha <= 0.001f)
            return;

        GUI.depth = -10000;
        Color oldColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, fadeAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), fadeTexture, ScaleMode.StretchToFill);
        GUI.color = oldColor;

        if (fadeAlpha < 0.58f || string.IsNullOrEmpty(loadingLabel))
            return;

        loadingStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height * 0.026f), 18, 32),
            fontStyle = FontStyle.Bold
        };
        loadingStyle.normal.textColor = new Color(0.72f, 0.94f, 0.96f, fadeAlpha);

        string progressText = loadProgress > 0.01f && loadProgress < 1f
            ? $"{loadingLabel}  {Mathf.RoundToInt(loadProgress * 100f):00}%"
            : loadingLabel;
        GUI.Label(new Rect(0f, Screen.height * 0.46f, Screen.width, 48f), progressText, loadingStyle);
    }

    private static bool IsPlayableScene(string sceneName)
    {
        int level = ParseLevelNumber(sceneName);
        return level >= 1 && level <= 3;
    }

    private static int ParseLevelNumber(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith("Level"))
            return 0;

        return int.TryParse(sceneName.Substring(5), out int level) ? level : 0;
    }
}
