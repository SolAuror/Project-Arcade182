using UnityEngine;

public enum AirFootyAIState
{
    Recover,
    PredictIntercept,
    AcquireShotLane,
    Charge,
    Strike,
    Cooldown
}

public enum AirFootyAIShotType
{
    NearPost,
    FarPost,
    Bank
}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AirFootyStrikeMotor3D))]
[RequireComponent(typeof(AirFootyAbilityChargeBank3D))]
public class AIPlayer3D : MonoBehaviour
{
    private struct ShotCandidate
    {
        public AirFootyAIShotType type;
        public Vector3 goalTarget;
        public Vector3 aimDirection;
        public Vector3 contactPosition;
        public float bankWallZ;
        public float score;
    }

    [Header("References")]
    [SerializeField] private Transform ball;
    [SerializeField] private Transform player;
    [SerializeField] private AirFootyStrikeMotor3D strikeMotor;
    [SerializeField] private AirFootyAbilityChargeBank3D chargeBank;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6.8f;
    [SerializeField] private float reactionDelay = 0.2f;
    [SerializeField] private Vector2 defensivePosition = new Vector2(5.75f, 0f);
    [SerializeField] private float midfieldBoundaryX = 0.5f;
    [SerializeField, Min(0f)] private float wallSweepSkin = 0.012f;

    [Header("Tactical Arena Estimate")]
    [SerializeField, Min(0.1f)] private float goalWallX = 8.05f;
    [SerializeField, Min(0.1f)] private float sideWallZ = 5.55f;

    [Header("Decision State")]
    [SerializeField, Min(0f)] private float recoverDuration = 0.22f;
    [SerializeField, Min(0.1f)] private float controllableBallSpeed = 2f;
    [SerializeField, Min(0.1f)] private float interceptAcquireDistance = 1.65f;
    [SerializeField, Min(0.1f)] private float shotPositionTolerance = 0.45f;
    [SerializeField, Min(0.1f)] private float shotContactOffset = 1.65f;
    [SerializeField, Min(0.1f)] private float shotPlanInvalidationDistance = 1.9f;
    [SerializeField, Min(0f)] private float cooldownDuration = 0.4f;

    [Header("Ability Use")]
    [SerializeField] private bool useDash = true;
    [SerializeField, Min(0.05f)] private float dashDuration = 0.14f;
    [SerializeField, Min(1f)] private float dashSpeedMultiplier = 2.25f;
    [SerializeField, Min(0.1f)] private float dashMinimumDistance = 1.35f;
    [SerializeField, Min(0.1f)] private float dashMaximumDistance = 5.4f;
    [SerializeField, Min(0f)] private float dashCooldown = 2f;
    [SerializeField, Min(0)] private int dashChargeReserve = 1;
    [SerializeField, Range(-1f, 1f)] private float offensiveDashAimDot = 0.76f;

    [Header("Reactive Pulse")]
    [SerializeField] private bool useReactivePulse = true;
    [SerializeField, Min(0f)] private float reactivePulseChargeSeconds = 0.18f;
    [SerializeField, Min(0f)] private float reactivePulseMinimumSpeed = 2.8f;
    [SerializeField, Min(0f)] private float reactivePulseTowardGoalSpeed = 0.65f;
    [SerializeField, Min(0f)] private float reactivePulseCooldown = 0.85f;

    [Header("Shot Construction")]
    [SerializeField] private float playerGoalX = -8.75f;
    [SerializeField, Min(0f)] private float goalPostInset = 0.95f;
    [SerializeField, Range(0f, 1f)] private float bankWillingness = 0.42f;
    [SerializeField, Range(0f, 8f)] private float standardAimErrorDegrees = 3f;
    [SerializeField, Min(0.05f)] private float telegraphDuration = 0.38f;
    [SerializeField, Min(0f)] private float standardChargeSeconds = 0.42f;
    [SerializeField] private Color telegraphColor = new Color(1f, 0.2f, 0.14f, 1f);

    [Header("Goal-Wall Recovery")]
    [SerializeField, Min(0.1f)] private float goalWallEnterDistance = 0.55f;
    [SerializeField, Min(0.1f)] private float goalWallExitDistance = 1.15f;
    [SerializeField, Min(0f)] private float goalWallWaitBehindBall = 0.75f;

    [Header("Slow Side-Wall Release")]
    [SerializeField, Min(0.1f)] private float sideWallEnterDistance = 0.55f;
    [SerializeField, Min(0.1f)] private float sideWallExitDistance = 1.05f;
    [SerializeField, Min(0f)] private float sideWallEnterBallSpeed = 1.1f;
    [SerializeField, Min(0f)] private float sideWallExitBallSpeed = 1.8f;
    [SerializeField, Min(0f)] private float sideWallReleaseBehindBall = 0.9f;
    [SerializeField, Min(0f)] private float sideWallReleaseTowardCentre = 1.5f;
    [SerializeField, Min(0f)] private float sideWallReleaseContactDistance = 1.45f;
    [SerializeField, Min(0.1f)] private float sideWallReleaseDuration = 0.65f;
    [SerializeField, Min(0f)] private float sideWallReleaseReentryGrace = 1.2f;

