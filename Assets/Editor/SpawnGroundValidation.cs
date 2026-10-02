using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SpawnGroundValidation
{
    const string Request = "Artifacts/spawn_validate.request";
    const string Result = "Artifacts/spawn_validation_result.txt";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static SpawnGroundValidation() { EditorApplication.delayCall += CheckRequest; }
    static void CheckRequest()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Run();
    }
    [MenuItem("Tools/Validation/Validate Cave Spawn Ground")]
    public static void Run()
    {
        Scene previous = SceneManager.GetActiveScene();
        var existing = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
        var enabled = Array.FindAll(existing, c => c.enabled);
        Scene temporary = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
        try
        {
            foreach (var collider in enabled) collider.enabled = false;
            SceneManager.SetActiveScene(temporary);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Shared/fused_cave_open.glb");
            if (asset == null) throw new Exception("Cave model is missing");
            var root = UnityEngine.Object.Instantiate(asset);
            Level2SinkholeBootstrap.ExpandMap(root.transform);
            var loader = root.AddComponent<Level2SinkholeRuntimeLoader>();
            loader.AddEnvironmentColliders();
            var renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Physics.SyncTransforms();
            CenoteLandscape.Build(root.transform, bounds); Physics.SyncTransforms(); Vector3 water = Level2SinkholeBootstrap.FusedWaterCenter;
            Vector3 old = water + new Vector3(-bounds.extents.x * .32f, 0, -bounds.extents.z * .24f);
            Physics.Raycast(new Vector3(old.x, bounds.max.y + 10, old.z), Vector3.down, out var oldHit, bounds.size.y + 40);
            File.WriteAllText("Artifacts/spawn_diagnostic.txt", $"bounds={bounds}; old hit={oldHit.point}; normal={oldHit.normal}; collider={oldHit.collider}"); Level2SinkholeRuntimeLoader.GetShadowSideSpawnPose(bounds, water, out _, out var spawn);
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.AddComponent<PlayerGearState>();
            player.AddComponent<Rigidbody>();
            var move = player.AddComponent<SimpleMove>();
            move.groundLayer = ~0; move.requireVisibleCaveGround = true;
            player.transform.position = spawn;
            typeof(SimpleMove).GetMethod("Start", Private).Invoke(move, null);
            var step = typeof(SimpleMove).GetMethod("MoveWithGroundStop", Private);
            Physics.SyncTransforms();
            if (!move.IsGroundedNow) throw new Exception("Spawn is not grounded: " + spawn); if (!Level2SinkholeRuntimeLoader.TryFindVisibleCaveGround(spawn, 1.1f, out var support)) throw new Exception("No visible mesh under feet"); var mc=(MeshCollider)support.collider; var tris=mc.sharedMesh.triangles; var verts=mc.sharedMesh.vertices; var bc=support.barycentricCoordinate; var tri=support.triangleIndex*3; var renderedPoint=mc.transform.TransformPoint(verts[tris[tri]]*bc.x+verts[tris[tri+1]]*bc.y+verts[tris[tri+2]]*bc.z); File.WriteAllText("Artifacts/spawn_visible_support.txt", $"Spawn={spawn}; support={support.collider.name}; hit={support.point}; distance={support.distance}; normal={support.normal}; renderedPoint={renderedPoint}; bounds={mc.GetComponent<CaveGroundSurface>().source.bounds}; colliderBounds={mc.bounds}");
            foreach (float dt in new[] { 1f / 30f, 1f / 60f, 1f / 144f })
            {
                player.transform.position = spawn + Vector3.up * 2;
                for (int i = 0; i < 1200; i++)
                {
                    Physics.SyncTransforms();
                    step.Invoke(move, new object[] { Vector3.down * 8f, dt });
                }
                if (Mathf.Abs(player.transform.position.y - spawn.y) > .03f || !move.IsGroundedNow)
                    throw new Exception("Fall/standing regression at dt " + dt + ": " + player.transform.position);
            }
            foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                player.transform.position = spawn;
                for (int i = 0; i < 30; i++)
                {
                    Physics.SyncTransforms();
                    step.Invoke(move, new object[] { direction + Vector3.down * 8f, 1f / 60f });
                }
                if (Vector3.ProjectOnPlane(player.transform.position - spawn, Vector3.up).magnitude < .4f ||
                    Mathf.Abs(player.transform.position.y - spawn.y) > .5f)
                    throw new Exception("Spawn movement obstructed in " + direction);
            }
            player.transform.position = spawn + Vector3.up * 10;
            move.EnvironmentReady = false;
            for (int i = 0; i < 600; i++) typeof(SimpleMove).GetMethod("Update", Private).Invoke(move, null);
            if (player.transform.position != spawn + Vector3.up * 10) throw new Exception("Moved before environment was ready");
            if (Vector3.Distance(renderedPoint, support.point) > .02f) throw new Exception("Collision is offset from visible triangle"); File.WriteAllText(Result, $"PASS: shared Level2/3 spawn {spawn}; old ground normal {oldHit.normal}; grounded; falls and 1200 standing steps at 30/60/144 Hz; movement in four directions; loading gate.");
            Debug.Log(File.ReadAllText(Result));
        }
        catch (Exception e) { File.WriteAllText(Result, e.ToString()); Debug.LogException(e); }
        finally
        {
            EditorSceneManager.CloseScene(temporary, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            foreach (var collider in enabled) if (collider != null) collider.enabled = true;
            Physics.SyncTransforms();
        }
    }
}







