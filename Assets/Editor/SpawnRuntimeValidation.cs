using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SpawnRuntimeValidation
{
    const string Key = "SpawnRuntimeValidation.Level";
    static double started;
    static double stableSince;
    static Vector3 spawn;
    static bool sampled;
    static SpawnRuntimeValidation() { EditorApplication.update += Tick; }
    public static void Level2() => Begin("Level2");
    public static void Level3() => Begin("Level3");
    static void Begin(string level)
    {
        started=0; stableSince=0; sampled=false;
        SessionState.SetString(Key, level);
        EditorSceneManager.OpenScene("Assets/Scenes/" + level + ".unity");
        EditorApplication.isPlaying = true;
    }
    static void Tick()
    {
        string level = SessionState.GetString(Key, "");
        if (string.IsNullOrEmpty(level)) { const string request="Artifacts/runtime_spawn.request"; if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(request)) { string requested=File.ReadAllText(request).Trim(); File.Delete(request); if(requested=="Level2" || requested=="Level3") Begin(requested); } return; }
        if (started == 0) started = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - started > 120) { Finish(level, "FAIL: loading timeout", 1); return; }
        if (!EditorApplication.isPlaying || !EditorApplication.isPlayingOrWillChangePlaymode) return;
        var player = GameObject.FindWithTag("Player");
        if (player == null) return;
        var move = player.GetComponent<SimpleMove>();
        if (move == null || !move.EnvironmentReady) return;
        if (!sampled)
        {
            sampled = true;
            spawn = player.transform.position;
            stableSince = EditorApplication.timeSinceStartup;
        }
        if (player.transform.position.y < spawn.y - .15f) { Finish(level, "FAIL: fell from " + spawn + " to " + player.transform.position, 1); return; }
        if (EditorApplication.timeSinceStartup - stableSince < 5) return;
        if (!move.enabled || !move.IsGroundedNow) { Finish(level, "FAIL: movement disabled or not grounded", 1); return; }
        try { Level2RepairValidation.ValidateLevel1Controls(move); }
        catch(Exception ex) { Finish(level,"FAIL: "+ex.Message,1); return; }
        var coach = level == "Level2" ? (MonoBehaviour)UnityEngine.Object.FindFirstObjectByType<Level2CoachDialogue>() : UnityEngine.Object.FindFirstObjectByType<Level3CoachDialogue>();
        if (coach == null || Vector3.Distance(player.transform.position, coach.transform.position) > 8) { Finish(level, "FAIL: coach is not reachable from spawn", 1); return; }
        if (!Level2SinkholeRuntimeLoader.TryFindVisibleCaveGround(player.transform.position, 1.1f, out var ground)) { Finish(level, "FAIL: no visible cave ground under feet", 1); return; } if (!Level2SinkholeRuntimeLoader.TryFindVisibleCaveGround(coach.transform.position + Vector3.up * .2f, .4f, out _)) { Finish(level, "FAIL: coach feet are not on visible ground", 1); return; } if (Camera.main != null && !Application.isBatchMode) { var camera=Camera.main; var rt=new RenderTexture(1280,800,24); var oldTarget=camera.targetTexture; var oldActive=RenderTexture.active; camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; var shot=new Texture2D(1280,800,TextureFormat.RGB24,false); shot.ReadPixels(new Rect(0,0,1280,800),0,0); shot.Apply(); File.WriteAllBytes("Artifacts/"+level+"_grounded_view.png",shot.EncodeToPNG()); RenderTexture.active=oldActive; camera.targetTexture=oldTarget; UnityEngine.Object.DestroyImmediate(shot); UnityEngine.Object.DestroyImmediate(rt); } Finish(level, "PASS: visible ground under player and coach; actual scene loaded; player " + spawn + " remains grounded for 5 seconds; movement enabled; coach present nearby; Player tag set.", 0);
    }
    static void Finish(string level, string result, int code)
    {
        File.WriteAllText("Artifacts/" + level + "_spawn_runtime.txt", result);
        Debug.Log(result);
        SessionState.EraseString(Key);
        if(Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.isPlaying=false;
    }
}

