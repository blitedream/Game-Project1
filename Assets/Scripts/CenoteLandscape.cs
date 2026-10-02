using UnityEngine;
using UnityEngine.Rendering;

// Keep the imported cave intact. Only invisible containment and lighting are added.
public static class CenoteLandscape
{
    public static void Build(Transform parent, Bounds scan)
    {
        Level2SinkholeRuntimeLoader.CreateMapPerimeter(parent, scan);
        ConfigureSky();
    }

    public static void ConfigureSky()
    {
        Shader shader=Shader.Find("GP1/Cenote Sky");
        if(shader!=null)RenderSettings.skybox=new Material(shader);
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.58f,.7f,.8f);RenderSettings.ambientEquatorColor=new Color(.38f,.43f,.36f);RenderSettings.ambientGroundColor=new Color(.18f,.2f,.16f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogColor=new Color(.62f,.72f,.76f);RenderSettings.fogDensity=.0018f;
        foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Directional){light.intensity=1.25f;light.color=new Color(1f,.94f,.82f);RenderSettings.sun=light;break;}
        if(Application.isPlaying && Object.FindFirstObjectByType<CenoteWaterAtmosphere>()==null)new GameObject("Cenote water atmosphere").AddComponent<CenoteWaterAtmosphere>();
    }

}

public sealed class CenoteWaterAtmosphere:MonoBehaviour
{
    Camera targetCamera;
    Light[] suns;
    float[] sunlight;
    Color sky, equator, ground, ambient, fog, background;
    float fogDensity, reflection;
    bool fogEnabled, initialized;
    FogMode fogMode;
    CameraClearFlags clearFlags;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnBootstrapSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnBootstrapSceneLoaded;
    }

    private static void OnBootstrapSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        Bootstrap();
    }

    private static void Bootstrap()
    {
        string scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if((scene=="Level1" || scene=="Level2" || scene=="Level3") && Object.FindFirstObjectByType<CenoteWaterAtmosphere>()==null)
            new GameObject("Depth lighting").AddComponent<CenoteWaterAtmosphere>();
    }

    public static float LightAtDepth(float depth) => Mathf.Exp(-Mathf.Max(0,depth)*.045f);
    public static float FogAtDepth(float depth) => Mathf.Lerp(.025f,.13f,1f-Mathf.Exp(-Mathf.Max(0,depth)/35f));

    void LateUpdate()
    {
        Camera camera=Camera.main;if(camera==null)return;
        var movement=camera.GetComponentInParent<SimpleMove>();
        if(movement==null || !movement.EnvironmentReady)return;
        if(!initialized)
        {
            targetCamera=camera; clearFlags=camera.clearFlags; background=camera.backgroundColor;
            sky=RenderSettings.ambientSkyColor;equator=RenderSettings.ambientEquatorColor;
            ground=RenderSettings.ambientGroundColor;ambient=RenderSettings.ambientLight;
            fog=RenderSettings.fogColor;fogDensity=RenderSettings.fogDensity;fogEnabled=RenderSettings.fog;
            fogMode=RenderSettings.fogMode;reflection=RenderSettings.reflectionIntensity;
            suns=System.Array.FindAll(Object.FindObjectsByType<Light>(FindObjectsSortMode.None),l=>l.type==LightType.Directional);
            sunlight=new float[suns.Length];for(int i=0;i<suns.Length;i++)sunlight[i]=suns[i].intensity;
            initialized=true;
        }
        float depth=movement.IsUnderwater?Mathf.Max(0,movement.waterSurfaceY-camera.transform.position.y):0;
        float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01(depth/.6f));
        float light=LightAtDepth(depth);
        Color waterFog=Color.Lerp(new Color(.025f,.19f,.23f),new Color(.001f,.004f,.008f),1-light);
        RenderSettings.fog=blend>0 || fogEnabled;
        RenderSettings.fogMode=blend>0?FogMode.Exponential:fogMode;
        RenderSettings.fogColor=Color.Lerp(fog,waterFog,blend);
        RenderSettings.fogDensity=Mathf.Lerp(fogDensity,FogAtDepth(depth),blend);
        RenderSettings.ambientSkyColor=sky*light;RenderSettings.ambientEquatorColor=equator*light;
        RenderSettings.ambientGroundColor=ground*light;RenderSettings.ambientLight=ambient*light;
        RenderSettings.reflectionIntensity=reflection*light;
        for(int i=0;i<suns.Length;i++)if(suns[i]!=null)suns[i].intensity=sunlight[i]*light;
        camera.clearFlags=depth>.05f?CameraClearFlags.SolidColor:clearFlags;
        camera.backgroundColor=depth>.05f?RenderSettings.fogColor:background;
    }

    void OnDisable()
    {
        if(!initialized)return;
        RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;
        RenderSettings.ambientGroundColor=ground;RenderSettings.ambientLight=ambient;
        RenderSettings.reflectionIntensity=reflection;RenderSettings.fog=fogEnabled;
        RenderSettings.fogColor=fog;RenderSettings.fogDensity=fogDensity;RenderSettings.fogMode=fogMode;
        for(int i=0;i<suns.Length;i++)if(suns[i]!=null)suns[i].intensity=sunlight[i];
        if(targetCamera!=null){targetCamera.clearFlags=clearFlags;targetCamera.backgroundColor=background;}
        initialized=false;
    }
}
