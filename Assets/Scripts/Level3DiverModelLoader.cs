using System;
using System.IO;
using GLTFast;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Level3DiverModelLoader : MonoBehaviour
{
    private const string RelativeModelPath = "Level3/ScubaDiver/scuba_diver.glb";
    private const string VisualName = "Curled Scuba Diver Visual";
    private bool loading;
    private float? floorHeight;

    public void SetFloorHeight(float height)
    {
        floorHeight = height;
        AlignToFloor();
    }

    private void AlignToFloor()
    {
        var visual = transform.Find(VisualName);
        if (!floorHeight.HasValue || visual == null) return;
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        float bottom = float.PositiveInfinity;
        foreach (var renderer in renderers) bottom = Mathf.Min(bottom, renderer.bounds.min.y);
        transform.position += Vector3.up * (floorHeight.Value + .04f - bottom);
    }

    private async void Start()
    {
        if (!Application.isPlaying || loading || transform.Find(VisualName) != null)
            return;

        loading = true;
        GameObject visual = new GameObject(VisualName);
        visual.transform.SetParent(transform, false);

        GltfAsset asset = visual.AddComponent<GltfAsset>();
        asset.LoadOnStartup = false;
        asset.SceneId = -1;
        asset.PlayAutomatically = false;

        try
        {
            bool loaded = await asset.Load(GetModelUrl());
            if (!loaded)
            {
                Debug.LogError("Failed to load the Level 3 scuba diver model.");
                DestroySceneObject(visual);
                return;
            }

            CurlMeshes(visual.transform);
            HidePlaceholderParts();
            AlignToFloor();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            DestroySceneObject(visual);
        }
        finally
        {
            loading = false;
        }
    }

    private static string GetModelUrl()
    {
        string path = Path.Combine(Application.streamingAssetsPath, RelativeModelPath);
        if (Uri.TryCreate(path, UriKind.Absolute, out Uri uri) && !string.IsNullOrEmpty(uri.Scheme))
            return uri.IsFile ? uri.AbsoluteUri : path;
        return new Uri(Path.GetFullPath(path)).AbsoluteUri;
    }

    private void CurlMeshes(Transform visualRoot)
    {
        MeshFilter[] filters = visualRoot.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter filter in filters)
        {
            Mesh source = filter.sharedMesh;
            if (source == null || !source.isReadable)
                continue;

            Mesh curled = Instantiate(source);
            curled.name = source.name + " - Curled Recovery Pose";
            Vector3[] vertices = curled.vertices;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 modelPoint = visualRoot.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                modelPoint = CurlPoint(modelPoint);
                vertices[i] = filter.transform.InverseTransformPoint(visualRoot.TransformPoint(modelPoint));
            }

            curled.vertices = vertices;
            curled.RecalculateNormals();
            curled.RecalculateTangents();
            curled.RecalculateBounds();
            filter.sharedMesh = curled;
        }
    }

    private static Vector3 CurlPoint(Vector3 point)
    {
        Vector3 result = point;

        // Head and shoulders slump forward. The source model uses Z as body height.
        float upper = Mathf.InverseLerp(0.92f, 1.72f, point.z);
        result.y -= upper * upper * 0.22f;
        result.z -= upper * upper * 0.08f;

        // Bring the T-pose arms down and inward toward the chest. Each side gets a
        // slightly different depth so the body does not look mirrored or rigid.
        if (Mathf.Abs(point.x) > 0.23f && point.z > 1.05f)
        {
            float side = Mathf.Sign(point.x);
            float extension = Mathf.Abs(point.x) - 0.23f;
            result.x = side * (0.23f + extension * 0.28f);
            result.z -= extension * (side > 0f ? 0.62f : 0.48f);
            result.y += extension * (side > 0f ? 0.34f : 0.18f);
        }

        // Fold both legs at the knees. The lower legs and fins return toward the
        // torso, producing a loose recovery/fetal pose instead of a tight ball.
        if (point.z < 0.92f && Mathf.Abs(point.x) < 0.36f)
        {
            float side = Mathf.Sign(point.x == 0f ? 1f : point.x);
            if (point.z >= 0.48f)
            {
                float length = 0.92f - point.z;
                result.z = 0.92f - length * (side > 0f ? 0.78f : 0.68f);
                result.y -= length * (side > 0f ? 0.72f : 0.58f);
            }
            else
            {
                float lowerLength = 0.48f - point.z;
                result.z = (side > 0f ? 0.577f : 0.621f) + lowerLength * (side > 0f ? 0.58f : 0.46f);
                result.y = (side > 0f ? -0.30f : -0.25f) + lowerLength * (side > 0f ? 0.38f : 0.28f);
            }
        }

        // A subtle whole-body curl and twist prevents the silhouette from lying on
        // a perfectly flat plane.
        float curl = Mathf.InverseLerp(0.25f, 1.65f, point.z);
        result.y -= Mathf.Sin(curl * Mathf.PI) * 0.07f;
        result.x += Mathf.Sin(curl * Mathf.PI) * 0.025f;
        return result;
    }

    private void HidePlaceholderParts()
    {
        foreach (Transform child in transform)
        {
            if (child.name == VisualName || child.name == "BodyRecoveryPrompt")
                continue;
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = false;
        }
    }

    private static void DestroySceneObject(UnityEngine.Object target)
    {
        if (target == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(target);
            return;
        }
#endif
        Destroy(target);
    }
}
