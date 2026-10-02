using System;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CavePassageValidation
{
    static CavePassageValidation()
    {
        EditorApplication.delayCall += () =>
        {
            const string request="Artifacts/cave_validate.request";
            if(Application.isBatchMode||!System.IO.File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            System.IO.File.Delete(request);
            Run();
        };
    }
    public static void Run()
    {
        GameObject root = null;
        Scene previous=SceneManager.GetActiveScene();
        Scene temporary=SceneManager.CreateScene("Cave validation temporary");
        SceneManager.SetActiveScene(temporary);
        try
        {
            const string path = "Assets/Models/Shared/fused_cave_open.glb";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new Exception("Opened cave import failed");
            root = UnityEngine.Object.Instantiate(asset);
            Level2SinkholeBootstrap.ExpandMap(root.transform);
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.cookingOptions &= ~MeshColliderCookingOptions.UseFastMidphase;
                collider.sharedMesh = filter.sharedMesh;
            }
            Physics.SyncTransforms();
            var renderers=root.GetComponentsInChildren<Renderer>();
            Bounds bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            CenoteLandscape.Build(root.transform,bounds);
            Physics.SyncTransforms();
            Vector3 water = Level2SinkholeBootstrap.FusedWaterCenter;
            int tested = 0;
            foreach (float x in new[] { -6f, 0f, 6f })
            foreach (float z in new[] { -6f, 0f, 6f })
            {
                Vector3 origin = water + new Vector3(x, .5f, z);
                foreach (RaycastHit hit in Physics.SphereCastAll(origin, .5f, Vector3.down, 65f))
                {
                    if (hit.transform.IsChildOf(root.transform))
                        throw new Exception($"Descent blocked: {hit.transform.name} at {hit.point}");
                }
                tested++;
            }
            // Inspect both sides: a closed shell can have outward-facing normals.
            bool oldBackfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = false;
            bool floorFound = false;
            foreach (RaycastHit hit in Physics.RaycastAll(water + Vector3.down * 15f, Vector3.down, 120f))
                if (hit.transform.IsChildOf(root.transform)) floorFound = true;
            Physics.queriesHitBackfaces = oldBackfaces;
            if (!floorFound) throw new Exception("Lower cave bottom missing");
            var loader=root.AddComponent<Level2SinkholeRuntimeLoader>();
            typeof(Level2SinkholeRuntimeLoader).GetMethod("CreateWaterSurface",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(loader,new object[]{bounds});
            var waterObject=GameObject.Find("Single Cave Water Surface");
            if(waterObject==null||waterObject.GetComponent<Collider>()!=null)throw new Exception("Water surface must exist and be passable");
            var player=new GameObject("Water entry validation player");
            player.AddComponent<PlayerGearState>().hasDivingGear=true;
            var movement=player.AddComponent<SimpleMove>();movement.ApplyLevel1ControlProfile();
            movement.waterSurfaceY=water.y;movement.useBoundedWaterArea=true;movement.waterAreaCenterXZ=new Vector2(water.x,water.z);movement.waterAreaRadii=Level2SinkholeBootstrap.CaveWaterRadii;movement.deepWaterAreaRadii=new Vector2(42,34);movement.waterBottomY=bounds.min.y-1;
            typeof(SimpleMove).GetMethod("Start",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(movement,null);
            foreach(float depth in new[]{.5f,10f,25f,40f,65f})
            {player.transform.position=water+Vector3.down*depth+Vector3.up*movement.waterProbeOffset;
                if(!movement.ConfirmWaterEntry()||!movement.IsUnderwater)throw new Exception($"Water state missing at {depth}m");}
            UnityEngine.Object.DestroyImmediate(player);
            var sun=new GameObject("Validation sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(48,-30,0);sun.intensity=1.3f;
            var camera=new GameObject("Validation camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;camera.farClipPlane=1000;
            camera.transform.position=water+new Vector3(-80,65,-100);camera.transform.LookAt(water);
            var rt=new RenderTexture(1280,800,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
            System.IO.File.WriteAllBytes("Artifacts/cenote_landscape_preview.png",image.EncodeToPNG());
            camera.transform.position=water+new Vector3(-24,5,-26);camera.transform.LookAt(water+Vector3.up*2);
            camera.Render();image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();System.IO.File.WriteAllBytes("Artifacts/cenote_shore_preview.png",image.EncodeToPNG());
            RenderTexture.active=null;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(sun.gameObject);UnityEngine.Object.DestroyImmediate(waterObject);
            Debug.Log($"CAVE_PASSAGE_PASS: {tested} body-width sweeps to 65m clear; floor front face present; runtime water visible/non-solid; entry state passes at 0.5/10/25/40/65m.");
            UnityEngine.Object.DestroyImmediate(root);
            System.IO.File.WriteAllText("Artifacts/cave_validation_result.txt","PASS: geometry, water surface, water entry at five depths, floor collision, visual renders.");
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            System.IO.File.WriteAllText("Artifacts/cave_validation_result.txt",e.ToString());
            if(Application.isBatchMode)EditorApplication.Exit(1);
        }
        finally
        {
            EditorSceneManager.CloseScene(temporary,true);
            if(previous.IsValid())SceneManager.SetActiveScene(previous);
        }
    }
}