    private readonly ShotCandidate[] candidates = new ShotCandidate[3];
    private Rigidbody aiBody;
    private Rigidbody ballBody;
    private BallController3D ballController;
    private Vector3 targetPosition;
    private Vector3 plannedBallPosition;
    private ShotCandidate plannedShot;
    private LineRenderer telegraphLine;
    private Light telegraphGlow;
    private TrailRenderer dashTrail;
    private AudioSource feedbackAudio;
    private float reactionTimer;
    private float stateTimer;
    private float sideWallReleaseStartedAt;
    private float sideWallReleaseReentryUntil;
    private float dashEndsAt;
    private float dashReadyAt;
    private float reactivePulseReadyAt;
    private int shotSequence;
    private bool movementEnabled = true;
    private bool waitingForGoalWallBounce;
    private bool waitingForSideWallRelease;
    private bool hasShotPlan;
    private bool dashing;
    private Vector3 dashDirection;

    public AirFootyAIState CurrentState { get; private set; }
    public AirFootyAIShotType SelectedShotType => plannedShot.type;
    public bool HasShotPlan => hasShotPlan;
    public Vector3 CurrentPlanarVelocity { get; private set; }

    private void Awake()
    {
        aiBody = GetComponent<Rigidbody>();
        AirFootyArenaMovement3D.ConfigureStrikerPhysics(
            aiBody,
            wallSweepSkin);
        strikeMotor = strikeMotor != null
            ? strikeMotor
            : GetComponent<AirFootyStrikeMotor3D>();
        chargeBank = chargeBank != null
            ? chargeBank
            : GetComponent<AirFootyAbilityChargeBank3D>();
        ResolveSceneReferences();
        BuildTelegraph();
        targetPosition = DefensiveTarget();
        SetState(AirFootyAIState.Recover);
    }

    private void FixedUpdate()
    {
        if (ball == null || !movementEnabled)
        {
            CurrentPlanarVelocity = Vector3.zero;
            return;
        }

        stateTimer += Time.fixedDeltaTime;
        reactionTimer -= Time.fixedDeltaTime;
        bool timingCriticalState =
            CurrentState == AirFootyAIState.Charge ||
            CurrentState == AirFootyAIState.Strike ||
            CurrentState == AirFootyAIState.Cooldown;
        if (!dashing && timingCriticalState)
        {
            TickDecisionState();
        }
        else if (!dashing && reactionTimer <= 0f)
        {
            TickDecisionState();
            reactionTimer = reactionDelay;
        }

        MoveTowardTarget();
        UpdateDashState();
    }

    private void Update()
    {
        if (dashTrail != null)
        {
            dashTrail.emitting = movementEnabled && dashing;
        }

        if (telegraphLine == null)
        {
            return;
        }

        bool charging = movementEnabled && CurrentState == AirFootyAIState.Charge;
        telegraphLine.enabled = charging;
        if (telegraphGlow != null)
        {
            telegraphGlow.enabled = charging;
        }

        if (!charging)
        {
            return;
        }

        UpdateTelegraphPath();
        float pulse = 0.72f + Mathf.Sin(Time.unscaledTime * 14f) * 0.18f;
        telegraphLine.widthMultiplier = Mathf.Lerp(0.055f, 0.12f, pulse);
        if (telegraphGlow != null)
        {
            telegraphGlow.intensity = Mathf.Lerp(0.8f, 2f, pulse);
        }
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        reactionTimer = 0f;
        if (!enabled)
        {
            targetPosition = aiBody.position;
            CurrentPlanarVelocity = Vector3.zero;
            waitingForGoalWallBounce = false;
            waitingForSideWallRelease = false;
            hasShotPlan = false;
            reactivePulseReadyAt = float.NegativeInfinity;
            CancelDash();
            chargeBank?.Refill();
            SetState(AirFootyAIState.Recover);
        }
    }

    private void TickDecisionState()
    {
        if (ballBody == null || ballController == null || player == null)
        {
            ResolveSceneReferences();
        }

        switch (CurrentState)
        {
            case AirFootyAIState.Recover:
                UpdateRecover();
                break;
            case AirFootyAIState.PredictIntercept:
                UpdatePredictIntercept();
                break;
            case AirFootyAIState.AcquireShotLane:
                UpdateAcquireShotLane();
                break;
            case AirFootyAIState.Charge:
                UpdateCharge();
                break;
            case AirFootyAIState.Strike:
                ExecuteStrike();
                break;
            case AirFootyAIState.Cooldown:
                UpdateCooldown();
                break;
        }
    }

    private void UpdateRecover()
    {
        targetPosition = DefensiveTarget();
        if (stateTimer < recoverDuration || !BallIsActive())
        {
            return;
        }

        if (ball.position.x > 0.15f &&
            (!BallIsThreateningGoal() || FlatBallSpeed() <= controllableBallSpeed))
        {
            SetState(AirFootyAIState.AcquireShotLane);
        }
        else
        {
            SetState(AirFootyAIState.PredictIntercept);
        }
    }

