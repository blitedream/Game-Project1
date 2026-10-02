using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Level1MissionController : MonoBehaviour
{
    private enum Stage
    {
        Briefing,
        TalkToInstructor,
        EquipGear,
        EnterWater,
        HoldAt20m,
        HoldAt40m,
        TouchBottom60m,
        ExitPool,
        FinalInstructorTalk,
        Complete
    }

    private Stage stage = Stage.Briefing;
    private Transform player;
    private SimpleMove movement;
    private PlayerGearState gear;
    private Vector3 safeExitPosition;
    private float depthHoldTime;
    private float currentDepth;
    private bool insideDepthBand;
    private float messageUntil;
    private string message;
    private Level1CoachDialogue instructorDialogue;

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
        if (SceneManager.GetActiveScene().name != "Level1" || FindFirstObjectByType<Level1MissionController>() != null)
            return;

        new GameObject("Level 1 Mission Flow").AddComponent<Level1MissionController>();
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
        {
            Debug.LogError("Level 1 mission could not find the player.");
            enabled = false;
            return;
        }

        player = playerObject.transform;
        movement = playerObject.GetComponent<SimpleMove>();
        gear = playerObject.GetComponent<PlayerGearState>();
        player.position = Level1PoolExtension.PlayerDeckSpawn;
        movement?.ResetVerticalVelocity();
        safeExitPosition = player.position;

        if (movement != null)
            movement.ApplyLevel1ControlProfile();

        MouseLook look = Camera.main != null ? Camera.main.GetComponent<MouseLook>() : null;
        if (look != null)
            look.ApplyLevel1ControlProfile();

        GameObject oldIntro = GameObject.Find("IntroText");
        if (oldIntro != null)
            oldIntro.SetActive(false);

        if (movement != null)
            movement.enabled = false;

        instructorDialogue = FindFirstObjectByType<Level1CoachDialogue>();
    }

    private void Update()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        if (stage <= Stage.EquipGear && player != null && player.position.y < -0.2f)
        {
            player.position = safeExitPosition;
            movement?.ResetVerticalVelocity();
        }

        if (stage == Stage.Briefing)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                HeartRateRuntime.Instance?.BeginMonitoring();
                stage = Stage.TalkToInstructor;
                if (movement != null)
                    movement.enabled = true;
                ShowMessage("Training started. Speak to the diving coach across the pool.");
            }
            return;
        }

        if (stage == Stage.TalkToInstructor)
        {
            if (instructorDialogue == null)
                instructorDialogue = FindFirstObjectByType<Level1CoachDialogue>();
            if (instructorDialogue != null && instructorDialogue.Completed)
            {
                stage = Stage.EquipGear;
                ShowMessage("Briefing complete. Collect and inspect the diving equipment.");
            }
            return;
        }

        if (stage == Stage.EquipGear && gear != null && gear.hasDivingGear)
        {
            stage = Stage.EnterWater;
            LevelRunManager.Current?.StartRun();
            ShowMessage("Equipment secured. Proceed to the pool.");
        }

        if (movement == null)
            return;

        if (stage == Stage.EnterWater && movement.IsUnderwater)
        {
            stage = Stage.HoldAt20m;
            depthHoldTime = 0f;
            ShowMessage("Descend to 20 m and hold your depth for 30 seconds. C descend / SPACE ascend.");
        }

        if (stage == Stage.HoldAt20m)
        {
            UpdateDepthHold(20f, 30f, Stage.HoldAt40m,
                "20 m stability check complete. Descend to 40 m and hold for 60 seconds.");
        }

        if (stage == Stage.HoldAt40m)
        {
            UpdateDepthHold(40f, 60f, Stage.TouchBottom60m,
                "40 m stability check complete. Continue to 60 m and make contact with the pool bottom.");
        }

        UpdateCurrentDepth();

        if (stage == Stage.TouchBottom60m && movement.IsUnderwater &&
            currentDepth >= 58.5f && movement.IsGroundedNow)
        {
            CompleteDepthMarker(60f);
            stage = Stage.ExitPool;
            ShowMessage("Bottom contact confirmed. Ascend to the surface, swim to the edge and get onto the deck.");
        }

        if (stage == Stage.ExitPool && IsPlayerOnDeck())
        {
            stage = Stage.FinalInstructorTalk;
            if (instructorDialogue == null)
                instructorDialogue = FindFirstObjectByType<Level1CoachDialogue>();
            instructorDialogue?.BeginFinalDebrief();
            ShowMessage("Return to the diving coach for your final assessment.");
        }

        if (stage == Stage.FinalInstructorTalk)
        {
            if (instructorDialogue == null)
            {
                instructorDialogue = FindFirstObjectByType<Level1CoachDialogue>();
                instructorDialogue?.BeginFinalDebrief();
            }

            if (instructorDialogue != null && instructorDialogue.FinalCompleted)
            {
                stage = Stage.Complete;
                LevelRunManager.Current?.CompleteLevel();
            }
        }
    }

    private void UpdateCurrentDepth()
    {
        if (player == null || movement == null)
            return;

        float probeY = player.position.y - movement.waterProbeOffset;
        currentDepth = Mathf.Max(0f, movement.waterSurfaceY - probeY);
    }

    private bool IsPlayerOnDeck()
    {
        if (movement == null || movement.IsUnderwater || player.position.y < Level1PoolExtension.WaterSurfaceY + 0.45f)
            return false;

        Vector3 offset = player.position - Level1PoolExtension.PoolCenter;
        bool outsideOpening = Mathf.Abs(offset.x) > Level1PoolExtension.PoolWidth * 0.5f + 0.1f ||
                              Mathf.Abs(offset.z) > Level1PoolExtension.PoolLength * 0.5f + 0.1f;
        return outsideOpening && movement.IsGroundedNow;
    }

    private void UpdateDepthHold(float targetDepth, float requiredSeconds, Stage nextStage, string completionMessage)
    {
        UpdateCurrentDepth();
        insideDepthBand = movement.IsUnderwater && Mathf.Abs(currentDepth - targetDepth) <= 1.25f;

        if (insideDepthBand)
            depthHoldTime += Time.deltaTime;
        else
            depthHoldTime = 0f;

        if (depthHoldTime < requiredSeconds)
            return;

        CompleteDepthMarker(targetDepth);
        stage = nextStage;
        depthHoldTime = 0f;
        insideDepthBand = false;
        ShowMessage(completionMessage);
    }

    private static void CompleteDepthMarker(float targetDepth)
    {
        bool matched = false;
        foreach (DepthTrainingMarker marker in FindObjectsByType<DepthTrainingMarker>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool depthMatches = Mathf.Abs(marker.Depth - targetDepth) <= 0.1f;
            bool nameMatches = marker.name.IndexOf($"{targetDepth:0}m", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (depthMatches || nameMatches)
            {
                marker.CompleteObjective();
                matched = true;
            }
        }

        if (!matched)
            Debug.LogWarning($"No depth marker could be found for the completed {targetDepth:0} m task.");
    }

    private void ShowMessage(string value)
    {
        message = value;
        messageUntil = Time.unscaledTime + 3.5f;
    }

    private void OnGUI()
    {
        if (UnderwaterPanoramaMode.IsActive)
            return;

        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
        title.normal.textColor = Color.white;

        GUIStyle body = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            wordWrap = true
        };
        body.normal.textColor = new Color(0.78f, 0.93f, 1f);

        if (stage == Stage.Briefing)
        {
            GUI.Box(new Rect(16f, 54f, 370f, 94f), GUIContent.none);
            GUI.Label(new Rect(28f, 64f, 344f, 26f), "LEVEL 01  /  TRAINING POOL", title);
            GUI.Label(new Rect(28f, 91f, 344f, 55f),
                "BASIC DIVE TRAINING\nDepth control: 20 m / 40 m / 60 m bottom\nPress ENTER to begin", body);
            return;
        }

        GUI.Box(new Rect(16f, 54f, 425f, 288f), GUIContent.none);
        GUI.Label(new Rect(28f, 64f, 344f, 26f), "LEVEL 01  /  TRAINING POOL", title);

        float lineY = 96f;
        DrawTaskLine(new Rect(28f, lineY, 380f, 22f), "Talk to the diving coach", Stage.TalkToInstructor, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 380f, 22f), "Equip diving gear", Stage.EquipGear, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 380f, 22f), "Enter the training pool", Stage.EnterWater, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 380f, 22f),
            $"Hold at 20 m  {GetHoldProgress(Stage.HoldAt20m, 30f)}", Stage.HoldAt20m, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 380f, 22f),
            $"Hold at 40 m  {GetHoldProgress(Stage.HoldAt40m, 60f)}", Stage.HoldAt40m, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 390f, 22f), "Touch the 60 m pool bottom", Stage.TouchBottom60m, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 390f, 22f), "Ascend, surface and get onto the deck", Stage.ExitPool, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 390f, 22f), "Report back to the diving coach", Stage.FinalInstructorTalk, body);

        if (stage == Stage.HoldAt20m || stage == Stage.HoldAt40m ||
            stage == Stage.TouchBottom60m || stage == Stage.ExitPool)
        {
            string bandState = stage == Stage.TouchBottom60m ? "FIND BOTTOM" :
                stage == Stage.ExitPool ? "ASCEND AND EXIT" :
                insideDepthBand ? "HOLDING" : "ADJUST DEPTH";
            GUI.Label(new Rect(28f, 310f, 390f, 24f),
                $"DEPTH {currentDepth:0.0} m   {bandState}   C DOWN / SPACE UP", body);
        }

        if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            GUI.Label(new Rect(28f, 350f, 760f, 28f), message, body);
    }

    private void DrawTaskLine(Rect rect, string label, Stage taskStage, GUIStyle baseStyle)
    {
        bool completed = stage > taskStage;
        bool active = stage == taskStage;
        GUIStyle style = new GUIStyle(baseStyle);
        style.normal.textColor = completed
            ? new Color(0.35f, 1f, 0.55f)
            : active ? new Color(0.2f, 0.9f, 1f) : new Color(0.58f, 0.65f, 0.68f);

        string prefix = completed ? "✓" : active ? "▶" : "○";
        GUI.Label(rect, $"{prefix}  {label}", style);
    }

    private string GetHoldProgress(Stage taskStage, float requiredSeconds)
    {
        if (stage > taskStage)
            return "DONE";
        if (stage < taskStage)
            return $"0/{requiredSeconds:0}s";
        return $"{Mathf.Min(depthHoldTime, requiredSeconds):0.0}/{requiredSeconds:0}s";
    }
}
