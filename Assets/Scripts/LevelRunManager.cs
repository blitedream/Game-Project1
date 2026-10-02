using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LevelRunManager : MonoBehaviour
{
    public static LevelRunManager Current { get; private set; }

    private int levelNumber;
    private float startedAt;
    private float finishTime;
    private bool complete;
    private bool running;
    public bool HasFailed { get; private set; }
    public bool IsFinished => complete || HasFailed;
    private int failedChange;
    private float elapsedTime;

    private void Update()
    {
        if(running && !IsFinished && !HeartRateRuntime.BlocksGameplay && !SceneTransitionManager.IsTransitioning)
            elapsedTime += Time.unscaledDeltaTime;
    }
    public void FailHeartRate(int change)
    {
        if(!HeartRateRuntime.GameplayRulesEnabled || IsFinished)return;
        HasFailed=true;failedChange=change;finishTime=elapsedTime;
        Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }

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
        string scene = SceneManager.GetActiveScene().name;
        if (!TryGetLevelNumber(scene, out _) || FindFirstObjectByType<LevelRunManager>() != null) return;
        new GameObject("Level Run Manager").AddComponent<LevelRunManager>();
    }

    private void Awake()
    {
        Current = this;
        TryGetLevelNumber(SceneManager.GetActiveScene().name, out levelNumber);
        // Every mission has an explicit briefing/equipment phase. Timing begins
        // only when the corresponding mission controller confirms preparation.
        running = false;
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        Time.timeScale = 1f;
    }

    public void CompleteLevel()
    {
        if (IsFinished || HeartRateRuntime.BlocksGameplay) return;
        if (!running) StartRun();
        complete = true;
        finishTime = elapsedTime;
        GameProgression.CompleteLevel(levelNumber, finishTime);
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void StartRun()
    {
        if (running || IsFinished || HeartRateRuntime.BlocksGameplay) return;
        running = true;
        startedAt = Time.realtimeSinceStartup;
    }

    private void OnGUI()
    {
        if(HasFailed)
        {
            GUI.depth=-1100;
            GUI.color=new Color(.04f,.015f,.02f,.96f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
            float fw=Mathf.Min(480,Screen.width-24),fx=(Screen.width-fw)/2,fy=(Screen.height-270)/2;
            GUI.Box(new Rect(fx,fy,fw,270),GUIContent.none);
            var fs=new GUIStyle(GUI.skin.label){fontSize=26,alignment=TextAnchor.MiddleCenter};
            GUI.Label(new Rect(fx+16,fy+22,fw-32,45),"MISSION FAILED",fs);
            fs.fontSize=16;fs.wordWrap=true;
            GUI.Label(new Rect(fx+25,fy+80,fw-50,70),$"Heart rate changed too quickly ({failedChange:+0;-0} BPM).\n25 BPM / 10 seconds / 3 confirmations",fs);
            if(GUI.Button(new Rect(fx+25,fy+190,(fw-60)/2,45),"RETRY"))SceneTransitionManager.ReloadCurrentScene();
            if(GUI.Button(new Rect(fx+35+(fw-60)/2,fy+190,(fw-60)/2,45),"MISSION MAP"))SceneTransitionManager.OpenMissionMap();
            return;
        }
        if(HeartRateRuntime.BlocksGameplay)return;
        if (UnderwaterPanoramaMode.IsActive)
            return;

        float elapsed = complete ? finishTime : elapsedTime;
        GUIStyle hud = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        hud.normal.textColor = Color.white;
        GUI.Label(new Rect(Screen.width - 205f, 12f, 180f, 26f), "TIME  " + GameProgression.FormatTime(elapsed), hud);
        if (!complete) return;

        float w = 480f, h = 390f, x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;
        GUI.Box(new Rect(x, y, w, h), GUIContent.none);
        GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title.normal.textColor = new Color(0.25f, 1f, 0.75f);
        GUIStyle center = new GUIStyle(GUI.skin.label) { fontSize = 21, alignment = TextAnchor.MiddleCenter };
        center.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 20f, y + 30f, w - 40f, 48f), "MISSION COMPLETE", title);
        GUI.Label(new Rect(x + 20f, y + 96f, w - 40f, 36f), $"LEVEL {levelNumber}  CLEAR TIME", center);
        GUI.Label(new Rect(x + 20f, y + 137f, w - 40f, 48f), GameProgression.FormatTime(finishTime), title);
        GUI.Label(new Rect(x + 20f, y + 190f, w - 40f, 32f), "BEST  " + GameProgression.FormatTime(GameProgression.GetBestTime(levelNumber)), center);

        string primaryLabel = levelNumber < 3 ? "NEXT MISSION" : "RETURN TO MISSION MAP";
        if (GUI.Button(new Rect(x + 55f, y + 245f, w - 110f, 44f), primaryLabel))
        {
            if (levelNumber < 3)
                SceneTransitionManager.LoadLevel(levelNumber + 1);
            else
                SceneTransitionManager.OpenMissionMap();
        }

        if (GUI.Button(new Rect(x + 55f, y + 298f, 175f, 42f), "REPLAY"))
            SceneTransitionManager.ReloadCurrentScene();
        if (GUI.Button(new Rect(x + 250f, y + 298f, 175f, 42f), "MISSION MAP"))
            SceneTransitionManager.OpenMissionMap();
    }

    private static bool TryGetLevelNumber(string scene, out int number)
    {
        number = scene == "Level1" ? 1 : scene == "Level2" ? 2 : scene == "Level3" ? 3 : 0;
        return number != 0;
    }
}