    private void UpdatePredictIntercept()
    {
        Vector3 velocity = FlatBallVelocity();
        if (TryReactivePulse())
        {
            return;
        }
        if (ball.position.x <= 0.15f && velocity.x <= 0.2f)
        {
            // Do not shadow a harmless ball along the side wall forever.
            // Re-form centrally until it actually travels toward the AI goal.
            targetPosition = DefensiveTarget();
            return;
        }

        float predictedZ = PredictSideWallZAtX(
            ball.position,
            velocity,
            defensivePosition.x,
            -sideWallZ,
            sideWallZ);
        targetPosition = ClampTarget(new Vector3(
            defensivePosition.x,
            aiBody.position.y,
            predictedZ));

        if (!BallIsActive())
        {
            return;
        }

        float distanceToBall = FlatDistance(aiBody.position, ball.position);
        bool canConstructShot =
            ball.position.x > 0.15f &&
            (FlatBallSpeed() <= controllableBallSpeed ||
             velocity.x <= 0.2f ||
             distanceToBall <= interceptAcquireDistance);
        if (canConstructShot)
        {
            SetState(AirFootyAIState.AcquireShotLane);
        }
    }

    private void UpdateAcquireShotLane()
    {
        if (!BallIsActive())
        {
            targetPosition = DefensiveTarget();
            return;
        }

        if (TryReactivePulse())
        {
            return;
        }

        if (ball.position.x <= 0.05f)
        {
            SetState(AirFootyAIState.PredictIntercept);
            return;
        }

        UpdateSideWallReleaseState();
        if (waitingForSideWallRelease)
        {
            waitingForGoalWallBounce = false;
            hasShotPlan = false;
            targetPosition = SideWallReleaseTarget();
            return;
        }

        UpdateGoalWallRecoveryState();
        if (waitingForGoalWallBounce)
        {
            hasShotPlan = false;
            targetPosition = GoalWallWaitTarget();
            return;
        }

        if (BallIsThreateningGoal() &&
            FlatBallSpeed() > controllableBallSpeed &&
            FlatDistance(aiBody.position, ball.position) > interceptAcquireDistance)
        {
            SetState(AirFootyAIState.PredictIntercept);
            return;
        }

        if (!hasShotPlan ||
            FlatDistance(plannedBallPosition, ball.position) > shotPlanInvalidationDistance)
        {
            SelectShotPlan();
        }

        targetPosition = ContactPositionFor(plannedShot.aimDirection);
        plannedShot.contactPosition = targetPosition;
        TryBeginOffensiveDash();

        float positionError = FlatDistance(aiBody.position, targetPosition);
        bool inPosition = positionError <= shotPositionTolerance;
        bool pressurePosition =
            FlatBallSpeed() <= controllableBallSpeed * 1.35f &&
            positionError <= shotPositionTolerance * 1.75f;
        if ((inPosition || pressurePosition) &&
            strikeMotor != null &&
            strikeMotor.IsBallInPulseRange(
                strikeMotor.GetChargeFraction(standardChargeSeconds)))
        {
            SetState(AirFootyAIState.Charge);
        }
    }

    private void UpdateCharge()
    {
        targetPosition = ContactPositionFor(plannedShot.aimDirection);
        if (!BallIsActive() || ball.position.x <= 0f)
        {
            SetState(AirFootyAIState.PredictIntercept);
            return;
        }

        if (FlatDistance(plannedBallPosition, ball.position) >
            shotPlanInvalidationDistance * 1.35f)
        {
            hasShotPlan = false;
            SetState(AirFootyAIState.AcquireShotLane);
            return;
        }

        float charge = strikeMotor != null
            ? strikeMotor.GetChargeFraction(standardChargeSeconds)
            : 0f;
        if (stateTimer >= telegraphDuration &&
            strikeMotor != null &&
            strikeMotor.IsBallInPulseRange(charge))
        {
            SetState(AirFootyAIState.Strike);
        }
        else if (stateTimer >= telegraphDuration)
        {
            hasShotPlan = false;
            SetState(AirFootyAIState.AcquireShotLane);
        }
    }

