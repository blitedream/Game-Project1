using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class Level2RepairValidation
{
    const string Key="Level2RepairValidation.Active";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static double since;
    static Level2RepairValidation() { EditorApplication.update+=Tick; }
    static void Require(bool condition,string reason) { if(!condition) throw new Exception(reason); }
    static void Tick()
    {
        if(SessionState.GetBool(Key,false) && !EditorApplication.isPlayingOrWillChangePlaymode)
            SessionState.SetBool(Key,false);
        if(!SessionState.GetBool(Key,false))
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists("Artifacts/level2_repair.request")) return;
            File.Delete("Artifacts/level2_repair.request");
            EditorSceneManager.OpenScene("Assets/Scenes/Level2.unity");
            SessionState.SetBool(Key,true); since=0; EditorApplication.isPlaying=true; return;
        }
        if(!EditorApplication.isPlaying) return;
        var player=GameObject.FindWithTag("Player");
        if(player==null) return;
        var mission=player.GetComponent<Level2MissionController>();
        var move=player.GetComponent<SimpleMove>();
        if(mission==null || move==null || !move.EnvironmentReady) return;
        if(since==0) since=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-since<5) return;
        try { Validate(player,mission,move); Finish("PASS: Level2 25/50/75m story sequence and continuous holds; slower Level2 pace; solid shore ring; Level1 equipment; grounded spawn; interaction gates; 75m descent/ascent and underwater side collision checks."); }
        catch(Exception ex) { Finish("FAIL: "+ex); }
    }
    static void Validate(GameObject player,Level2MissionController mission,SimpleMove move)
    {
        var coach=UnityEngine.Object.FindFirstObjectByType<Level2CoachDialogue>();
        var pickup=UnityEngine.Object.FindFirstObjectByType<DivingGearPickup>();
        Require(move.enabled && move.IsGroundedNow,"Player not grounded/movable");
        Require(!mission.HasStarted && !mission.CanCollectGear,"Interaction enabled before Enter");
        Require(coach!=null && pickup!=null,"Missing coach/equipment");
        Require(!coach.InitialCompleted && !player.GetComponent<PlayerGearState>().hasDivingGear,"Pre-start interaction already completed");
        Require(Vector3.Distance(coach.transform.position,pickup.transform.position)>8,"Stations too close");
        Require(Vector3.Distance(player.transform.position,coach.transform.position)>4,"Coach too close to spawn");
        Require(Level2SinkholeRuntimeLoader.TryFindVisibleCaveGround(coach.transform.position+Vector3.up*.2f,.4f,out _),"Coach floats");
        Require(Level2SinkholeRuntimeLoader.TryFindVisibleCaveGround(pickup.transform.position+Vector3.up*.2f,.5f,out _),"Equipment floats");
        var hint=pickup.transform.Find("PickupText"); Require(hint!=null && !hint.gameObject.activeSelf,"Pre-start equipment hint visible");
        Require(pickup.transform.Find("Level1 Diving Gear Model") != null,"Not using Level1 equipment");
        Vector3 center=Level2SinkholeBootstrap.FusedWaterCenter;
        for(float radius=12.3f;radius<=23.5f;radius+=.4f) for(int angle=0;angle<360;angle+=2)
        {
            Vector3 p=center+Quaternion.Euler(0,angle,0)*Vector3.forward*radius;
            Require(Level2SinkholeRuntimeLoader.TryFindVisibleCaveGround(p+Vector3.up*20,30,out var ground),"Shore hole: "+p);
            Require(ground.point.y>=center.y+.45f,"Shore sinks below water: "+p);
        }
        var stage=typeof(Level2MissionController).GetField("stage",Hidden);
        ValidateLevel1Controls(move);
        stage.SetValue(mission,Enum.Parse(stage.FieldType,"TalkToCoach")); Require(mission.HasStarted && !mission.CanCollectGear,"Briefing gate wrong");
        stage.SetValue(mission,Enum.Parse(stage.FieldType,"EquipGear")); Require(mission.CanCollectGear,"Equipment remains locked after briefing");
        stage.SetValue(mission,Enum.Parse(stage.FieldType,"Briefing"));
        var survey=typeof(Level2MissionController).GetMethod("UpdateSurveyProgress",Hidden);
        stage.SetValue(mission,Enum.Parse(stage.FieldType,"HoldAt25m"));
        survey.Invoke(mission,new object[]{75f,true,30f});
        Require(mission.CurrentSurveyDepth==25f,"Skipped 25m survey");
        foreach(float depth in new[]{25f,50f,75f})
        {
            Require(mission.CurrentSurveyDepth==depth,"Incorrect story order");
            survey.Invoke(mission,new object[]{depth,true,10f});
            survey.Invoke(mission,new object[]{depth+4,true,1f});
            survey.Invoke(mission,new object[]{depth,true,5f});
            Require(mission.CurrentSurveyDepth==depth && stage.GetValue(mission).ToString()!="ReturnToSurface","Hold did not reset outside band");
            survey.Invoke(mission,new object[]{depth,true,10f});
            var marker=GameObject.Find($"Level 2 Survey Marker {depth:0}m").GetComponent<DepthTrainingMarker>();
            Require(marker.Completed,"Depth marker not completed");
        }
        Require(stage.GetValue(mission).ToString()=="ReturnToSurface","75m did not unlock return");
        stage.SetValue(mission,Enum.Parse(stage.FieldType,"Briefing"));
        Vector3 water=Level2SinkholeBootstrap.FusedWaterCenter;
        for(int depth=1;depth<=75;depth++) for(int i=0;i<36;i++)
        {
            Vector3 direction=Quaternion.Euler(0,i*10,0)*Vector3.forward;
            bool wall=false;
            foreach(var hit in Physics.RaycastAll(water+Vector3.down*depth,direction,80,~0,QueryTriggerInteraction.Ignore))
                if(Level2SinkholeRuntimeLoader.IsVisibleCaveGround(hit.collider)) { wall=true; break; }
            Require(wall,"Underwater side leak depth="+depth+" direction="+i*10);
        }
        var step=typeof(SimpleMove).GetMethod("MoveWithGroundStop",Hidden);
        var originalPosition=player.transform.position;
        for(int angle=0;angle<360;angle+=10) foreach(float radius in new[]{12.5f,16f,20f,24f})
        {
            var p=water+Quaternion.Euler(0,angle,0)*Vector3.forward*radius;
            Require(Level2SinkholeRuntimeLoader.TryFindVisibleCaveGround(p+Vector3.up*20,30,out var ground),"Missing shore support");
            player.transform.position=ground.point+Vector3.up*(move.groundSnapOffset+.3f);
            for(int frame=0;frame<60;frame++) step.Invoke(move,new object[]{Vector3.down*2f,1f/60});
            Require(Mathf.Abs(player.transform.position.y-ground.point.y-move.groundSnapOffset)<.06f,"Character sank into shore: "+p);
        }
        player.transform.position=originalPosition;
        Vector3 spawn=player.transform.position; player.transform.position=water+Vector3.up;
        for(int i=0;i<2280;i++) step.Invoke(move,new object[]{Vector3.down,1f/30});
        Require(Mathf.Abs(player.transform.position.y-(water.y-75))<.1,"Descent blocked: "+player.transform.position);
        Capture("75m",water+Vector3.down*75,Quaternion.Euler(0,90,0));
        Capture("25m",water+Vector3.down*25,Quaternion.Euler(0,90,0));
        Capture("3m",water+Vector3.down*3,Quaternion.Euler(5,90,0));
        for(int i=0;i<2280;i++) step.Invoke(move,new object[]{Vector3.up,1f/30});
        Require(Mathf.Abs(player.transform.position.y-(water.y+1))<.1,"Ascent blocked");
        player.transform.position=spawn; move.ResetVerticalVelocity();
        Capture("spawn",Camera.main.transform.position,Camera.main.transform.rotation);
    }
    public static void ValidateLevel1Controls(SimpleMove actual)
    {
        var baselineObject=new GameObject("Control profile validation");baselineObject.SetActive(false);
        try
        {
            var baseline=baselineObject.AddComponent<SimpleMove>();baseline.ApplyLevel1ControlProfile();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Level2") baseline.ApplyLevel2Pace();
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Level3") baseline.ApplyOpenWaterCurrent();
            string[] fields={"moveSpeed","sprintMultiplier","jumpForce","gravity","maxFallSpeed","landAcceleration","landDeceleration","waterEnterDepth","waterExitHeight","surfaceFloatBand","waterEntryFallDamping","waterProbeOffset","surfaceDiveSpeed","underwaterMoveSpeed","underwaterSprintMultiplier","underwaterVerticalSpeed","verticalThrustAcceleration","underwaterGravity","surfaceBuoyancyAcceleration","deepBuoyancyMultiplier","verticalWaterDrag","maxUnderwaterRiseSpeed","maxUnderwaterSinkSpeed","underwaterAcceleration","underwaterDeceleration","passiveDriftSpeed","passiveVerticalDriftSpeed","passiveDriftAcceleration","passiveDriftInterval","passiveVerticalCorrection","densityStartDepth","maxDensityDepth","deepMoveSpeedMultiplier","deepVerticalSpeedMultiplier","deepDragMultiplier","upwardSurfaceBrake","groundCheckDistance","groundSnapOffset","bodyRadius","wallCheckHeight","wallSkin"};
            foreach(string name in fields) {var f=typeof(SimpleMove).GetField(name);Require(Equals(f.GetValue(actual),f.GetValue(baseline)),"Level1 control mismatch: "+name);}
            var look=Camera.main.GetComponent<MouseLook>();Require(look!=null && look.mouseSensitivity==320f,"Mouse control mismatch");
        }
        finally {UnityEngine.Object.DestroyImmediate(baselineObject);}
    }
    static void Capture(string label,Vector3 position,Quaternion rotation)
    {
        var camera=Camera.main; if(camera==null)return;
        var p=camera.transform.position;var q=camera.transform.rotation;var old=camera.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(1280,800,24);var shot=new Texture2D(1280,800,TextureFormat.RGB24,false);
        camera.transform.SetPositionAndRotation(position,rotation);
        var movement=camera.GetComponentInParent<SimpleMove>();
        var wetField=typeof(SimpleMove).GetField("isUnderwater",Hidden);
        bool wasWet=movement.IsUnderwater;
        wetField.SetValue(movement,position.y<movement.waterSurfaceY);
        var atmosphere=UnityEngine.Object.FindFirstObjectByType<CenoteWaterAtmosphere>();
        Require(atmosphere!=null,"Missing depth lighting");
        var lighting=typeof(CenoteWaterAtmosphere).GetMethod("LateUpdate",Hidden);
        lighting.Invoke(atmosphere,null);
        if(position.y<movement.waterSurfaceY)
            Require(Mathf.Abs(RenderSettings.fogDensity-CenoteWaterAtmosphere.FogAtDepth(movement.waterSurfaceY-position.y))<.001f,"Depth fog not applied");
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        shot.ReadPixels(new Rect(0,0,1280,800),0,0);shot.Apply();File.WriteAllBytes("Artifacts/Level2Blender/unity_"+label+".png",shot.EncodeToPNG());
        camera.transform.SetPositionAndRotation(p,q);camera.targetTexture=old;RenderTexture.active=active;
        wetField.SetValue(movement,wasWet);lighting.Invoke(atmosphere,null);
        UnityEngine.Object.DestroyImmediate(shot);UnityEngine.Object.DestroyImmediate(rt);
    }
    static void Finish(string result)
    {
        File.WriteAllText("Artifacts/Level2Blender/unity_validation.txt",result);Debug.Log(result);
        SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;
    }
}
