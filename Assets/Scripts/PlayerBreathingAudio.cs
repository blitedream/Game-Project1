using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerBreathingAudio : MonoBehaviour
{
    AudioSource natural, equipped;
    SimpleMove movement;
    AudioClip normalizedBreath;
    bool touchingWater;

    void Start()
    {
        movement=GetComponent<SimpleMove>();
        natural=CreateLoop("Unequipped");
        equipped=CreateLoop("Equipped");
    }

    AudioSource CreateLoop(string name)
    {
        var child=new GameObject(name+" Breathing");child.transform.SetParent(transform,false);
        var source=child.AddComponent<AudioSource>();
        source.clip=Resources.Load<AudioClip>("Audio/Breathing/"+name);
        if(name=="Unequipped" && source.clip!=null)
        {
            var original=source.clip;
            original.LoadAudioData();
            var samples=new float[original.samples*original.channels];
            if(original.GetData(samples,0))
            {
                float peak=0;double energy=0;
                foreach(float value in samples){peak=Mathf.Max(peak,Mathf.Abs(value));energy+=value*value;}
                float rms=(float)System.Math.Sqrt(energy/samples.Length);
                float gain=Mathf.Min(40f,.08f/Mathf.Max(rms,.0001f),.9f/Mathf.Max(peak,.0001f));
                for(int i=0;i<samples.Length;i++)samples[i]*=gain;
                normalizedBreath=AudioClip.Create("Natural breathing - calibrated",original.samples,original.channels,original.frequency,false);
                normalizedBreath.SetData(samples,0);
                source.clip=normalizedBreath;
            }
        }
        source.playOnAwake=false;source.loop=true;source.spatialBlend=0;
        source.volume=0;source.priority=100;
        if(source.clip!=null)source.Play();
        else Debug.LogError("Missing breathing clip: "+name);
        return source;
    }

    void Update()
    {
        if(natural==null || equipped==null)return;
        bool active=Time.timeScale>0 && !SceneTransitionManager.IsTransitioning && movement!=null && movement.EnvironmentReady;
        // Feet touching the authored water surface trigger the water sound,
        // independently of equipment or the camera's submersion depth.
        if(movement!=null && movement.EnvironmentReady)
        {
            float contactY=transform.position.y-movement.waterProbeOffset;
            touchingWater=movement.IsInsideConfiguredWaterVolume &&
                contactY<=movement.waterSurfaceY+(touchingWater?.12f:0f);
        }
        natural.volume=Mathf.MoveTowards(natural.volume,active&&!touchingWater?.4f:0,Time.unscaledDeltaTime*.5f);
        equipped.volume=Mathf.MoveTowards(equipped.volume,active&&touchingWater?.19f:0,Time.unscaledDeltaTime*.25f);
    }

    void OnDisable()
    {
        if(natural!=null)natural.volume=0;
        if(equipped!=null)equipped.volume=0;
    }
    void OnDestroy(){if(normalizedBreath!=null)Destroy(normalizedBreath);}
}