    private void ExecuteStrike()
    {
        float charge = strikeMotor != null
            ? strikeMotor.GetChargeFraction(standardChargeSeconds)
            : 0f;
        AirFootyStrikeResult result =
            strikeMotor != null &&
            strikeMotor.IsPulseReady &&
            strikeMotor.IsBallInPulseRange(charge) &&
            chargeBank != null &&
            chargeBank.TrySpend()
                ? strikeMotor.TryPulse(charge)
                : AirFootyStrikeResult.Unavailable;

        if (result == AirFootyStrikeResult.Hit ||
            result == AirFootyStrikeResult.Perfect)
        {
            AirFootyWorldPopup.Spawn(
                transform.position + Vector3.up * 0.85f,
                "PULSE",
                telegraphColor);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        AirFootyPlaytestTelemetry.Instance?.RecordAIStrikeResult(
            result);
#endif

        SetState(AirFootyAIState.Cooldown);
    }

    private void UpdateCooldown()
    {
        targetPosition = DefensiveTarget();
        if (stateTimer >= cooldownDuration)
        {
            SetState(AirFootyAIState.Recover);
        }
    }

    private void SelectShotPlan()
    {
        float defenderZ = player != null ? player.position.z : 0f;
        float lowerPostZ = -goalPostInset;
        float upperPostZ = goalPostInset;
        bool lowerIsNear = Mathf.Abs(defenderZ - lowerPostZ) <=
                           Mathf.Abs(defenderZ - upperPostZ);

        candidates[0] = BuildDirectCandidate(
            lowerIsNear ? AirFootyAIShotType.NearPost : AirFootyAIShotType.FarPost,
            lowerPostZ);
        candidates[1] = BuildDirectCandidate(
            lowerIsNear ? AirFootyAIShotType.FarPost : AirFootyAIShotType.NearPost,
            upperPostZ);

        float bankWallZ = defenderZ >= 0f ? -sideWallZ : sideWallZ;
        candidates[2] = BuildBankCandidate(bankWallZ, -Mathf.Sign(bankWallZ) * goalPostInset * 0.65f);

        int bestIndex = 0;
        int secondIndex = 1;
        if (candidates[secondIndex].score > candidates[bestIndex].score)
        {
            (bestIndex, secondIndex) = (secondIndex, bestIndex);
        }
        for (int i = 2; i < candidates.Length; i++)
        {
            if (candidates[i].score > candidates[bestIndex].score)
            {
                secondIndex = bestIndex;
                bestIndex = i;
            }
            else if (candidates[i].score > candidates[secondIndex].score)
            {
                secondIndex = i;
            }
        }

        // Every fourth construction may use a competitive second-best lane. This
        // keeps variety controlled and never substitutes a clearly poor option.
        int selectedIndex =
            shotSequence % 4 == 3 &&
            candidates[bestIndex].score - candidates[secondIndex].score <= 0.65f
                ? secondIndex
                : bestIndex;
        shotSequence++;

        plannedShot = candidates[selectedIndex];
        float signedError = DeterministicAimError(shotSequence);
        plannedShot.aimDirection =
            Quaternion.Euler(0f, signedError, 0f) * plannedShot.aimDirection;
        plannedShot.aimDirection.y = 0f;
        plannedShot.aimDirection.Normalize();
        plannedShot.contactPosition = ContactPositionFor(plannedShot.aimDirection);
        plannedBallPosition = ball.position;
        hasShotPlan = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        AirFootyPlaytestTelemetry.Instance?.RecordAIShotPlan(plannedShot.type);
#endif
    }

    private ShotCandidate BuildDirectCandidate(AirFootyAIShotType type, float targetZ)
    {
        Vector3 goalTarget = new Vector3(playerGoalX, ball.position.y, targetZ);
        Vector3 aim = FlattenAndNormalize(goalTarget - ball.position);
        Vector3 contact = ContactPositionFor(aim);
        float laneClearance = player != null
            ? DistancePointToSegmentXZ(player.position, ball.position, goalTarget)
            : 2f;
        float defenderSeparation = player != null
            ? Mathf.Abs(player.position.z - targetZ)
            : 1f;
        float repositionCost = FlatDistance(aiBody.position, contact);

        return new ShotCandidate
        {
            type = type,
            goalTarget = goalTarget,
            aimDirection = aim,
            contactPosition = contact,
            bankWallZ = 0f,
            score = laneClearance * 0.72f +
                    defenderSeparation * 0.48f -
                    repositionCost * 0.18f +
                    0.25f
        };
    }

    private ShotCandidate BuildBankCandidate(float wallZ, float targetZ)
    {
        Vector3 goalTarget = new Vector3(playerGoalX, ball.position.y, targetZ);
        Vector3 mirroredGoal = goalTarget;
        mirroredGoal.z = 2f * wallZ - goalTarget.z;
        Vector3 aim = FlattenAndNormalize(mirroredGoal - ball.position);
        Vector3 contact = ContactPositionFor(aim);

        TryGetBankBounce(ball.position, aim, wallZ, out Vector3 bounce);
        float laneClearance = player != null
            ? Mathf.Min(
                DistancePointToSegmentXZ(player.position, ball.position, bounce),
                DistancePointToSegmentXZ(player.position, bounce, goalTarget))
            : 2f;
        float repositionCost = FlatDistance(aiBody.position, contact);

        return new ShotCandidate
        {
            type = AirFootyAIShotType.Bank,
            goalTarget = goalTarget,
            aimDirection = aim,
            contactPosition = contact,
            bankWallZ = wallZ,
            score = laneClearance * 0.7f -
                    repositionCost * 0.2f +
                    bankWillingness
        };
    }

    private void SetState(AirFootyAIState state)
    {
        bool stateChanged = CurrentState != state;
        CurrentState = state;
        stateTimer = 0f;
        reactionTimer = 0f;

        if (state == AirFootyAIState.Recover ||
            state == AirFootyAIState.PredictIntercept)
        {
            hasShotPlan = false;
        }

        if (state == AirFootyAIState.Charge)
        {
            PlayChargeCue();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (stateChanged)
        {
            AirFootyPlaytestTelemetry.Instance?.RecordAIStateTransition(state);
        }
#endif
    }

    private void MoveTowardTarget()
    {
        Vector3 previousPosition = aiBody.position;
        Vector3 newPosition;
        if (dashing)
        {
            newPosition =
                previousPosition +
                dashDirection *
                moveSpeed *
                dashSpeedMultiplier *
                Time.fixedDeltaTime;
        }
        else
        {
            float speedMultiplier =
                CurrentState == AirFootyAIState.Charge ? 0.55f : 1f;
            newPosition = Vector3.MoveTowards(
                previousPosition,
                ClampTarget(targetPosition),
                moveSpeed * speedMultiplier * Time.fixedDeltaTime);
        }
        newPosition = AirFootyArenaMovement3D.ResolvePosition(
            aiBody,
            newPosition,
            midfieldBoundaryX,
            float.PositiveInfinity,
            wallSweepSkin);
        Vector3 planarVelocity = Time.fixedDeltaTime > 0f
            ? (newPosition - previousPosition) / Time.fixedDeltaTime
            : Vector3.zero;
        planarVelocity.y = 0f;
        CurrentPlanarVelocity = planarVelocity;
        aiBody.MovePosition(newPosition);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!TryDashContact(collision))
        {
            ApplyMovementContact(collision);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!TryDashContact(collision))
        {
            ApplyMovementContact(collision);
        }
    }

    private void TryBeginDash(Vector3 destination)
    {
        if (!useDash ||
            dashing ||
            Time.time < dashReadyAt ||
            CurrentState != AirFootyAIState.AcquireShotLane ||
            strikeMotor == null ||
            !strikeMotor.IsStrikeReady ||
            chargeBank == null ||
            chargeBank.CurrentCharges <= dashChargeReserve)
        {
            return;
        }

        Vector3 offset = destination - aiBody.position;
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance < dashMinimumDistance ||
            distance > dashMaximumDistance ||
            !chargeBank.TrySpend())
        {
            return;
        }

        dashDirection = offset / distance;
        dashing = true;
        dashEndsAt = Time.time + dashDuration;
        dashReadyAt = Time.time + dashCooldown;
        PlayDashCue();
    }

