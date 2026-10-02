using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class WaterDepthValidation
{
    static WaterDepthValidation(){EditorApplication.delayCall+=Validate;}
    static void Validate()
    {
        var go=new GameObject("Water validation");go.SetActive(false);
        var random=UnityEngine.Random.state;
        try
        {
            var move=go.AddComponent<SimpleMove>();move.ApplyLevel1ControlProfile();move.ApplyLevel2Pace();
            if(move.moveSpeed!=3.5f || move.passiveDriftSpeed!=1.05f)throw new Exception("Level2 pace/current mismatch");
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var timer=typeof(SimpleMove).GetField("nextPassiveDriftChange",flags);
            var target=typeof(SimpleMove).GetField("passiveDriftTarget",flags);
            var update=typeof(SimpleMove).GetMethod("UpdateUnderwaterCurrent",flags);
            var directions=new bool[8];UnityEngine.Random.InitState(3419);
            for(int i=0;i<256;i++)
            {
                timer.SetValue(move,-1f);update.Invoke(move,null);var v=(Vector3)target.GetValue(move);
                float angle=Mathf.Repeat(Mathf.Atan2(v.x,v.z)*Mathf.Rad2Deg,360);
                int direction=Mathf.RoundToInt(angle/45)%8;directions[direction]=true;
                float magnitude=new Vector2(v.x,v.z).magnitude;
                if(magnitude<.524f || magnitude>1.051f)throw new Exception("Current strength outside range");
            }
            if(Array.Exists(directions,d=>!d))throw new Exception("Missing current direction");
            float last=2,previousFog=0;
            foreach(float depth in new[]{0f,1f,5f,10f,25f,50f,75f,100f,285f})
            {
                float light=CenoteWaterAtmosphere.LightAtDepth(depth),fog=CenoteWaterAtmosphere.FogAtDepth(depth);
                if(light>=last || fog<previousFog)throw new Exception("Depth lighting not monotonic");
                last=light;previousFog=fog;
            }
            Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/water_depth_validation.txt","PASS: eight compass directions, increased current range, unchanged Level2 walking pace, monotonic depth light/fog from 0 to 285m.");
        }
        catch(Exception ex){File.WriteAllText("Artifacts/water_depth_validation.txt","FAIL: "+ex);Debug.LogException(ex);}
        finally{UnityEngine.Random.state=random;UnityEngine.Object.DestroyImmediate(go);}
    }
}
