using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SceneEntryValidation
{
    const string Key="SceneEntryValidation.Active";
    static int index;
    static double since;
    static readonly string[] route={"LevelSelect","Level1","LevelSelect","Level2","LevelSelect","Level3"};
    static SceneEntryValidation(){EditorApplication.update+=Tick;}
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    static void Tick()
    {
        if(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists("Artifacts/level3_preview.request"))
        {
            File.Delete("Artifacts/level3_preview.request");EditorSceneManager.OpenScene("Assets/Scenes/Level3.unity");return;
        }
        if(!SessionState.GetBool(Key,false))
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Artifacts/scene_entry.request"))return;
            File.Delete("Artifacts/scene_entry.request");EditorSceneManager.OpenScene("Assets/Scenes/LevelSelect.unity");
            SessionState.SetBool(Key,true);index=0;since=0;EditorApplication.isPlaying=true;return;
        }
        if(!EditorApplication.isPlaying)return;
        if(since==0)since=EditorApplication.timeSinceStartup;
        try
        {
            if(EditorApplication.timeSinceStartup-since>180)throw new Exception("Scene timed out: "+route[index]);
            if(SceneTransitionManager.IsTransitioning || SceneManager.GetActiveScene().name!=route[index] || EditorApplication.timeSinceStartup-since<5)return;
            string scene=route[index];
            if(scene=="LevelSelect")Check(UnityEngine.Object.FindObjectsByType<LevelSelectNode>(FindObjectsSortMode.None).Length==3,"Missing map nodes");
            else
            {
                var move=UnityEngine.Object.FindFirstObjectByType<SimpleMove>();if(move==null || !move.EnvironmentReady)return;
                var breathing=move.GetComponent<PlayerBreathingAudio>();
                Check(breathing!=null,"Missing breathing in "+scene);
                if(move.transform.Find("Unequipped Breathing")==null || move.transform.Find("Equipped Breathing")==null)return;
                var natural=move.transform.Find("Unequipped Breathing").GetComponent<AudioSource>();
                var scuba=move.transform.Find("Equipped Breathing").GetComponent<AudioSource>();
                Check(natural.clip!=null && scuba.clip!=null && natural.loop && scuba.loop,"Breathing clips missing or not looping");
                var breathingSamples=new float[natural.clip.samples*natural.clip.channels];
                Check(natural.clip.GetData(breathingSamples,0),"Cannot read natural breathing");
                double energy=0;foreach(float sample in breathingSamples)energy+=sample*sample;
                Check(System.Math.Sqrt(energy/breathingSamples.Length)>.05,"Natural breathing is still inaudibly quiet");
                var output=new float[1024];AudioListener.GetOutputData(output,0);
                float outputPeak=0;foreach(float sample in output)outputPeak=Mathf.Max(outputPeak,Mathf.Abs(sample));
                if(outputPeak<.00001f)return;
                var gear=move.GetComponent<PlayerGearState>();bool originalGear=gear.hasDivingGear;
                var updateBreathing=typeof(PlayerBreathingAudio).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var shorePosition=move.transform.position;
                foreach(bool wearing in new[]{false,true})
                {
                    gear.hasDivingGear=wearing;
                    foreach(float depth in new[]{0f,2f})
                    {
                        move.transform.position=new Vector3(move.waterAreaCenterXZ.x,move.waterSurfaceY+move.waterProbeOffset-depth,move.waterAreaCenterXZ.y);
                        for(int n=0;n<2000;n++)updateBreathing.Invoke(breathing,null);
                        Check(scuba.volume>0 && natural.volume==0,"Water contact must select underwater sound regardless of gear");
                    }
                    move.transform.position=shorePosition;
                    for(int n=0;n<2000;n++)updateBreathing.Invoke(breathing,null);
                    Check(natural.volume>0 && scuba.volume==0,"Shore must restore natural breathing regardless of gear");
                }
                gear.hasDivingGear=originalGear;
                var flashlight=Camera.main.GetComponent<DiverFlashlight>();
                int listenerCount=0;
                foreach(var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                    if(listener.isActiveAndEnabled)listenerCount++;
                Check(listenerCount==1 && Camera.main.GetComponent<AudioListener>().enabled,"Player audio listener missing or duplicated");
                Check(flashlight!=null && !flashlight.IsOn,"Missing flashlight or incorrect default");
                flashlight.Toggle();Check(flashlight.IsOn && flashlight.Beam.type==LightType.Spot && flashlight.Beam.shadows!=LightShadows.None,"Flashlight did not turn on");
                flashlight.Toggle();Check(!flashlight.IsOn,"Flashlight did not turn off");
                var switchSound=flashlight.Beam.GetComponent<AudioSource>();
                Check(switchSound!=null && switchSound.clip!=null && switchSound.isPlaying,"Flashlight click missing");
                var coachVoice=UnityEngine.Object.FindFirstObjectByType<CoachVoice>();
                Check(coachVoice!=null,"Coach voice missing in "+scene);
                Check(Resources.LoadAll<AudioClip>("Audio/Coach").Length==8,"Coach syllables missing");
                coachVoice.SetLine("Coach: Bala mala.");
                typeof(CoachVoice).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(coachVoice,null);
                Check(coachVoice.GetComponent<AudioSource>().isPlaying,"Coach voice did not start");
                coachVoice.SetLine(null);
                Check(!coachVoice.GetComponent<AudioSource>().isPlaying,"Coach voice did not stop");
                var markers=UnityEngine.Object.FindObjectsByType<DepthTrainingMarker>(FindObjectsSortMode.None);
                Check(markers.Length>=3,"Missing depth buoys in "+scene);
                var marker=markers[0];
                if(!marker.Touched)
                {
                    var position=move.transform.position;
                    move.transform.position=marker.transform.position;
                    typeof(DepthTrainingMarker).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(marker,null);
                    move.transform.position=position;
                    Check(marker.Touched && !marker.Completed,"Touch must dim buoy without completing mission");
                    foreach(var lamp in marker.GetComponentsInChildren<Light>())Check(!lamp.enabled,"Touched buoy still emits light");
                    Check(marker.GetComponent<Renderer>().sharedMaterial.shader.name.Contains("Lit"),"Touched buoy still uses unlit material");
                    marker.Touch();Check(!marker.Completed,"Repeated touch completes mission");
                }
                Check(UnityEngine.Object.FindFirstObjectByType<CenoteWaterAtmosphere>()!=null,"Missing depth lighting");
                var water=GameObject.Find(scene=="Level1"?"WaterVisual":"Single Cave Water Surface");
                Check(water!=null,"Missing water surface");
                var waterMaterial=water.GetComponent<Renderer>().sharedMaterial;
                Check(waterMaterial.shader.name=="GP1/Level2/Kelkaya Water" && waterMaterial.GetColor("_ShallowColor").a<.3f,"Water surface remains opaque");
                CaptureWater(water.transform.position,scene);
                Check(UnityEngine.Object.FindObjectsByType<LevelMapHotkey>(FindObjectsSortMode.None).Length==1,"Duplicate map hotkeys");
                if(scene=="Level1")Check(UnityEngine.Object.FindFirstObjectByType<Level1MissionController>()!=null,"Missing Level1 mission");
                else
                {
                    Check(move.IsGroundedNow,"Ungrounded entry "+scene);
                    Check(GameObject.Find("Level2 solid shore ring")!=null,"Old terrain loaded "+scene);
                    Check(GameObject.Find("Level1 Diving Gear Model")!=null,"Old equipment loaded "+scene);
                    Check(GameObject.Find(Level2SinkholeBootstrap.PreviewRootName)==null,"Editor preview remains active");
                    if(scene=="Level3")
                    {
                        Check(UnityEngine.Object.FindFirstObjectByType<Level3MissionController>()!=null,"Recovery story missing");
                        var diver=GameObject.Find("Curled Scuba Diver Visual");
                        if(diver==null || diver.GetComponentsInChildren<Renderer>().Length==0)return;
                        float bottom=float.PositiveInfinity;
                        foreach(var renderer in diver.GetComponentsInChildren<Renderer>())bottom=Mathf.Min(bottom,renderer.bounds.min.y);
                        var target=UnityEngine.Object.FindFirstObjectByType<Level3MissionController>().recoveryTarget;
                        bool onFloor=false;
                        foreach(var hit in Physics.RaycastAll(new Vector3(target.position.x,bottom+1,target.position.z),Vector3.down,2,~0,QueryTriggerInteraction.Ignore))
                            if(hit.collider.GetComponent<CaveGroundSurface>()!=null && Mathf.Abs(bottom-hit.point.y)<.15f)onFloor=true;
                        Check(onFloor,"Recovery diver not resting on cave floor");
                        Check(GameObject.Find("Dive Lamp")==null,"Old permanent lamp remains");
                        File.WriteAllText("Artifacts/recovery_floor_validation.txt",$"PASS: diver bottom {bottom:F2}; target {target.position}; depth {move.waterSurfaceY-bottom:F2}m; one active camera AudioListener.");
                    }
                }
            }
            if(++index==route.Length){Finish("PASS: all three scenes have transparent water, calibrated natural breathing and nonzero AudioListener output; water-contact breathing switches correctly with and without gear, coach voice and flashlight play; grounded entries and recovery diver on the floor.");return;}
            since=EditorApplication.timeSinceStartup;Check(SceneTransitionManager.LoadScene(route[index]),"Transition rejected");
        }
        catch(Exception ex){Finish("FAIL: "+ex);}
    }
    static void CaptureWater(Vector3 center,string scene)
    {
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
        var previous=camera.targetTexture;var active=RenderTexture.active;
        var target=new RenderTexture(960,640,24);var image=new Texture2D(960,640,TextureFormat.RGB24,false);
        try
        {
            camera.transform.position=center+(scene=="Level1"?new Vector3(0,2,-3):new Vector3(0,9,-9));camera.transform.LookAt(center+Vector3.down*3);
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,960,640),0,0);image.Apply();
            File.WriteAllBytes("Artifacts/"+scene+"_clear_water.png",image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=previous;RenderTexture.active=active;
            camera.transform.SetPositionAndRotation(position,rotation);
            UnityEngine.Object.DestroyImmediate(image);target.Release();UnityEngine.Object.DestroyImmediate(target);
        }
    }
    static void Finish(string result){File.WriteAllText("Artifacts/scene_entry_validation.txt",result);Debug.Log(result);SessionState.SetBool(Key,false);File.WriteAllText("Artifacts/level3_preview.request","run");EditorApplication.isPlaying=false;}
}