    private void TryBeginOffensiveDash()
    {
        if (!hasShotPlan || ball == null)
        {
            return;
        }

        Vector3 toBall = ball.position - aiBody.position;
        toBall.y = 0f;
        Vector3 destination = targetPosition;
        if (toBall.sqrMagnitude > 0.0001f &&
            Vector3.Dot(toBall.normalized, plannedShot.aimDirection) >=
            offensiveDashAimDot)
        {
            destination = ball.position + plannedShot.aimDirection * 0.65f;
            destination.y = aiBody.position.y;
        }

        TryBeginDash(destination);
    }

    private bool TryReactivePulse()
    {
        if (!useReactivePulse ||
            Time.time < reactivePulseReadyAt ||
            strikeMotor == null ||
            !strikeMotor.IsPulseReady ||
            chargeBank == null ||
            chargeBank.CurrentCharges <= 0 ||
            !BallIsActive())
        {
            return false;
        }

        Vector3 velocity = FlatBallVelocity();
        if (velocity.magnitude < reactivePulseMinimumSpeed ||
            velocity.x < reactivePulseTowardGoalSpeed)
        {
            return false;
        }

        float charge = strikeMotor.GetChargeFraction(
            reactivePulseChargeSeconds);
        if (!strikeMotor.IsBallInPulseRange(charge) ||
            !chargeBank.TrySpend())
        {
            return false;
        }

        AirFootyStrikeResult result = strikeMotor.TryPulse(charge);
        reactivePulseReadyAt = Time.time + reactivePulseCooldown;
        hasShotPlan = false;
        waitingForGoalWallBounce = false;
        waitingForSideWallRelease = false;
        AirFootyWorldPopup.Spawn(
            transform.position + Vector3.up * 0.85f,
            "PULSE SAVE",
            telegraphColor);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        AirFootyPlaytestTelemetry.Instance?.RecordAIStrikeResult(result);
#endif
        SetState(AirFootyAIState.Cooldown);
        return true;
    }

    private void UpdateDashState()
    {
        if (dashing && Time.time >= dashEndsAt)
        {
            CancelDash();
        }
    }

    private void CancelDash()
    {
        dashing = false;
        if (dashTrail != null)
        {
            dashTrail.emitting = false;
        }
    }

    private bool TryDashContact(Collision collision)
    {
        if (!dashing)
        {
            return false;
        }

        BallController3D contactedBall =
            collision.collider.GetComponentInParent<BallController3D>();
        if (contactedBall == null)
        {
            return false;
        }

        AirFootyStrikeResult result = strikeMotor != null
            ? strikeMotor.TryDashStrike(dashDirection)
            : AirFootyStrikeResult.Unavailable;
        if (result == AirFootyStrikeResult.Hit ||
            result == AirFootyStrikeResult.Perfect)
        {
            CancelDash();
            hasShotPlan = false;
            AirFootyWorldPopup.Spawn(
                transform.position + Vector3.up * 0.85f,
                "DASH",
                telegraphColor);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AirFootyPlaytestTelemetry.Instance?.RecordAIStrikeResult(result);
#endif
            SetState(AirFootyAIState.Cooldown);
        }

        return true;
    }

