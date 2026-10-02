using UnityEngine;

// This component used to create a glowing ascent ring at the old pool bottom.
// The current Level 1 flow uses the 20 m and 40 m depth objectives instead, so
// any serialized copy of this legacy object must remove itself permanently.
// Legacy cleanup version 2: the editor builder no longer recreates this object.
[ExecuteAlways]
public sealed class PoolBottomMarker : MonoBehaviour
{
    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += RemoveLegacyObjectInEditor;
            return;
        }
#endif
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void RemoveLegacyObjectInEditor()
    {
        if (this == null || gameObject == null)
            return;

        UnityEngine.SceneManagement.Scene scene = gameObject.scene;
        DestroyImmediate(gameObject);

        if (!scene.IsValid() || !scene.isLoaded)
            return;

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        if (scene.name == "Level1" && !string.IsNullOrEmpty(scene.path))
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }
#endif
}
