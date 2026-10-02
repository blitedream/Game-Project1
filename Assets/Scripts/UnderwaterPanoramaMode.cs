using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class UnderwaterPanoramaMode : MonoBehaviour
{
    public static bool IsActive { get; private set; }

    private readonly List<CanvasState> canvases = new List<CanvasState>();

    private struct CanvasState
    {
        public Canvas Canvas;
        public bool Enabled;
    }

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
        string scene = SceneManager.GetActiveScene().name;
        if ((scene == "Level1" || scene == "Level2" || scene == "Level3") &&
            FindFirstObjectByType<UnderwaterPanoramaMode>() == null)
            new GameObject("Underwater Panorama [K]").AddComponent<UnderwaterPanoramaMode>();
    }

    private void Update()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        if (Input.GetKeyDown(KeyCode.K))
        {
            if (IsActive) ExitPanorama();
            else EnterPanorama();
        }

    }

    private void EnterPanorama()
    {
        canvases.Clear();
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            canvases.Add(new CanvasState { Canvas = canvas, Enabled = canvas.enabled });
            canvas.enabled = false;
        }

        IsActive = true;
    }

    private void ExitPanorama()
    {
        foreach (CanvasState state in canvases)
            if (state.Canvas != null) state.Canvas.enabled = state.Enabled;
        canvases.Clear();

        IsActive = false;
    }

    private void OnGUI()
    {
        if (!IsActive)
        {
            GUIStyle hint = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleRight };
            hint.normal.textColor = new Color(0.7f, 0.9f, 1f, 0.8f);
            GUI.Label(new Rect(Screen.width - 230f, Screen.height - 34f, 210f, 22f), "K  HIDE UI", hint);
            return;
        }

        // Active mode intentionally draws nothing: K is a pure clean-screen toggle.
    }

    private void OnDestroy()
    {
        if (IsActive)
            ExitPanorama();
    }
}