    private void ApplyMovementContact(Collision collision)
    {
        if (CurrentState == AirFootyAIState.Charge ||
            CurrentState == AirFootyAIState.Strike)
        {
            return;
        }

        BallController3D contactedBall =
            collision.collider.GetComponentInParent<BallController3D>();
        contactedBall?.ApplyMovementContact(
            AirFootyTeam.AI,
            CurrentPlanarVelocity,
            aiBody.position);
    }

    private void ResolveSceneReferences()
    {
        if (ball == null)
        {
            ballController = FindFirstObjectByType<BallController3D>();
            ball = ballController != null ? ballController.transform : null;
        }
        else
        {
            ballController = ball.GetComponent<BallController3D>();
        }

        ballBody = ball != null ? ball.GetComponent<Rigidbody>() : null;
        if (player == null)
        {
            PlayerMovement3D playerMovement = FindFirstObjectByType<PlayerMovement3D>();
            player = playerMovement != null ? playerMovement.transform : null;
        }
    }

    private void BuildTelegraph()
    {
        Transform authoredTelegraph = transform.Find("AI Shot Telegraph");
        if (authoredTelegraph == null)
        {
            Debug.LogError("AirFooty AI is missing its authored AI Shot Telegraph.", this);
            return;
        }
        GameObject telegraphObject = authoredTelegraph.gameObject;

        telegraphLine = telegraphObject.GetComponent<LineRenderer>();
        if (telegraphLine == null)
        {
            Debug.LogError("AirFooty AI Shot Telegraph is missing its authored LineRenderer.", telegraphObject);
            return;
        }
        telegraphLine.useWorldSpace = true;
        telegraphLine.loop = false;
        telegraphLine.positionCount = 2;
        telegraphLine.numCapVertices = 4;
        telegraphLine.numCornerVertices = 3;
        telegraphLine.textureMode = LineTextureMode.Stretch;
        telegraphLine.alignment = LineAlignment.View;
        telegraphLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        telegraphLine.receiveShadows = false;
        telegraphLine.startColor = telegraphColor;
        telegraphLine.endColor = new Color(
            telegraphColor.r,
            telegraphColor.g,
            telegraphColor.b,
            0.16f);

        if (telegraphLine.sharedMaterial == null)
        {
            Debug.LogError("AirFooty AI Shot Telegraph is missing its authored material.", telegraphLine);
            return;
        }
        telegraphLine.enabled = false;

        dashTrail = GetComponent<TrailRenderer>();
        if (dashTrail == null)
        {
            Debug.LogError("AirFooty AI is missing its authored dash TrailRenderer.", this);
            return;
        }
        dashTrail.time = 0.26f;
        dashTrail.minVertexDistance = 0.04f;
        dashTrail.startWidth = 0.46f;
        dashTrail.endWidth = 0f;
        dashTrail.startColor = telegraphColor;
        dashTrail.endColor = new Color(
            telegraphColor.r,
            telegraphColor.g,
            telegraphColor.b,
            0f);
        dashTrail.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        dashTrail.receiveShadows = false;
        dashTrail.sharedMaterial = telegraphLine.sharedMaterial;
        dashTrail.emitting = false;

        telegraphGlow = telegraphObject.GetComponent<Light>();
        if (telegraphGlow == null)
        {
            Debug.LogError("AirFooty AI Shot Telegraph is missing its authored Light.", telegraphObject);
            return;
        }
        telegraphGlow.type = LightType.Point;
        telegraphGlow.color = telegraphColor;
        telegraphGlow.range = 3f;
        telegraphGlow.shadows = LightShadows.None;
        telegraphGlow.enabled = false;

        feedbackAudio = GetComponent<AudioSource>();
        if (feedbackAudio == null)
        {
            Debug.LogError("AirFooty AI is missing its authored feedback AudioSource.", this);
            return;
        }
        feedbackAudio.playOnAwake = false;
        feedbackAudio.spatialBlend = 0.25f;
        feedbackAudio.dopplerLevel = 0f;
    }

    private void UpdateTelegraphPath()
    {
        Vector3 origin = ball.position;
        origin.y += 0.08f;

        if (plannedShot.type == AirFootyAIShotType.Bank &&
            TryGetBankBounce(
                origin,
                plannedShot.aimDirection,
                plannedShot.bankWallZ,
                out Vector3 bounce))
        {
            Vector3 reflectedDirection = plannedShot.aimDirection;
            reflectedDirection.z *= -1f;
            float denominator = reflectedDirection.x;
            float timeToGoal = Mathf.Abs(denominator) > 0.0001f
                ? (playerGoalX - bounce.x) / denominator
                : 0f;
            Vector3 goalPoint = bounce + reflectedDirection * Mathf.Max(0f, timeToGoal);
            goalPoint.y = origin.y;
            bounce.y = origin.y;

            telegraphLine.positionCount = 3;
            telegraphLine.SetPosition(0, origin);
            telegraphLine.SetPosition(1, bounce);
            telegraphLine.SetPosition(2, goalPoint);
            return;
        }

        float directTime = Mathf.Abs(plannedShot.aimDirection.x) > 0.0001f
            ? (playerGoalX - origin.x) / plannedShot.aimDirection.x
            : 0f;
        Vector3 end = origin + plannedShot.aimDirection * Mathf.Max(0f, directTime);
        end.y = origin.y;
        telegraphLine.positionCount = 2;
        telegraphLine.SetPosition(0, origin);
        telegraphLine.SetPosition(1, end);
    }

