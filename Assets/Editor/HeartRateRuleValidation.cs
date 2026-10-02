using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Reflection;

[InitializeOnLoad]
public static class HeartRateRuleValidation
{
    const string Key="HeartRateRuleValidation.Active";
    static int index=1;
    static double since;
    static string report="Detector checks passed: rise/fall, exact threshold, drift, spike, gap, burst. ";
    static HeartRateRuleValidation(){EditorApplication.update+=Tick;}
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static bool Feed(HeartRateChangeDetector d,int[] values,float start=0){bool failed=false;for(int i=0;i<values.Length;i++)failed|=d.Add(values[i],start+i);return failed;}
    static void Rules()
    {
        var d=new HeartRateChangeDetector();
        Check(!Feed(d,new[]{80,80,81,82,83,84,85,86,87,88,89,90,91}),"stable drift");
        d.Reset();Check(!Feed(d,new[]{80,80,80,125,80,80,80,80,80}),"isolated spike");
        d.Reset();Check(Feed(d,new[]{80,80,80,110,110,110,110}),"sustained rise");
        d.Reset();Check(Feed(d,new[]{115,115,115,80,80,80,80}),"sustained fall");
        d.Reset();Feed(d,new[]{80,80,80});Check(!Feed(d,new[]{120,120,120,120,120},20),"gap must reset");
        d.Reset();Feed(d,new[]{80,80,80});for(int i=0;i<20;i++)Check(!d.Add(125,2.1f+i*.001f),"queued burst");
        d.Reset();Check(!Feed(d,new[]{80,80,80,104,104,104,104,104}),"below threshold");
        d.Reset();Check(Feed(d,new[]{80,80,80,105,105,105,105}),"exact threshold");
    }
    static void Tick()
    {
        try
        {
            if(!SessionState.GetBool(Key,false))
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists("Artifacts/heart_rate_rule_test.request"))return;
                File.Delete("Artifacts/heart_rate_rule_test.request");Rules();
                report="Detector checks passed: rise/fall, exact threshold, drift, spike, gap, burst. ";index=1;since=0;
                EditorSceneManager.OpenScene("Assets/Scenes/Level1.unity");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;return;
            }
            if(!EditorApplication.isPlaying){if(!EditorApplication.isPlayingOrWillChangePlaymode)SessionState.SetBool(Key,false);return;}
            if(since==0)since=EditorApplication.timeSinceStartup;
            if(EditorApplication.timeSinceStartup-since>180)throw new Exception("scene timeout");
            if(SceneTransitionManager.IsTransitioning || SceneManager.GetActiveScene().name!="Level"+index)return;
            var move=UnityEngine.Object.FindFirstObjectByType<SimpleMove>();
            if(move==null || !move.EnvironmentReady)return;
            if(index==3)
            {
                var diver=GameObject.Find("Curled Scuba Diver Visual");
                if(diver==null || diver.GetComponentsInChildren<Renderer>().Length==0)return;
            }
            var runtime=HeartRateRuntime.Instance;var run=LevelRunManager.Current;
            Check(runtime!=null && run!=null,"missing runtime/run");
            if(index==1)Check(runtime.NeedsConnection && HeartRateRuntime.BlocksGameplay,"initial connection gate");
            // Isolated synthetic readings exercise the gameplay rule, not the live HUD or server.
            typeof(HeartRateRuntime).GetField("accepted",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(runtime,true);
            Time.timeScale=1;runtime.BeginMonitoring();
            var read=typeof(HeartRateRuntime).GetMethod("OnReading",BindingFlags.Instance|BindingFlags.NonPublic);
            float t=Time.realtimeSinceStartup;int n=0;
            foreach(int bpm in new[]{80,80,80,110,110,110,110})read.Invoke(runtime,new object[]{bpm,t+n++});
            Check(run.HasFailed && Time.timeScale==0 && HeartRateRuntime.BlocksGameplay,"failure must stop level "+index);
            run.CompleteLevel();Check(run.HasFailed,"failure overwritten by completion");
            report+="Level"+index+" failure/pause passed. ";
            if(index==3){Finish("PASS: "+report);return;}
            index++;since=EditorApplication.timeSinceStartup;SceneTransitionManager.LoadScene("Level"+index);
        }
        catch(Exception e){Finish("FAIL: "+e.Message);}
    }
    static void Finish(string result){File.WriteAllText("Artifacts/heart_rate_rule_validation.txt",result);SessionState.SetBool(Key,false);if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;}
}
