using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Level3SceneBuilderEditor
{
    static Level3SceneBuilderEditor()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += BuildIfLevel3IsOpenAndEmpty;
    }

    [MenuItem("Tools/Level 3/Rebuild Zacaton Scene")]
    public static void RebuildLevel3Scene()
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.name != "Level3")
        {
            Debug.LogWarning("Open Assets/Scenes/Level3.unity before rebuilding the Zacaton scene.");
            return;
        }

        GameObject root = Level3AutoBootstrap.RebuildScene();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;

        Debug.Log("Level 3 Zacaton scene rebuilt in edit mode. Save the scene to keep the generated objects.");
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.name == "Level3")
            EditorApplication.delayCall += BuildIfLevel3IsOpenAndEmpty;
    }

    private static void BuildIfLevel3IsOpenAndEmpty()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Level3")
            return;

        GameObject existingRoot = GameObject.Find(Level3AutoBootstrap.RootName);
        if (existingRoot != null && existingRoot.transform.childCount > 0)
            return;

        GameObject root = Level3AutoBootstrap.RebuildScene();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
    }
}