    private void PlayChargeCue()
    {
        if (feedbackAudio == null)
        {
            return;
        }

        feedbackAudio.pitch = 0.62f;
        feedbackAudio.volume = 0.1f;
        feedbackAudio.PlayOneShot(AirFootyFeedbackUtility.ImpactClip);
    }

    private void PlayDashCue()
    {
        if (feedbackAudio == null)
        {
            return;
        }

        feedbackAudio.pitch = 1.42f;
        feedbackAudio.volume = 0.24f;
        feedbackAudio.PlayOneShot(AirFootyFeedbackUtility.ImpactClip);
    }

    private void UpdateGoalWallRecoveryState()
    {
        float goalWallDistance = DistanceFromGoalWall(ball.position.x);
        if (!waitingForGoalWallBounce && goalWallDistance <= goalWallEnterDistance)
        {
            waitingForGoalWallBounce = true;
        }
        else if (waitingForGoalWallBounce && goalWallDistance >= goalWallExitDistance)
        {
            waitingForGoalWallBounce = false;
        }
    }

    private void UpdateSideWallReleaseState()
    {
        float sideWallDistance =
            Mathf.Max(0f, sideWallZ - Mathf.Abs(ball.position.z));
        float ballSpeed = FlatBallSpeed();
        Vector2 aiToBall = new Vector2(
            ball.position.x - aiBody.position.x,
            ball.position.z - aiBody.position.z);

        if (!waitingForSideWallRelease)
        {
            waitingForSideWallRelease =
                Time.time >= sideWallReleaseReentryUntil &&
                sideWallDistance <= sideWallEnterDistance &&
                ballSpeed <= sideWallEnterBallSpeed &&
                aiToBall.sqrMagnitude <=
                sideWallReleaseContactDistance * sideWallReleaseContactDistance;
            if (waitingForSideWallRelease)
            {
                sideWallReleaseStartedAt = Time.time;
            }
            return;
        }

        if (sideWallDistance >= sideWallExitDistance ||
            ballSpeed >= sideWallExitBallSpeed ||
            Time.time - sideWallReleaseStartedAt >= sideWallReleaseDuration)
        {
            waitingForSideWallRelease = false;
            sideWallReleaseReentryUntil =
                Time.time + sideWallReleaseReentryGrace;
        }
    }

    private Vector3 DefensiveTarget()
    {
        return ClampTarget(new Vector3(
            defensivePosition.x,
            aiBody.position.y,
            defensivePosition.y));
    }

    private Vector3 GoalWallWaitTarget()
    {
        return ClampTarget(new Vector3(
            ball.position.x + goalWallWaitBehindBall,
            aiBody.position.y,
            ball.position.z));
    }

    private Vector3 SideWallReleaseTarget()
    {
        float wallDirection = Mathf.Sign(ball.position.z);
        return ClampTarget(new Vector3(
            ball.position.x + sideWallReleaseBehindBall,
            aiBody.position.y,
            ball.position.z - wallDirection * sideWallReleaseTowardCentre));
    }

    private Vector3 ContactPositionFor(Vector3 aimDirection)
    {
        return ClampTarget(new Vector3(
            ball.position.x - aimDirection.x * shotContactOffset,
            aiBody.position.y,
            ball.position.z - aimDirection.z * shotContactOffset));
    }

    private Vector3 ClampTarget(Vector3 target)
    {
        target.x = Mathf.Max(target.x, midfieldBoundaryX);
        target.y = aiBody != null ? aiBody.position.y : target.y;
        return target;
    }

    private bool BallIsActive()
    {
        return ballController == null || ballController.CanMove;
    }

    private bool BallIsThreateningGoal()
    {
        Vector3 velocity = FlatBallVelocity();
        return velocity.x > 0.2f;
    }

    private Vector3 FlatBallVelocity()
    {
        if (ballBody == null)
        {
            return Vector3.zero;
        }

        Vector3 velocity = ballBody.linearVelocity;
        velocity.y = 0f;
        return velocity;
    }

    private float FlatBallSpeed()
    {
        return FlatBallVelocity().magnitude;
    }

    private float DistanceFromGoalWall(float xPosition)
    {
        return Mathf.Max(0f, goalWallX - xPosition);
    }

    private float DeterministicAimError(int sequence)
    {
        float sample = Mathf.Sin(sequence * 12.9898f + 78.233f) * 43758.5453f;
        float normalized = (sample - Mathf.Floor(sample)) * 2f - 1f;
        return normalized * standardAimErrorDegrees;
    }

    public static float PredictSideWallZAtX(
        Vector3 position,
        Vector3 velocity,
        float targetX,
        float minimumZ,
        float maximumZ)
    {
        if (Mathf.Abs(velocity.x) <= 0.0001f)
        {
            return Mathf.Clamp(position.z, minimumZ, maximumZ);
        }

        float time = (targetX - position.x) / velocity.x;
        if (time <= 0f)
        {
            return Mathf.Clamp(position.z, minimumZ, maximumZ);
        }

        float predictedZ = position.z + velocity.z * time;
        if (predictedZ > maximumZ)
        {
            predictedZ = maximumZ - (predictedZ - maximumZ);
        }
        else if (predictedZ < minimumZ)
        {
            predictedZ = minimumZ + (minimumZ - predictedZ);
        }

        return Mathf.Clamp(predictedZ, minimumZ, maximumZ);
    }

