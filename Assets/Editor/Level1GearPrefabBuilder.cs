using UnityEditor;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public static class Level1GearPrefabBuilder
{
    static Level1GearPrefabBuilder() { EditorApplication.delayCall += Build; }
    static void Build()
    {
        const string path="Assets/Resources/Equipment/Level1DivingGear.prefab";
        if (File.Exists(path)) return;
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("ebe0699eb4935d74594b99aa496f89de"));
        if(model==null) { Debug.LogError("Level1 gear model missing"); return; }
        Directory.CreateDirectory("Assets/Resources/Equipment");
        var instance=Object.Instantiate(model);
        instance.name="Level1DivingGear";
        PrefabUtility.SaveAsPrefabAsset(instance,path);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
    }
}
