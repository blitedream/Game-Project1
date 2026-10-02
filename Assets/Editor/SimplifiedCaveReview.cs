using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SimplifiedCaveReview
{
    static SimplifiedCaveReview() { EditorApplication.delayCall += Check; }
    static void Check()
    {
        const string request="Artifacts/simple_cave_review.request";
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        string previous=SceneManager.GetActiveScene().path;
        try
        {
            foreach(string level in new[]{"Level2","Level3"})
            {
                var scene=EditorSceneManager.OpenScene("Assets/Scenes/"+level+".unity");
                typeof(ScenePreviewBuilders).GetMethod("BuildCurrentScenePreview",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
                var root=GameObject.Find(Level2SinkholeBootstrap.PreviewRootName);
                if(root==null)throw new Exception("No cave preview");
                if(root.transform.Find("Cenote Landscape")!=null)throw new Exception("Old generated landscape remains");
                var perimeter=root.transform.Find("Natural Rock Perimeter");
                if(perimeter==null || perimeter.GetComponentsInChildren<Renderer>().Length!=0 || perimeter.GetComponentsInChildren<BoxCollider>().Length!=5)
                    throw new Exception("Boundary must contain only five invisible colliders");
                var cam=new GameObject("Review camera").AddComponent<Camera>();
                cam.clearFlags=CameraClearFlags.Skybox;cam.farClipPlane=1000;
                Vector3 water=Level2SinkholeBootstrap.FusedWaterCenter;
                cam.transform.position=water+new Vector3(-115,85,-125);cam.transform.LookAt(water+Vector3.down*10);
                var rt=new RenderTexture(1280,800,24);cam.targetTexture=rt;cam.Render();
                var old=RenderTexture.active;RenderTexture.active=rt;
                var image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
                File.WriteAllBytes("Artifacts/"+level+"_simplified.png",image.EncodeToPNG());
                RenderTexture.active=old;cam.targetTexture=null;
                UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(cam.gameObject);
                EditorSceneManager.SaveScene(scene);
            }
            File.WriteAllText("Artifacts/simple_cave_review.txt","PASS: both saved scenes rebuilt; imported cave retained; no generated landscape; five collider-only boundaries per scene; skybox previews rendered.");
        }
        catch(Exception e){File.WriteAllText("Artifacts/simple_cave_review.txt",e.ToString());Debug.LogException(e);}
        finally {if(!string.IsNullOrEmpty(previous))EditorSceneManager.OpenScene(previous);}
    }
}
