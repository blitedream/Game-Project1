using UnityEngine;

public sealed class Level2MissionController : MonoBehaviour
{
    private enum Stage
    {
        Briefing,
        TalkToCoach,
        EquipGear,
        EnterWater,
        Reach10m,
        HoldAt25m,
        HoldAt50m,
        HoldAt75m,
        ReturnToSurface,
        ReturnToCoach,
        FinalCoachTalk,
        Complete
    }

    public SimpleMove movement;
    public Level2CoachDialogue coachDialogue;
    public float waterSurfaceY;
    public float targetDepth = 25f;
    public float targetHoldSeconds = 15f;
    public Vector3 startingPosition;

    private Stage stage = Stage.Briefing;
    public bool HasStarted => stage != Stage.Briefing;
    public bool CanCollectGear => stage == Stage.EquipGear;
    public float CurrentSurveyDepth => stage == Stage.HoldAt50m ? 50f : stage == Stage.HoldAt75m ? 75f : 25f;
    private bool IsSurveyStage => stage >= Stage.HoldAt25m && stage <= Stage.HoldAt75m;
    private float currentDepth;
    private float holdTime;
    private bool insideDepthBand;
    private float messageUntil;
    private string message;
    private PlayerGearState gear;

    private void Start()
    {
        if (startingPosition == Vector3.zero)
            startingPosition = transform.position;
        gear = GetComponent<PlayerGearState>();
    }

    private void Update()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        UpdateCurrentDepth();

        if (gear != null && !gear.hasDivingGear && movement != null &&
            movement.IsInsideConfiguredWaterVolume && !movement.IsGroundedNow &&
            transform.position.y < waterSurfaceY - 0.5f)
        {
            transform.position = startingPosition;
            movement?.ResetVerticalVelocity();
        }

