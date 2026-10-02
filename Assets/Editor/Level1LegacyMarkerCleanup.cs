using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class Level1LegacyMarkerCleanup
{
    private const string Level1ScenePath = "Assets/Scenes/Level1.unity";

    [MenuItem("Tools/GP1/Cleanup Legacy Level 1 Bottom Marker")]
    private static void Cleanup()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        bool openedAdditively = activeScene.path != Level1ScenePath;
        Scene level1Scene = openedAdditively
            ? EditorSceneManager.OpenScene(Level1ScenePath, OpenSceneMode.Additive)
            : activeScene;

        int removed = 0;
        foreach (GameObject root in level1Scene.GetRootGameObjects())
        {
            if (root.name != "Pool Bottom Ascend Marker" && root.GetComponent<PoolBottomMarker>() == null)
                continue;

            Object.DestroyImmediate(root);
            removed++;
        }

        if (removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(level1Scene);
            EditorSceneManager.SaveScene(level1Scene);
        }

        if (openedAdditively)
            EditorSceneManager.CloseScene(level1Scene, true);

        Debug.Log($"Level 1 legacy bottom marker cleanup completed; removed {removed} root object(s).");
    }
}
