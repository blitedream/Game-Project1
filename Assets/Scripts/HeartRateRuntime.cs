using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-500)]
public sealed class HeartRateRuntime : MonoBehaviour
{
    // Temporarily disabled; retain the connection and rule implementation for later.
    public static bool GameplayRulesEnabled => false;
    [System.Serializable] sealed class Settings { public string apiKey; public string deviceId; }
    public static HeartRateRuntime Instance { get; private set; }
    public static bool BlocksGameplay => GameplayRulesEnabled && Instance != null && (Instance.NeedsConnection || (LevelRunManager.Current != null && LevelRunManager.Current.HasFailed));
    public bool NeedsConnection => GameplayRulesEnabled && !accepted;
    public bool Monitoring { get; private set; }
    public readonly HeartRateChangeDetector Detector = new HeartRateChangeDetector();
    bool accepted, ownsPause;
    int readyReadings;
    float lastReading=-1;
    string device="", scene="";
    HyperateManager manager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic(){Instance=null;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        Application.runInBackground=true;
        var manager=HyperateManager.Instance;
        if(manager==null)
        {
            var root=new GameObject("Shared Heart Rate Connection");
            manager=root.AddComponent<HyperateManager>();
            var config=Resources.Load<TextAsset>("HeartRate/Connection");
            if(config!=null){var settings=JsonUtility.FromJson<Settings>(config.text);manager.apiKey=settings.apiKey;manager.deviceId=settings.deviceId;}
        }
        if(manager.GetComponent<HeartRateRuntime>()==null)manager.gameObject.AddComponent<HeartRateRuntime>();
    }
    void Awake(){Instance=this;manager=GetComponent<HyperateManager>();manager.ReadingReceived+=OnReading;}
    void Start(){device=manager.deviceId;}
    void OnDestroy(){if(manager!=null)manager.ReadingReceived-=OnReading;if(Instance==this)Instance=null;if(ownsPause)Time.timeScale=1;}
    public void BeginMonitoring(){Monitoring=GameplayRulesEnabled;Detector.Reset();}
    void OnReading(int bpm,float time)
    {
        if(!GameplayRulesEnabled){Detector.Reset();return;}
        if(lastReading<0 || time-lastReading>5f)readyReadings=0;
        if(lastReading<0 || time-lastReading>=.5f){readyReadings++;lastReading=time;}
        if(!accepted || !Monitoring || SceneTransitionManager.IsTransitioning || Time.timeScale==0 || LevelRunManager.Current==null || LevelRunManager.Current.IsFinished){Detector.Reset();return;}
        if(Detector.Add(bpm,time))LevelRunManager.Current.FailHeartRate(Detector.LastChange);
    }
    public bool ConfirmConnection()
    {
        if(!manager.HasLiveHeartRate || readyReadings<3)return false;
        accepted=true;Detector.Reset();
        if(ownsPause){Time.timeScale=1;ownsPause=false;}
        bool map=SceneManager.GetActiveScene().name=="LevelSelect";
        Cursor.lockState=map?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=map;
        return true;
    }
    void Update()
    {
        string current=SceneManager.GetActiveScene().name;
        if(scene!=current){scene=current;Monitoring=false;Detector.Reset();}
        if(!GameplayRulesEnabled)
        {
            Monitoring=false;Detector.Reset();
            if(ownsPause){Time.timeScale=1;ownsPause=false;}
            return;
        }
        if(accepted && !manager.HasLiveHeartRate){accepted=false;readyReadings=0;Detector.Reset();}
        if(!accepted && !SceneTransitionManager.IsTransitioning && !(LevelRunManager.Current!=null && LevelRunManager.Current.IsFinished))
        {Time.timeScale=0;ownsPause=true;}
    }
    void LateUpdate()
    {
        if(BlocksGameplay){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
    void OnGUI()
    {
        if(!GameplayRulesEnabled)return;
        if(scene!="LevelSelect" && scene!="Level1" && scene!="Level2" && scene!="Level3")return;
        if(LevelRunManager.Current!=null && LevelRunManager.Current.IsFinished)return;
        if(NeedsConnection){DrawConnection();return;}
        if(UnderwaterPanoramaMode.IsActive)return;
        float width=Mathf.Min(280,Screen.width-32);
        var rect=new Rect(Screen.width-width-16,70,width,72);
        GUI.Box(rect,GUIContent.none);
        var style=new GUIStyle(GUI.skin.label){fontSize=15,alignment=TextAnchor.MiddleRight};
        style.normal.textColor=manager.HasLiveHeartRate?new Color(.5f,1f,.7f):Color.white;
        GUI.Label(new Rect(rect.x+10,rect.y+3,width-20,25),manager.HasLiveHeartRate?$"HEART RATE  {manager.currentHeartRate} BPM":"HEART RATE  --",style);
        style.fontSize=11;style.normal.textColor=new Color(.8f,.85f,.9f);
        GUI.Label(new Rect(rect.x+10,rect.y+29,width-20,22),Monitoring?"MONITORING  /  25 BPM IN 10s":manager.ConnectionStatus,style);
        if(Detector.Confirmations>0)GUI.Label(new Rect(rect.x+10,rect.y+48,width-20,20),$"RAPID CHANGE  {Detector.Confirmations}/3",style);
    }
    void DrawConnection()
    {
        GUI.depth=-1000;
        GUI.color=new Color(.015f,.035f,.045f,.97f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
        float w=Mathf.Min(520,Screen.width-24),h=340,x=(Screen.width-w)/2,y=(Screen.height-h)/2;
        GUI.Box(new Rect(x,y,w,h),GUIContent.none);
        var title=new GUIStyle(GUI.skin.label){fontSize=25,alignment=TextAnchor.MiddleCenter};
        var body=new GUIStyle(GUI.skin.label){fontSize=14,alignment=TextAnchor.MiddleCenter,wordWrap=true};
        GUI.Label(new Rect(x+16,y+16,w-32,40),"CONNECT YOUR HEART RATE",title);
        GUI.Label(new Rect(x+20,y+62,w-40,40),"Start HypeRate on your watch and keep it online.",body);
        GUI.Label(new Rect(x+24,y+109,100,28),"DEVICE ID");
        device=GUI.TextField(new Rect(x+125,y+104,w-255,30),device,32);
        if(GUI.Button(new Rect(x+w-120,y+104,96,30),"CONNECT") && !string.IsNullOrWhiteSpace(device))
        {readyReadings=0;lastReading=-1;Detector.Reset();manager.ChangeDevice(device);}
        GUI.Label(new Rect(x+20,y+150,w-40,30),manager.HasLiveHeartRate?$"{manager.currentHeartRate} BPM   /   {Mathf.Min(3,readyReadings)}/3 readings":manager.ConnectionStatus,body);
        GUI.Label(new Rect(x+24,y+188,w-48,55),"Game rule: a rise or fall of 25 BPM within 10 seconds, confirmed 3 times, ends the mission.",body);
        GUI.enabled=manager.HasLiveHeartRate && readyReadings>=3;
        if(GUI.Button(new Rect(x+40,y+260,w-80,48),Monitoring?"RESUME MISSION":"CONTINUE"))ConfirmConnection();
        GUI.enabled=true;
    }
}