        if (stage == Stage.Briefing)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                HeartRateRuntime.Instance?.BeginMonitoring();
                stage = Stage.TalkToCoach;
                if (movement != null)
                    movement.enabled = true;
                ShowMessage("Meet the diving coach for the Kelkaya safety briefing.");
            }
            return;
        }

        if (stage == Stage.TalkToCoach)
        {
            if (coachDialogue == null)
                coachDialogue = FindFirstObjectByType<Level2CoachDialogue>();
            if (coachDialogue != null && coachDialogue.InitialCompleted)
            {
                stage = Stage.EquipGear;
                ShowMessage("Briefing complete. Collect and inspect the diving equipment on shore.");
            }
            return;
        }

        if (stage == Stage.EquipGear && gear != null && gear.hasDivingGear)
        {
            stage = Stage.EnterWater;
            LevelRunManager.Current?.StartRun();
            ShowMessage("Equipment secured. Enter the sinkhole and follow the descent checks.");
        }

        if (movement == null)
            return;

        // Confirm from the shared authored water volume as well as the regular
        // movement transition. This avoids a one-frame/order mismatch when the
        // player crosses the surface while the mission component updates.
        bool enteredSinkholeWater = movement.IsUnderwater ||
            (currentDepth >= 0.2f && movement.ConfirmWaterEntry());
        if (stage == Stage.EnterWater && enteredSinkholeWater)
        {
            stage = Stage.Reach10m;
            ShowMessage("Water entry confirmed. Descend to 10 m and stabilize before continuing.");
        }

        if (stage == Stage.Reach10m && currentDepth >= 9.5f)
        {
            stage = Stage.HoldAt25m;
            holdTime = 0f;
            ShowMessage("10 m check complete. Descend to 25 m and remain there for 15 seconds.");
        }

        UpdateSurveyProgress(currentDepth, movement.IsUnderwater, Time.deltaTime);

        if (stage == Stage.ReturnToSurface && !movement.IsUnderwater &&
            transform.position.y >= waterSurfaceY - 0.1f)
        {
            stage = Stage.ReturnToCoach;
            ShowMessage("Surface confirmed. Return to the coach on the shaded approach.");
        }

        if (stage == Stage.ReturnToCoach && IsBackAtCoach())
        {
            stage = Stage.FinalCoachTalk;
            if (coachDialogue == null)
                coachDialogue = FindFirstObjectByType<Level2CoachDialogue>();
            coachDialogue?.BeginFinalDebrief();
            ShowMessage("Report back to the coach for the dive assessment.");
        }

        if (stage == Stage.FinalCoachTalk)
        {
            if (coachDialogue == null)
            {
                coachDialogue = FindFirstObjectByType<Level2CoachDialogue>();
                coachDialogue?.BeginFinalDebrief();
            }

            if (coachDialogue != null && coachDialogue.FinalCompleted)
            {
                stage = Stage.Complete;
                LevelRunManager.Current?.CompleteLevel();
            }
        }
    }

    private void UpdateCurrentDepth()
    {
        if (movement == null)
            return;
        float probeY = transform.position.y - movement.waterProbeOffset;
        currentDepth = Mathf.Max(0f, waterSurfaceY - probeY);
    }

    private bool IsBackAtCoach()
    {
        if (movement == null || movement.IsUnderwater || !movement.IsGroundedNow)
            return false;
        Level2CoachDialogue coach = coachDialogue != null
            ? coachDialogue
            : FindFirstObjectByType<Level2CoachDialogue>();
        return coach != null && Vector3.Distance(transform.position, coach.transform.position) <= 5.2f;
    }

    private void UpdateSurveyProgress(float depth, bool underwater, float deltaTime)
    {
        if (!IsSurveyStage) return;
        targetDepth = CurrentSurveyDepth;
        insideDepthBand = underwater && Mathf.Abs(depth - targetDepth) <= 1.75f;
        holdTime = insideDepthBand ? holdTime + deltaTime : 0f;
        if (holdTime < targetHoldSeconds) return;
        CompleteSurveyMarker(targetDepth);
        holdTime = 0f;
        insideDepthBand = false;
        if (stage == Stage.HoldAt25m)
        {
            stage = Stage.HoldAt50m;
            ShowMessage("25 m: Old survey marks lead deeper. Descend to 50 m and read the abandoned expedition log.");
        }
        else if (stage == Stage.HoldAt50m)
        {
            stage = Stage.HoldAt75m;
            ShowMessage("50 m: The log records a missing diver and a final signal below. Continue to 75 m to confirm the route.");
        }
        else
        {
            stage = Stage.ReturnToSurface;
            ShowMessage("75 m: Route confirmed. The last signal lies beyond this survey. Return to the professor with the coordinates.");
        }
    }

    private static void CompleteSurveyMarker(float depth)
    {
        GameObject markerObject = GameObject.Find($"Level 2 Survey Marker {depth:0}m");
        DepthTrainingMarker marker = markerObject != null ? markerObject.GetComponent<DepthTrainingMarker>() : null;
        marker?.CompleteObjective();
    }

    private void ShowMessage(string value)
    {
        message = value;
        messageUntil = Time.unscaledTime + 9f;
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
            GUI.Box(new Rect(16f, 54f, 390f, 112f), GUIContent.none);
            GUI.Label(new Rect(28f, 64f, 360f, 26f), "LEVEL 02  /  KELKAYA SINKHOLE", title);
            GUI.Label(new Rect(28f, 91f, 360f, 70f),
                "THE LOST EXPEDITION\nBriefing / 25 m survey / 50 m expedition log / 75 m route confirmation / return\nPress ENTER to begin", body);
            return;
        }

        GUI.Box(new Rect(16f, 54f, 425f, 332f), GUIContent.none);
        GUI.Label(new Rect(28f, 64f, 380f, 26f), "LEVEL 02  /  KELKAYA SINKHOLE", title);

        float lineY = 96f;
        DrawTaskLine(new Rect(28f, lineY, 380f, 22f), "Talk to the diving coach", Stage.TalkToCoach, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 380f, 22f), "Equip diving gear on shore", Stage.EquipGear, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 380f, 22f), "Enter the sinkhole water", Stage.EnterWater, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 380f, 22f), "Stabilize at 10 m", Stage.Reach10m, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 390f, 22f),
            $"25 m: Inspect survey marks  {GetHoldProgress(Stage.HoldAt25m)}", Stage.HoldAt25m, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 390f, 22f),
            $"50 m: Read expedition log  {GetHoldProgress(Stage.HoldAt50m)}", Stage.HoldAt50m, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 390f, 22f),
            $"75 m: Confirm the route  {GetHoldProgress(Stage.HoldAt75m)}", Stage.HoldAt75m, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 390f, 22f), "Ascend and leave the water", Stage.ReturnToSurface, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 390f, 22f), "Return to the coach", Stage.ReturnToCoach, body);
        lineY += 25f;
        DrawTaskLine(new Rect(28f, lineY, 390f, 22f), "Complete the coach debrief", Stage.FinalCoachTalk, body);

        if (stage >= Stage.EnterWater && stage <= Stage.ReturnToSurface)
        {
            string state = IsSurveyStage
                ? insideDepthBand ? "HOLDING" : "ADJUST DEPTH"
                : stage == Stage.ReturnToSurface ? "ASCEND AND EXIT" : "DESCEND";
            GUI.Label(new Rect(28f, 363f, 390f, 25f),
                $"DEPTH {currentDepth:0.0} m   {state}   C DOWN / SPACE UP", body);
        }

        if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            GUI.Label(new Rect(28f, 397f, Mathf.Min(760f, Screen.width - 48f), 64f), message, body);
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

    private string GetHoldProgress(Stage surveyStage)
    {
        if (stage > surveyStage)
            return "DONE";
        if (stage < surveyStage)
            return $"0/{targetHoldSeconds:0}s";
        return $"{Mathf.Min(holdTime, targetHoldSeconds):0.0}/{targetHoldSeconds:0}s";
    }
}

