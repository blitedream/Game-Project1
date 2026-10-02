using UnityEngine;

public sealed class Level3MissionController : MonoBehaviour
{
    private enum Stage
    {
        Briefing,
        TalkToCoach,
        EquipRecoveryGear,
        EnterWater,
        DescendToSearchDepth,
        LocateDiver,
        SecureBody,
        ReturnToSurface,
        ReturnToCoach,
        FinalDebrief,
        Complete
    }

    public Transform player;
    public Transform recoveryTarget;
    public SimpleMove movement;
    public Level3CoachDialogue coachDialogue;
    public Transform gearStation;
    public float targetDepthMeters = 285f;
    public float completeSurfaceDepth = 2.5f;
    public float waterSurfaceY;
    public Vector3 startingPosition;

    private Stage stage = Stage.Briefing;
    private bool bodyRecovered;
    private float currentDepth;
    private float messageUntil;
    private string message;
    private PlayerGearState gear;

    public bool BodyRecovered => bodyRecovered;
    public bool MissionComplete => stage == Stage.Complete;
    public bool CanRecoverBody => stage == Stage.SecureBody;

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }

        if (player == null)
        {
            Debug.LogError("Level 3 mission could not find the player.");
            enabled = false;
            return;
        }

        movement ??= player.GetComponent<SimpleMove>();
        gear = player.GetComponent<PlayerGearState>();
        if (startingPosition == Vector3.zero)
            startingPosition = player.position;
    }

    private void Update()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        if (player == null || (movement != null && !movement.EnvironmentReady))
            return;

        UpdateDepth();

        if (gear != null && !gear.hasDivingGear && movement != null &&
            movement.IsInsideConfiguredWaterVolume && !movement.IsGroundedNow &&
            player.position.y < waterSurfaceY - 0.5f)
        {
            player.position = startingPosition;
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
                ShowMessage("Meet your former coach and review the joint recovery plan.");
            }
            return;
        }

        if (stage == Stage.TalkToCoach)
        {
            coachDialogue ??= FindFirstObjectByType<Level3CoachDialogue>();
            if (coachDialogue != null && coachDialogue.InitialCompleted)
            {
                stage = Stage.EquipRecoveryGear;
                ShowMessage("Briefing complete. Inspect and equip the recovery diving rig.");
            }
            return;
        }

        if (stage == Stage.EquipRecoveryGear && gear != null && gear.hasDivingGear)
        {
            stage = Stage.EnterWater;
            LevelRunManager.Current?.StartRun();
            ShowMessage("Recovery rig secured. Enter the water with the coach and begin the descent.");
        }

        if (movement == null)
            return;

        bool enteredWater = movement.IsUnderwater ||
            (currentDepth >= 0.2f && movement.ConfirmWaterEntry());
        if (stage == Stage.EnterWater && enteredWater)
        {
            stage = Stage.DescendToSearchDepth;
            ShowMessage("Water entry confirmed. Descend along the cave route toward the last known position.");
        }

        float searchDepth = Mathf.Max(20f, targetDepthMeters - 25f);
        if (stage == Stage.DescendToSearchDepth && currentDepth >= searchDepth)
        {
            stage = Stage.LocateDiver;
            ShowMessage("Search depth reached. Follow the recovery beacon and locate the missing diver.");
        }

        if (stage == Stage.LocateDiver && recoveryTarget != null &&
            Vector3.Distance(player.position, recoveryTarget.position) <= 14f)
        {
            stage = Stage.SecureBody;
            ShowMessage("Missing diver located. Approach and press E to secure the recovery harness.");
        }

        if (stage == Stage.ReturnToSurface && !movement.IsUnderwater && currentDepth <= completeSurfaceDepth)
        {
            stage = Stage.ReturnToCoach;
            ShowMessage("Recovered diver is at the surface. Bring the casualty to the coach on shore.");
        }

        if (stage == Stage.ReturnToCoach && IsBackAtCoach())
        {
            stage = Stage.FinalDebrief;
            coachDialogue ??= FindFirstObjectByType<Level3CoachDialogue>();
            coachDialogue?.BeginFinalDebrief();
            ShowMessage("Complete the recovery debrief with the coach.");
        }

        if (stage == Stage.FinalDebrief)
        {
            coachDialogue ??= FindFirstObjectByType<Level3CoachDialogue>();
            if (coachDialogue != null && coachDialogue.FinalCompleted)
            {
                stage = Stage.Complete;
                LevelRunManager.Current?.CompleteLevel();
            }
        }
    }

    public void ConfigureSurfaceSetup(Vector3 spawnPosition, Vector3 waterCenter)
    {
        startingPosition = spawnPosition;
        waterSurfaceY = waterCenter.y;

        Vector3 forward = Vector3.ProjectOnPlane(waterCenter - spawnPosition, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        if (coachDialogue != null)
        {
            Vector3 coachPosition = spawnPosition + right * 3.2f + forward * 1.2f;
            coachPosition = FindGroundedPosition(coachPosition);
            coachDialogue.transform.position = coachPosition;

            // The visual loader applies its configured position in Start(). Keep
            // that value synchronized when the fused cave finishes loading so
            // it cannot move the coach back to the temporary bootstrap pose.
            Level1CoachLoader coachLoader = coachDialogue.GetComponent<Level1CoachLoader>();
            if (coachLoader != null)
            {
                coachLoader.UseOverridePosition = true;
                coachLoader.OverridePosition = coachPosition;
            }

            coachDialogue.transform.LookAt(new Vector3(spawnPosition.x, coachDialogue.transform.position.y, spawnPosition.z));
        }

        if (gearStation != null)
        {
            Vector3 gearPosition = spawnPosition - right * 3.2f + forward * 2f;
            gearStation.position = FindGroundedPosition(gearPosition) + Vector3.up * 0.08f;
            gearStation.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }

    public void RecoverBody()
    {
        if (bodyRecovered || stage != Stage.SecureBody || recoveryTarget == null)
            return;

        bodyRecovered = true;
        stage = Stage.ReturnToSurface;

        foreach (Collider targetCollider in recoveryTarget.GetComponentsInChildren<Collider>(true))
            targetCollider.enabled = false;

        recoveryTarget.SetParent(player, true);
        recoveryTarget.localPosition = new Vector3(0.75f, -0.9f, -1.45f);
        recoveryTarget.localRotation = Quaternion.Euler(18f, 90f, 78f);

        BodyRecoveryTarget interaction = recoveryTarget.GetComponent<BodyRecoveryTarget>();
        interaction?.SetRecovered();
        ShowMessage("Recovery harness secured. Tow the diver back to the surface and return to the coach.");
        Debug.Log("Level 3 recovery body secured to the player for surface extraction.");
    }

    private void UpdateDepth()
    {
        if (player == null)
            return;
        float probeOffset = movement != null ? movement.waterProbeOffset : 0f;
        currentDepth = Mathf.Max(0f, waterSurfaceY - (player.position.y - probeOffset));
    }

    private bool IsBackAtCoach()
    {
        if (coachDialogue == null || movement == null || movement.IsUnderwater)
            return false;
        return Vector3.Distance(player.position, coachDialogue.transform.position) <= 5.5f;
    }

    private static Vector3 FindGroundedPosition(Vector3 position)
    {
        if (Level2SinkholeRuntimeLoader.TryFindVisibleCaveGround(position + Vector3.up * 12f, 30f, out RaycastHit hit))
            position.y = hit.point.y;
        return position;
    }

    private void ShowMessage(string value)
    {
        message = value;
        messageUntil = Time.unscaledTime + 4.5f;
    }

    private void OnGUI()
    {
        if (UnderwaterPanoramaMode.IsActive || player == null)
            return;

        GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        title.normal.textColor = Color.white;
        GUIStyle body = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        body.normal.textColor = new Color(0.78f, 0.93f, 1f);

        if (movement != null && !movement.EnvironmentReady)
        {
            GUI.Box(new Rect(16f, 54f, 410f, 60f), "Loading cave and shore...");
            return;
        }

        if (stage == Stage.Briefing)
        {
            GUI.Box(new Rect(16f, 54f, 410f, 128f), GUIContent.none);
            GUI.Label(new Rect(28f, 64f, 380f, 26f), "LEVEL 03  /  ZACATON RECOVERY", title);
            GUI.Label(new Rect(28f, 91f, 380f, 82f),
                "ONE YEAR LATER\nJoint recovery operation / locate missing diver / return casualty\nPress ENTER to begin", body);
            return;
        }

        GUI.Box(new Rect(16f, 54f, 440f, 315f), GUIContent.none);
        GUI.Label(new Rect(28f, 64f, 400f, 26f), "LEVEL 03  /  ZACATON RECOVERY", title);
        float y = 96f;
        DrawTaskLine(new Rect(28f, y, 400f, 22f), "Review the recovery plan with the coach", Stage.TalkToCoach, body); y += 25f;
        DrawTaskLine(new Rect(28f, y, 400f, 22f), "Equip the recovery diving rig", Stage.EquipRecoveryGear, body); y += 25f;
        DrawTaskLine(new Rect(28f, y, 400f, 22f), "Enter the water with the coach", Stage.EnterWater, body); y += 25f;
        DrawTaskLine(new Rect(28f, y, 400f, 22f), "Descend to the search depth", Stage.DescendToSearchDepth, body); y += 25f;
        DrawTaskLine(new Rect(28f, y, 400f, 22f), "Locate the missing diver", Stage.LocateDiver, body); y += 25f;
        DrawTaskLine(new Rect(28f, y, 400f, 22f), "Secure the recovery harness [E]", Stage.SecureBody, body); y += 25f;
        DrawTaskLine(new Rect(28f, y, 400f, 22f), "Tow the recovered diver to the surface", Stage.ReturnToSurface, body); y += 25f;
        DrawTaskLine(new Rect(28f, y, 400f, 22f), "Return the casualty to the coach", Stage.ReturnToCoach, body); y += 25f;
        DrawTaskLine(new Rect(28f, y, 400f, 22f), "Complete the recovery debrief", Stage.FinalDebrief, body);

        if (stage >= Stage.EnterWater && stage <= Stage.ReturnToSurface)
        {
            string state = stage == Stage.ReturnToSurface ? "ASCEND WITH CASUALTY" : "SEARCH AND DESCEND";
            GUI.Label(new Rect(28f, 340f, 410f, 24f),
                $"DEPTH {currentDepth:0.0} m / {targetDepthMeters:0} m   {state}", body);
        }

        if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            GUI.Label(new Rect(28f, 389f, Mathf.Min(780f, Screen.width - 48f), 36f), message, body);
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
}

public sealed class Level3CoachDialogue : MonoBehaviour
{
    public bool InitialCompleted { get; private set; }
    public bool FinalCompleted { get; private set; }

    private readonly string[] initialLines =
    {
        "Coach: It has been over a year since Kelkaya. Your control and cave judgement have improved enough for this operation.",
        "Coach: A diver is missing in the deepest accessible section. This is a recovery mission, not a speed dive.",
        "Coach: We descend as a team. Keep the exit route in mind and correct the changing current smoothly.",
        "Coach: Locate the diver, secure the recovery harness, then tow the casualty back to the surface with me.",
        "Coach: Check the recovery rig on shore before entering. If conditions deteriorate, the return route takes priority."
    };

    private readonly string[] finalLines =
    {
        "Coach: The casualty is back at the surface. You kept control of the recovery load and protected the exit route.",
        "Coach: This was the work of a trained rescue diver, not the student who first entered the pool.",
        "Coach: Recovery operation complete. Record the time and close the mission."
    };

    private Transform player;
    private SimpleMove movement;
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
    }

    private void Update()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        if (player == null || UnderwaterPanoramaMode.IsActive ||
            (!finalDebriefAvailable && InitialCompleted) || (finalDebriefAvailable && FinalCompleted))
            return;

        bool near = Vector3.Distance(player.position, transform.position) <= 4.8f;
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
        if (player == null || UnderwaterPanoramaMode.IsActive ||
            (!finalDebriefAvailable && InitialCompleted) || (finalDebriefAvailable && FinalCompleted))
            return;

        bool near = Vector3.Distance(player.position, transform.position) <= 4.8f;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        style.normal.textColor = Color.white;

        if (!talking && near)
            GUI.Label(new Rect((Screen.width - 380f) * 0.5f, Screen.height - 86f, 380f, 28f),
                finalDebriefAvailable ? "E  COMPLETE RECOVERY DEBRIEF" : "E  TALK TO DIVING COACH", style);
        if (!talking)
            return;

        float width = Mathf.Min(760f, Screen.width - 60f);
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height - 150f;
        GUI.Box(new Rect(x, y, width, 112f), GUIContent.none);
        GUI.Label(new Rect(x + 22f, y + 18f, width - 44f, 58f),
            ActiveLines[Mathf.Clamp(lineIndex, 0, ActiveLines.Length - 1)], style);
        GUI.Label(new Rect(x + 22f, y + 80f, width - 44f, 24f), "E  CONTINUE", style);
    }
}
