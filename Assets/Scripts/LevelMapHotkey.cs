using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LevelMapHotkey : MonoBehaviour
{
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
        if (FindFirstObjectByType<LevelMapHotkey>() == null)
            new GameObject("Level Map Hotkey").AddComponent<LevelMapHotkey>();
    }

    private void Update()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        if (SceneTransitionManager.IsTransitioning)
            return;

        string scene = SceneManager.GetActiveScene().name;
        if (scene != "LevelSelect" && Input.GetKeyDown(KeyCode.M))
        {
            SceneTransitionManager.OpenMissionMap();
        }
        else if (scene == "LevelSelect" && Input.GetKeyDown(KeyCode.Escape))
        {
            MapCameraController controller = Camera.main != null ? Camera.main.GetComponent<MapCameraController>() : null;
            if (controller != null && controller.IsFocused) controller.ExitFocus();
            else SceneTransitionManager.ReturnFromMissionMap();
        }
    }
}
