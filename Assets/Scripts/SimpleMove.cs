using UnityEngine;

public class SimpleMove : MonoBehaviour
{
    [Header("Surface Movement")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.6f;
    public float jumpForce = 8f;
    public float gravity = -20f;
    public float maxFallSpeed = 8f;
    public float landAcceleration = 30f;
    public float landDeceleration = 38f;

    [Header("Water Surface")]
    public float waterSurfaceY = -0.1f;
    public float waterEnterDepth = 0.25f;
    public float waterExitHeight = 0.15f;
    public float surfaceFloatBand = 0.35f;
    public float waterEntryFallDamping = 0.35f;
    public float waterProbeOffset = 1f;
    public float surfaceDiveSpeed = 1.6f;

    [Header("Bounded Water Area")]
    public bool useBoundedWaterArea;
    public Vector2 waterAreaCenterXZ;
    public Vector2 waterAreaRadii = new Vector2(20f, 20f);
    // Optional wider lower chamber; zero retains the original pool behaviour.
    public Vector2 deepWaterAreaRadii;
    public float waterBottomY = -100f;
    [Tooltip("Keeps an underwater player inside the authored water volume instead of falling through scan gaps.")]
    public bool constrainToWaterVolume;
    public float waterBoundaryInset = 0.65f;

    [Header("Underwater Movement")]
    public float underwaterMoveSpeed = 2f;
    public float underwaterSprintMultiplier = 1.4f;
    public float underwaterVerticalSpeed = 2f;
    [Tooltip("Acceleration produced while actively ascending or descending.")]
    public float verticalThrustAcceleration = 12f;
    [Tooltip("Downward acceleration that still applies underwater.")]
    public float underwaterGravity = -9.81f;
    [Tooltip("Upward buoyancy near the surface. Slightly above gravity creates positive buoyancy.")]
    public float surfaceBuoyancyAcceleration = 10.2f;
    [Range(0.1f, 1f)]
    [Tooltip("Fraction of surface buoyancy remaining at maximum depth.")]
    public float deepBuoyancyMultiplier = 0.72f;
    [Tooltip("Linear water resistance applied to vertical velocity.")]
    public float verticalWaterDrag = 0.55f;
    public float maxUnderwaterRiseSpeed = 5f;
    public float neutralBuoyancyBobFrequency = 1.1f;
    public float underwaterDrag = 6f;
    public float maxUnderwaterSinkSpeed = 2.5f;
    public float underwaterAcceleration = 10f;
    public float underwaterDeceleration = 7f;

    [Header("Changing Underwater Current")]
    [Tooltip("Maximum horizontal current speed. The current remains active while the player moves.")]
    public float passiveDriftSpeed = 0.55f;
    [Tooltip("Maximum upward/downward current speed.")]
    public float passiveVerticalDriftSpeed = 0.28f;
    [Tooltip("How quickly the water current builds toward its next direction.")]
    public float passiveDriftAcceleration = 0.35f;
    public Vector2 passiveDriftInterval = new Vector2(1.4f, 4.5f);
    public float passiveVerticalCorrection = 2.4f;

    [Header("Water Density")]
    public float densityStartDepth = 0.4f;
    public float maxDensityDepth = 5f;
    public float deepMoveSpeedMultiplier = 0.45f;
    public float deepVerticalSpeedMultiplier = 0.35f;
    public float deepDragMultiplier = 2.2f;
    public float upwardSurfaceBrake = 0.35f;

    [Header("Ground Check")]
    public float groundCheckDistance = 1.3f;
    public float groundSnapOffset = 1.05f;
    public LayerMask groundLayer;
    public bool requireVisibleCaveGround;

    [Header("Wall Collision")]
    public LayerMask wallLayer = ~0;
    public float bodyRadius = 0.45f;
    public float wallCheckHeight = 0.5f;
    public float wallSkin = 0.08f;

    private float verticalVelocity = 0f;
    private PlayerGearState gearState;
    private Rigidbody playerRigidbody;
    private bool isUnderwater;
    private float currentWaterDensity;
    private Vector3 horizontalVelocity;
    private Vector3 passiveDrift;
    private Vector3 passiveDriftTarget;
    private float nextPassiveDriftChange;

    public bool IsUnderwater => isUnderwater;
    public bool EnvironmentReady { get; set; } = true;
    public bool IsInsideConfiguredWaterVolume => IsInsideWaterArea();

    public bool ConfirmWaterEntry()
    {
        bool hasGear = gearState != null && gearState.hasDivingGear;
        if (!hasGear || !IsInsideWaterArea() || GetWaterProbeY() > waterSurfaceY + waterExitHeight)
            return false;

        isUnderwater = true;
        currentWaterDensity = CalculateWaterDensity();
        verticalVelocity = Mathf.Min(verticalVelocity, -0.15f);
        return true;
    }
    public float CurrentWaterDensity => currentWaterDensity;
    public float VerticalVelocity => verticalVelocity;
    public Vector3 CurrentWaterVelocity => passiveDrift;
    public bool IsGroundedNow => IsGrounded();

    /// <summary>
    /// Applies the movement tuning used by Level 1. Level-specific water
    /// bounds and surface heights are intentionally left untouched.
    /// </summary>
    public void ApplyLevel1ControlProfile()
    {
        moveSpeed = 7.5f;
        sprintMultiplier = 1.75f;
        jumpForce = 8f;
        gravity = -20f;
        maxFallSpeed = 8f;
        landAcceleration = 36f;
        landDeceleration = 45f;

        waterEnterDepth = 0.25f;
        waterExitHeight = 0.15f;
        surfaceFloatBand = 0.35f;
        waterEntryFallDamping = 0.35f;
        waterProbeOffset = 1f;
        surfaceDiveSpeed = 1.6f;

        underwaterMoveSpeed = 2f;
        underwaterSprintMultiplier = 1.4f;
        underwaterVerticalSpeed = 2f;
        verticalThrustAcceleration = 12f;
        underwaterGravity = -9.81f;
        surfaceBuoyancyAcceleration = 10.2f;
        deepBuoyancyMultiplier = 0.72f;
        verticalWaterDrag = 0.55f;
        maxUnderwaterRiseSpeed = 5f;
        maxUnderwaterSinkSpeed = 5.5f;
        underwaterAcceleration = 10f;
        underwaterDeceleration = 7f;

        passiveDriftSpeed = 0.7f;
        passiveVerticalDriftSpeed = 0.34f;
        passiveDriftAcceleration = 0.42f;
        passiveDriftInterval = new Vector2(1.3f, 4.2f);
        passiveVerticalCorrection = 2.4f;

        densityStartDepth = 18f;
        maxDensityDepth = 60f;
        deepMoveSpeedMultiplier = 0.72f;
        deepVerticalSpeedMultiplier = 0.68f;
        deepDragMultiplier = 2.2f;
        upwardSurfaceBrake = 0.35f;
        groundCheckDistance = 1.3f;
        groundSnapOffset = 1.05f;
        bodyRadius = .45f;
        wallCheckHeight = .5f;
        wallSkin = .08f;
    }

    public void ApplyLevel2Pace()
    {
        moveSpeed = 3.5f;
        sprintMultiplier = 1.35f;
        landAcceleration = 18f;
        landDeceleration = 24f;
        underwaterMoveSpeed = 1.4f;
        underwaterSprintMultiplier = 1.2f;
        underwaterVerticalSpeed = 1.4f;
        maxUnderwaterRiseSpeed = 2f;
        maxUnderwaterSinkSpeed = 2.5f;
        ApplyOpenWaterCurrent();
    }

    public void ApplyOpenWaterCurrent()
    {
        passiveDriftSpeed = 1.05f;
        passiveVerticalDriftSpeed = .48f;
        passiveDriftAcceleration = .85f;
        passiveDriftInterval = new Vector2(2f, 4.5f);
    }

    void Start()
    {
        // Apply the same controller at runtime, including serialized Level 1
        // players and dynamically created Level 2/3 players. Keep map bounds.
        string level = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (level == "Level1" || level == "Level2" || level == "Level3")
            ApplyLevel1ControlProfile();
        gearState = GetComponent<PlayerGearState>();
        if (level == "Level2") ApplyLevel2Pace();
        if (level == "Level3") ApplyOpenWaterCurrent();
        playerRigidbody = GetComponent<Rigidbody>();

        if (playerRigidbody != null)
        {
            playerRigidbody.useGravity = false;
            // Movement is handled explicitly by this component. A dynamic body can be
            // pushed continuously by dense photogrammetry colliders even with no input.
            playerRigidbody.isKinematic = true;
            playerRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    void Update()
    {
        if (HeartRateRuntime.BlocksGameplay) return;
        if (!EnvironmentReady)
            return;
        bool hasGear = gearState != null && gearState.hasDivingGear;
        UpdateWaterState(hasGear);

        if (isUnderwater)
        {
            UnderwaterMove();
        }
        else
        {
            LandMove();
        }
    }

    void UpdateWaterState(bool hasGear)
    {
        if (!hasGear || !IsInsideWaterArea())
        {
            isUnderwater = false;
            currentWaterDensity = 0f;
            return;
        }

        float waterProbeY = GetWaterProbeY();

        if (isUnderwater)
        {
            if (waterProbeY >= waterSurfaceY + waterExitHeight && !IsDiveHeld())
            {
                isUnderwater = false;
                currentWaterDensity = 0f;
                verticalVelocity = Mathf.Max(verticalVelocity, 0f);
            }
        }
        else if (waterProbeY <= waterSurfaceY - waterEnterDepth || ShouldDiveFromSurface(waterProbeY))
        {
            isUnderwater = true;
            verticalVelocity = ShouldDiveFromSurface(waterProbeY)
                ? -surfaceDiveSpeed
                : Mathf.Min(verticalVelocity * waterEntryFallDamping, 0f);
        }

        currentWaterDensity = isUnderwater ? CalculateWaterDensity() : 0f;
    }

    void LandMove()
    {
        passiveDrift = Vector3.MoveTowards(passiveDrift, Vector3.zero, passiveDriftAcceleration * 4f * Time.deltaTime);
        passiveDriftTarget = Vector3.zero;
        nextPassiveDriftChange = 0f;

        bool isGrounded = IsGrounded();

        if (isGrounded)
        {
            if (verticalVelocity < 0)
                verticalVelocity = 0f;

            if (Input.GetKeyDown(KeyCode.Space))
                verticalVelocity = jumpForce;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
            verticalVelocity = Mathf.Max(verticalVelocity, -maxFallSpeed);
        }

        float x = ReadMovementAxis("Horizontal");
        float z = ReadMovementAxis("Vertical");

        Vector3 move =
            transform.right * x +
            transform.forward * z;

        float currentMoveSpeed = moveSpeed;

        if (IsSprintHeld())
            currentMoveSpeed *= sprintMultiplier;

        Vector3 targetHorizontalVelocity = move.normalized * currentMoveSpeed;
        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetHorizontalVelocity,
            (move.sqrMagnitude > 0f ? landAcceleration : landDeceleration) * Time.deltaTime);

        Vector3 velocity = horizontalVelocity;
        velocity.y = verticalVelocity;

        MoveWithGroundStop(velocity, Time.deltaTime);
    }

    void UnderwaterMove()
    {
        float depthCompression = CalculateWaterDensity();
        currentWaterDensity = depthCompression;

        float x = ReadMovementAxis("Horizontal");
        float z = ReadMovementAxis("Vertical");

        Vector3 move =
            transform.right * x +
            transform.forward * z;

        bool ascendHeld = IsAscendHeld();
        bool diveHeld = IsDiveHeld();
        bool neutralVerticalControl = !ascendHeld && !diveHeld;
        UpdateUnderwaterCurrent();

        float densityMoveSpeed = Mathf.Lerp(
            underwaterMoveSpeed,
            underwaterMoveSpeed * deepMoveSpeedMultiplier,
            depthCompression
        );

        if (IsSprintHeld())
            densityMoveSpeed *= underwaterSprintMultiplier;

        // Water current is an external force: it continues to push while the
        // player swims and must be actively countered during depth holds.
        Vector3 targetHorizontalVelocity = move.normalized * densityMoveSpeed +
            new Vector3(passiveDrift.x, 0f, passiveDrift.z);
        float densityDragFactor = Mathf.Lerp(1f, deepDragMultiplier, depthCompression);
        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetHorizontalVelocity,
            (move.sqrMagnitude > 0f ? underwaterAcceleration : underwaterDeceleration * densityDragFactor)
            * Time.deltaTime);

        Vector3 velocity = horizontalVelocity;

        // Water never disables gravity. Buoyancy opposes it, but equipment compression
        // reduces buoyancy with depth, so an unattended diver slowly becomes negative.
        float buoyancy = Mathf.Lerp(
            surfaceBuoyancyAcceleration,
            surfaceBuoyancyAcceleration * deepBuoyancyMultiplier,
            depthCompression
        );
        // With no vertical control the diver is approximately neutrally trimmed;
        // the changing current still moves the body up or down. C/Space restore
        // the full gravity, buoyancy and thrust response.
        float verticalAcceleration = neutralVerticalControl ? 0f : underwaterGravity + buoyancy;

        float depthThrustMultiplier = Mathf.Lerp(1f, deepVerticalSpeedMultiplier, depthCompression);
        if (ascendHeld)
            verticalAcceleration += verticalThrustAcceleration * depthThrustMultiplier;
        if (diveHeld)
            verticalAcceleration -= verticalThrustAcceleration * depthThrustMultiplier;

        verticalVelocity += verticalAcceleration * Time.deltaTime;

        float depthDrag = Mathf.Lerp(verticalWaterDrag, verticalWaterDrag * deepDragMultiplier, depthCompression);
        verticalVelocity *= Mathf.Exp(-depthDrag * Time.deltaTime);

        verticalVelocity = ApplySurfaceResistance(verticalVelocity);
        verticalVelocity = Mathf.Clamp(verticalVelocity, -maxUnderwaterSinkSpeed, maxUnderwaterRiseSpeed);
        velocity.y = verticalVelocity + passiveDrift.y;

        if (constrainToWaterVolume)
            MoveInsideAuthoredWaterVolume(velocity);
        else
            MoveWithGroundStop(velocity, Time.deltaTime);
        ConstrainToWaterVolume();
    }

    void UpdateUnderwaterCurrent()
    {
        if (nextPassiveDriftChange <= Time.time)
        {
            // Eight discrete compass directions make the current readable but
            // unpredictable. A separate random vertical component supplies
            // rising, sinking and near-level water movement.
            int directionIndex = Random.Range(0, 8);
            float angle = directionIndex * 45f * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            float verticalDirection = Random.Range(-1, 2);
            if (Mathf.Approximately(verticalDirection, 0f))
                verticalDirection = Random.Range(-0.3f, 0.3f);

            float strength = Random.Range(0.5f, 1f);
            passiveDriftTarget = new Vector3(
                direction.x * passiveDriftSpeed,
                verticalDirection * passiveVerticalDriftSpeed,
                direction.z * passiveDriftSpeed) * strength;
            nextPassiveDriftChange = Time.time + Random.Range(
                Mathf.Min(passiveDriftInterval.x, passiveDriftInterval.y),
                Mathf.Max(passiveDriftInterval.x, passiveDriftInterval.y));
        }

        passiveDrift = Vector3.MoveTowards(
            passiveDrift,
            passiveDriftTarget,
            passiveDriftAcceleration * Time.deltaTime);
    }

    bool IsGrounded()
    {
        // A longer probe can see a lower step, but must not suspend the body
        // above it by cancelling gravity before the feet actually reach it.
        return TryFindGround(Mathf.Min(groundCheckDistance, groundSnapOffset + 0.02f), out _);
    }

    void MoveWithGroundStop(Vector3 velocity, float deltaTime)
    {
        Vector3 movement = velocity * deltaTime;
        movement = ApplyWallCollision(movement);

        Vector3 nextPosition = transform.position + movement;

        if (velocity.y < 0f)
        {
            float fallDistance = transform.position.y - nextPosition.y + groundCheckDistance;

            if (TryFindGround(fallDistance, out RaycastHit hit))
            {
                float groundedY = hit.point.y + groundSnapOffset;

                if (nextPosition.y < groundedY)
                {
                    nextPosition.y = groundedY;
                    verticalVelocity = 0f;
                }
            }
        }

        transform.position = nextPosition;
    }

    void MoveInsideAuthoredWaterVolume(Vector3 velocity)
    {
        // Photogrammetry often contains an invisible closure face across a
        // scanned opening. Inside an explicitly bounded water volume, depth is
        // governed by the authored containment floor instead of that scan cap.
        Vector3 movement = ApplyWallCollision(velocity * Time.deltaTime);
        transform.position += movement;
    }

    Vector3 ApplyWallCollision(Vector3 movement)
    {
        Vector3 horizontal = new Vector3(movement.x, 0f, movement.z);
        float distance = horizontal.magnitude;

        if (distance <= 0f)
            return movement;

        Vector3 direction = horizontal / distance;
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;

        if (Physics.SphereCast(
                origin,
                bodyRadius,
                direction,
                out RaycastHit hit,
                distance + wallSkin,
                wallLayer,
                QueryTriggerInteraction.Ignore
            ) && hit.transform != transform && !hit.transform.IsChildOf(transform))
        {
            float allowedDistance = Mathf.Max(0f, hit.distance - wallSkin);
            Vector3 blockedHorizontal = direction * allowedDistance;
            movement.x = blockedHorizontal.x;
            movement.z = blockedHorizontal.z;
        }

        return movement;
    }

    bool TryFindGround(float distance, out RaycastHit hit)
    {
        hit = default;
        float nearest = float.PositiveInfinity;
        foreach (RaycastHit candidate in Physics.RaycastAll(
            transform.position,
            Vector3.down,
            distance,
            groundLayer,
            QueryTriggerInteraction.Ignore
        ))
        {
            if (requireVisibleCaveGround && !Level2SinkholeRuntimeLoader.IsVisibleCaveGround(candidate.collider))
                continue;
            if (candidate.transform == transform || candidate.transform.IsChildOf(transform) ||
                candidate.normal.y <= 0.01f || candidate.distance >= nearest)
                continue;
            hit = candidate;
            nearest = candidate.distance;
        }
        return nearest < float.PositiveInfinity;
    }

    float CalculateWaterDensity()
    {
        float depth = Mathf.Max(0f, waterSurfaceY - GetWaterProbeY());
        return Mathf.InverseLerp(densityStartDepth, maxDensityDepth, depth);
    }

    float ApplySurfaceResistance(float velocityY)
    {
        if (velocityY <= 0f)
            return velocityY;

        float depth = waterSurfaceY - GetWaterProbeY();

        if (depth > surfaceFloatBand)
            return velocityY;

        float surfaceFactor = Mathf.InverseLerp(surfaceFloatBand, 0f, depth);
        return Mathf.Lerp(velocityY, velocityY * upwardSurfaceBrake, surfaceFactor);
    }

    bool IsSprintHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    static float ReadMovementAxis(string axisName)
    {
        float value = Input.GetAxisRaw(axisName);
        return Mathf.Abs(value) < 0.15f ? 0f : value;
    }

    bool IsAscendHeld()
    {
        return Input.GetKey(KeyCode.Space);
    }

    bool IsDiveHeld()
    {
        return Input.GetKey(KeyCode.C);
    }

    bool ShouldDiveFromSurface(float waterProbeY)
    {
        return IsDiveHeld() && waterProbeY <= waterSurfaceY + waterExitHeight;
    }

    float GetWaterProbeY()
    {
        return transform.position.y - waterProbeOffset;
    }

    bool IsInsideWaterArea()
    {
        if (!useBoundedWaterArea)
            return true;

        Vector2 radii = waterAreaRadii;
        if (deepWaterAreaRadii.x > 0f && deepWaterAreaRadii.y > 0f)
            radii = Vector2.Lerp(radii, deepWaterAreaRadii,
                Mathf.InverseLerp(7f, 12f, waterSurfaceY - GetWaterProbeY()));
        float radiusX = Mathf.Max(0.01f, radii.x);
        float radiusZ = Mathf.Max(0.01f, radii.y);
        float normalizedX = (transform.position.x - waterAreaCenterXZ.x) / radiusX;
        float normalizedZ = (transform.position.z - waterAreaCenterXZ.y) / radiusZ;
        bool insideEllipse = normalizedX * normalizedX + normalizedZ * normalizedZ <= 1f;
        bool aboveBottom = GetWaterProbeY() >= waterBottomY;
        return insideEllipse && aboveBottom;
    }

    void ConstrainToWaterVolume()
    {
        if (!constrainToWaterVolume || !useBoundedWaterArea || !isUnderwater)
            return;

        Vector3 position = transform.position;
        float radiusX = Mathf.Max(0.5f, waterAreaRadii.x - waterBoundaryInset);
        float radiusZ = Mathf.Max(0.5f, waterAreaRadii.y - waterBoundaryInset);
        float normalizedX = (position.x - waterAreaCenterXZ.x) / radiusX;
        float normalizedZ = (position.z - waterAreaCenterXZ.y) / radiusZ;
        float normalizedDistance = Mathf.Sqrt(normalizedX * normalizedX + normalizedZ * normalizedZ);

        if (normalizedDistance > 1f)
        {
            normalizedX /= normalizedDistance;
            normalizedZ /= normalizedDistance;
            position.x = waterAreaCenterXZ.x + normalizedX * radiusX;
            position.z = waterAreaCenterXZ.y + normalizedZ * radiusZ;
            horizontalVelocity = Vector3.zero;
            passiveDrift = Vector3.zero;
        }

        float minimumBodyY = waterBottomY + waterProbeOffset + groundSnapOffset;
        if (position.y < minimumBodyY)
        {
            position.y = minimumBodyY;
            verticalVelocity = Mathf.Max(0f, verticalVelocity);
            passiveDrift.y = Mathf.Max(0f, passiveDrift.y);
        }

        transform.position = position;
    }

    public void ResetVerticalVelocity()
    {
        verticalVelocity = 0f;
    }
}