public sealed class Level2CoachDialogue : MonoBehaviour
{
    public bool InitialCompleted { get; private set; }
    public bool FinalCompleted { get; private set; }

    private readonly string[] initialLines =
    {
        "Coach: This is Kelkaya. Unlike the training pool, the rock edge is irregular and visibility changes quickly.",
        "Professor: An earlier expedition left an incomplete survey here. We need to find where its route ended.",
        "Coach: Before entering, inspect and put on the diving equipment staged here on the shore.",
        "Professor: Stabilize at 10 metres. At 25 metres, inspect the survey marks. At 50 metres, read the expedition log.",
        "Coach: Natural current can push in any direction. Correct smoothly with WASD, use C to descend and Space to ascend.",
        "Professor: At 75 metres, confirm the route and last signal coordinates. Hold each survey depth for fifteen uninterrupted seconds.",
        "Professor: Seventy-five metres is our turnaround point. Bring the findings back; the recovery team will handle what lies beyond."
    };

    private readonly string[] finalLines =
    {
        "Professor: Your survey marks, expedition log and signal coordinates finally give us a continuous route.",
        "Professor: The log confirms that a diver never returned. The last signal points into the deeper cave.",
        "Professor: This survey is complete. Next we prepare a recovery operation; your findings will guide the team."
    };

    private Transform player;
    private SimpleMove movement;
    private Level2MissionController mission;
    private bool finalDebriefAvailable;
    private bool talking;
    private bool movementWasEnabled;
    private int lineIndex;
    private string[] ActiveLines => finalDebriefAvailable ? finalLines : initialLines;
    private CoachVoice voice;

    private void LateUpdate()
    {
        if (voice == null) voice = gameObject.AddComponent<CoachVoice>();
        bool audible = talking && isActiveAndEnabled && !UnderwaterPanoramaMode.IsActive;
        voice.SetLine(audible ? ActiveLines[Mathf.Clamp(lineIndex, 0, ActiveLines.Length - 1)] : null);
    }

    private void OnDisable()
    {
        if (voice != null) voice.SetLine(null);
    }


    public void BeginFinalDebrief()
    {
        if (FinalCompleted)
            return;
        finalDebriefAvailable = true;
        talking = false;
        lineIndex = 0;
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null)
            return;
        player = playerObject.transform;
        movement = playerObject.GetComponent<SimpleMove>();
        mission = playerObject.GetComponent<Level2MissionController>();
    }

    private void Update()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        if (player == null || mission == null || !mission.HasStarted || UnderwaterPanoramaMode.IsActive ||
            (!finalDebriefAvailable && InitialCompleted) || (finalDebriefAvailable && FinalCompleted))
            return;

        bool near = Vector3.Distance(player.position, transform.position) <= 3f;
        if (!talking && near && Input.GetKeyDown(KeyCode.E))
        {
            talking = true;
            lineIndex = 0;
            movementWasEnabled = movement != null && movement.enabled;
            if (movement != null)
                movement.enabled = false;
        }
        else if (talking && Input.GetKeyDown(KeyCode.E))
        {
            lineIndex++;
            if (lineIndex >= ActiveLines.Length)
            {
                talking = false;
                if (finalDebriefAvailable)
                    FinalCompleted = true;
                else
                    InitialCompleted = true;
                if (movement != null)
                    movement.enabled = movementWasEnabled;
            }
        }
    }

    private void OnGUI()
    {
        if (player == null || mission == null || !mission.HasStarted || UnderwaterPanoramaMode.IsActive ||
            (!finalDebriefAvailable && InitialCompleted) || (finalDebriefAvailable && FinalCompleted))
            return;

        bool near = Vector3.Distance(player.position, transform.position) <= 3f;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        style.normal.textColor = Color.white;

        if (!talking && near)
            GUI.Label(new Rect((Screen.width - 360f) * 0.5f, Screen.height - 86f, 360f, 28f),
                finalDebriefAvailable ? "E  REPORT BACK TO COACH" : "E  TALK TO DIVING COACH", style);
        if (!talking)
            return;

        float width = Mathf.Min(720f, Screen.width - 60f);
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height - 150f;
        GUI.Box(new Rect(x, y, width, 112f), GUIContent.none);
        GUI.Label(new Rect(x + 22f, y + 18f, width - 44f, 54f),
            ActiveLines[Mathf.Clamp(lineIndex, 0, ActiveLines.Length - 1)], style);
        GUI.Label(new Rect(x + 22f, y + 78f, width - 44f, 24f), "E  CONTINUE", style);
    }
}