    private static bool TryGetBankBounce(
        Vector3 origin,
        Vector3 direction,
        float wallZ,
        out Vector3 bounce)
    {
        bounce = origin;
        if (Mathf.Abs(direction.z) <= 0.0001f)
        {
            return false;
        }

        float distance = (wallZ - origin.z) / direction.z;
        if (distance <= 0f)
        {
            return false;
        }

        bounce = origin + direction * distance;
        return true;
    }

    private static float DistancePointToSegmentXZ(
        Vector3 point,
        Vector3 segmentStart,
        Vector3 segmentEnd)
    {
        Vector2 point2D = new Vector2(point.x, point.z);
        Vector2 start2D = new Vector2(segmentStart.x, segmentStart.z);
        Vector2 end2D = new Vector2(segmentEnd.x, segmentEnd.z);
        Vector2 segment = end2D - start2D;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= 0.0001f)
        {
            return Vector2.Distance(point2D, start2D);
        }

        float amount = Mathf.Clamp01(Vector2.Dot(point2D - start2D, segment) / lengthSquared);
        return Vector2.Distance(point2D, start2D + segment * amount);
    }

    private static float FlatDistance(Vector3 from, Vector3 to)
    {
        return Vector2.Distance(
            new Vector2(from.x, from.z),
            new Vector2(to.x, to.z));
    }

    private static Vector3 FlattenAndNormalize(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.left;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        reactionDelay = Mathf.Max(0.01f, reactionDelay);
        wallSweepSkin = Mathf.Max(0f, wallSweepSkin);
        goalWallX = Mathf.Max(midfieldBoundaryX + 0.1f, goalWallX);
        sideWallZ = Mathf.Max(0.1f, sideWallZ);
        recoverDuration = Mathf.Max(0f, recoverDuration);
        controllableBallSpeed = Mathf.Max(0.1f, controllableBallSpeed);
        interceptAcquireDistance = Mathf.Max(0.1f, interceptAcquireDistance);
        shotPositionTolerance = Mathf.Max(0.1f, shotPositionTolerance);
        shotContactOffset = Mathf.Max(0.1f, shotContactOffset);
        shotPlanInvalidationDistance = Mathf.Max(0.1f, shotPlanInvalidationDistance);
        cooldownDuration = Mathf.Max(0f, cooldownDuration);
        dashDuration = Mathf.Max(0.05f, dashDuration);
        dashSpeedMultiplier = Mathf.Max(1f, dashSpeedMultiplier);
        dashMinimumDistance = Mathf.Max(0.1f, dashMinimumDistance);
        dashMaximumDistance =
            Mathf.Max(dashMinimumDistance, dashMaximumDistance);
        dashCooldown = Mathf.Max(0f, dashCooldown);
        dashChargeReserve = Mathf.Max(0, dashChargeReserve);
        reactivePulseChargeSeconds = Mathf.Max(0f, reactivePulseChargeSeconds);
        reactivePulseMinimumSpeed = Mathf.Max(0f, reactivePulseMinimumSpeed);
        reactivePulseTowardGoalSpeed = Mathf.Max(0f, reactivePulseTowardGoalSpeed);
        reactivePulseCooldown = Mathf.Max(0f, reactivePulseCooldown);
        goalPostInset = Mathf.Max(0f, goalPostInset);
        telegraphDuration = Mathf.Max(0.05f, telegraphDuration);
        standardChargeSeconds = Mathf.Max(0f, standardChargeSeconds);
        goalWallEnterDistance = Mathf.Max(0.1f, goalWallEnterDistance);
        goalWallExitDistance =
            Mathf.Max(goalWallEnterDistance + 0.1f, goalWallExitDistance);
        goalWallWaitBehindBall = Mathf.Max(0f, goalWallWaitBehindBall);
        sideWallEnterDistance = Mathf.Max(0.1f, sideWallEnterDistance);
        sideWallExitDistance =
            Mathf.Max(sideWallEnterDistance + 0.1f, sideWallExitDistance);
        sideWallEnterBallSpeed = Mathf.Max(0f, sideWallEnterBallSpeed);
        sideWallExitBallSpeed =
            Mathf.Max(sideWallEnterBallSpeed, sideWallExitBallSpeed);
        sideWallReleaseBehindBall = Mathf.Max(0f, sideWallReleaseBehindBall);
        sideWallReleaseTowardCentre = Mathf.Max(0f, sideWallReleaseTowardCentre);
        sideWallReleaseContactDistance = Mathf.Max(0f, sideWallReleaseContactDistance);
        sideWallReleaseDuration = Mathf.Max(0.1f, sideWallReleaseDuration);
        sideWallReleaseReentryGrace = Mathf.Max(0f, sideWallReleaseReentryGrace);
    }
}
