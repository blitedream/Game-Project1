using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

[InitializeOnLoad]
public static class HyperateConnectionValidation
{
    const string Key="HyperateConnectionValidation.Active";
    static double since;
    static int index,identity,attempts;
    static string report;
    static readonly string[] route={"Level2","Level3","Level1"};
    static HyperateConnectionValidation(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists("Artifacts/hyperate_test.request"))return;
            File.Delete("Artifacts/hyperate_test.request");
            EditorSceneManager.OpenScene("Assets/Scenes/Level2.unity");
            SessionState.SetBool(Key,true);since=0;index=0;identity=0;report="";EditorApplication.isPlaying=true;return;
        }
        if(!EditorApplication.isPlaying)
        {
            if(!EditorApplication.isPlayingOrWillChangePlaymode)SessionState.SetBool(Key,false);
            return;
        }
        if(since==0)since=EditorApplication.timeSinceStartup;
        var manager=HyperateManager.Instance;
        if(EditorApplication.timeSinceStartup-since>100){Finish("FAIL: "+route[index]+" "+(manager==null?"Missing manager":manager.ConnectionStatus));return;}
        if(SceneTransitionManager.IsTransitioning || SceneManager.GetActiveScene().name!=route[index])return;
        var move=Object.FindFirstObjectByType<SimpleMove>();
        if(move==null || !move.EnvironmentReady || manager==null || !manager.IsSubscribed || !manager.HasServerHeartbeat)return;
        if(!manager.HasLiveHeartRate && EditorApplication.timeSinceStartup-since<50)return;
        if(Object.FindObjectsByType<HyperateManager>(FindObjectsSortMode.None).Length!=1 || Object.FindObjectsByType<HeartRateRuntime>(FindObjectsSortMode.None).Length!=1)
        {Finish("FAIL: duplicate/missing heart rate manager or HUD");return;}
        if(identity==0){identity=manager.GetInstanceID();attempts=manager.ConnectionAttempts;}
        if(identity!=manager.GetInstanceID() || attempts!=manager.ConnectionAttempts){Finish("FAIL: connection restarted during scene transition");return;}
        report+=route[index]+": one shared connection/HUD; live reading="+manager.HasLiveHeartRate+". ";
        if(++index==route.Length){Finish("PASS: "+report+"Same connection persisted across all scenes.");return;}
        since=EditorApplication.timeSinceStartup;
        SceneTransitionManager.LoadScene(route[index]);
    }
    static void Finish(string message)
    {
        File.WriteAllText("Artifacts/hyperate_connection_validation.txt",message);
        SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;
    }
}
