using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Sol/Minigames/Hoops Score Zone")]
    public class HoopsScoreZone : MonoBehaviour
    {
        private enum MovementMode
        {
            Idle,
            PoleSlide,
            BackboardSlide,
            DualSlide,
            Winged
        }

        [SerializeField] private HoopsGame game;
        [SerializeField] private int points = 1;
        [SerializeField] private Vector3 localScoringDirection = Vector3.forward;
        [SerializeField, Range(-1f, 1f)] private float minimumDirectionDot = 0.25f;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private Color inactiveColor = new Color(0.2f, 0.2f, 0.2f);
        [SerializeField] private Color activeColor = Color.green;

        [Header("Movement")]
        [Tooltip("How far the active hoop slides up/down its pole from the authored position. 0 disables pole slides.")]
        [SerializeField, Min(0f)] private float poleTravel = 0.9f;

        [Tooltip("How far the active hoop slides left/right along its backboard from the authored position. 0 disables backboard slides.")]
        [SerializeField, Min(0f)] private float backboardTravel = 1.1f;

        [Tooltip("Slide speed in units per second. Movement never leaves the pole/backboard plane, so the hoop cannot clip backwards.")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 1.4f;

        [Tooltip("Chance an active hoop slides along the backboard instead of the pole.")]
        [SerializeField, Range(0f, 1f)] private float backboardSlideChance = 0.5f;

        [Header("Winged Bonus")]
        [Tooltip("How high a winged hoop lifts off its pole.")]
        [SerializeField, Min(0f)] private float wingedLift = 1.3f;

        [SerializeField, Min(0f)] private float wingedBobAmplitude = 0.35f;
        [SerializeField, Min(0.1f)] private float wingedBobSpeed = 2.4f;
        [SerializeField] private Color wingedColor = new Color(1f, 0.8f, 0.15f);

        [Header("Feel")]
        [Tooltip("How strongly the active hoop's tint breathes toward white. 0 disables.")]
        [SerializeField, Range(0f, 1f)] private float activeColorPulse = 0.35f;

        [Tooltip("Scale punch when a shot scores; eases back each frame.")]
        [SerializeField, Min(1f)] private float scorePunchScale = 1.3f;

        [SerializeField, Min(0.02f)] private float scoreFlashSeconds = 0.15f;

        private Collider scoreTrigger;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 basePosition;
        private Vector3 baseScale;
        private Vector3 lateralAxis;
        private MovementMode movementMode = MovementMode.Idle;
        private float travelDistance;
        private float bobTime;
        private bool isWinged;
        private bool isActiveTarget;
        private Color stateColor;
        private float scoreFlashUntil = -1f;

        public int Points => points;

        /// <summary>True while this hoop is the flying bonus target.</summary>
        public bool IsWinged => isWinged;

        /// <summary>Difficulty factor from motion: 1 static, 2 one-axis, 3 two-axis/winged.</summary>
        public int StageMultiplier { get; private set; } = 1;

        public void AssignGame(HoopsGame owningGame)
        {
            game = owningGame;
        }

        public void SetActiveTarget(bool active)
        {
            SetActiveTarget(active, 0, false);
        }

        public void SetActiveTarget(bool active, int movementStage, bool winged)
        {
            if (scoreTrigger == null)
            {
                scoreTrigger = GetComponent<Collider>();
            }

            scoreTrigger.enabled = active;
            bool wasActive = isActiveTarget;
            isActiveTarget = active;
            isWinged = active && winged;
            stateColor = active ? (isWinged ? wingedColor : activeColor) : inactiveColor;
            SetRendererColor(stateColor);

            // Newly live targets announce themselves; winged gets a bigger flare.
            if (active && !wasActive)
            {
                SpellBurstVisual.Spawn(transform.position, isWinged ? 1.4f : 0.7f, stateColor, 0.3f);
            }

            movementMode = active ? PickMovementMode(movementStage) : MovementMode.Idle;
            StageMultiplier = Mathf.Clamp(movementStage, 0, 2) + 1;
            float travel = CurrentTravel();
            travelDistance = travel > 0f ? Random.Range(0f, travel * 2f) : 0f;
            bobTime = Random.Range(0f, Mathf.PI * 2f);
        }

        /// <summary>Punch, white flash, and burst when a shot scores on this hoop.</summary>
        public void PlayScoreFeedback()
        {
            if (baseScale == Vector3.zero)
            {
                baseScale = transform.localScale; // called before Awake
            }

            transform.localScale = baseScale * scorePunchScale;
            scoreFlashUntil = Time.time + scoreFlashSeconds;
            SetRendererColor(Color.white);
            SpellBurstVisual.Spawn(transform.position, 0.9f, stateColor, 0.3f);
        }

        private MovementMode PickMovementMode(int movementStage)
        {
            if (isWinged)
            {
                return MovementMode.Winged;
            }

            bool canSlidePole = poleTravel > 0f;
            bool canSlideBackboard = backboardTravel > 0f;

            if (movementStage <= 0 || (!canSlidePole && !canSlideBackboard))
            {
                return MovementMode.Idle;
            }

            if (movementStage >= 2 && canSlidePole && canSlideBackboard)
            {
                return MovementMode.DualSlide;
            }

            if (canSlideBackboard && (!canSlidePole || Random.value < backboardSlideChance))
            {
                return MovementMode.BackboardSlide;
            }

            return MovementMode.PoleSlide;
        }

        private float CurrentTravel()
        {
            switch (movementMode)
            {
                case MovementMode.PoleSlide:
                    return poleTravel;

                case MovementMode.BackboardSlide:
                case MovementMode.DualSlide:
                case MovementMode.Winged:
                    return backboardTravel;

                default:
                    return 0f;
            }
        }

        private void Update()
        {
            Vector3 desiredPosition = basePosition;

            if (movementMode != MovementMode.Idle)
            {
                float travel = CurrentTravel();
                if (travel > 0f)
                {
                    travelDistance += moveSpeed * Time.deltaTime;
                }

                // Ping-pong keeps the hoop within its travel range so it never
                // leaves the pole or slides past the backboard edge.
                float slideOffset = travel > 0f ? Mathf.PingPong(travelDistance, travel * 2f) - travel : 0f;

                switch (movementMode)
                {
                    case MovementMode.PoleSlide:
                        desiredPosition = basePosition + Vector3.up * slideOffset;
                        break;

                    case MovementMode.BackboardSlide:
                        desiredPosition = basePosition + lateralAxis * slideOffset;
                        break;

                    case MovementMode.DualSlide:
                        // Vertical runs at a different rate than lateral so the
                        // path traces a wandering loop instead of a diagonal.
                        float verticalOffset = poleTravel > 0f
                            ? Mathf.PingPong(travelDistance * 0.75f, poleTravel * 2f) - poleTravel
                            : 0f;
                        desiredPosition = basePosition + lateralAxis * slideOffset + Vector3.up * verticalOffset;
                        break;

                    case MovementMode.Winged:
                        bobTime += Time.deltaTime * wingedBobSpeed;
                        desiredPosition = basePosition +
                            Vector3.up * (wingedLift + Mathf.Sin(bobTime) * wingedBobAmplitude) +
                            lateralAxis * slideOffset;
                        break;
                }
            }

            if (transform.position != desiredPosition)
            {
                // Chase faster than the slide advances so the hoop stays glued
                // to its track after the initial engage.
                float chaseSpeed = Mathf.Max(moveSpeed * 2f, 1f);
                transform.position = Vector3.MoveTowards(transform.position, desiredPosition, chaseSpeed * Time.deltaTime);
            }

            // Score punch eases back to normal size.
            if (transform.localScale != baseScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.deltaTime * 8f);
            }

            // White score flash, then the active target breathes toward white.
            if (scoreFlashUntil > 0f && Time.time >= scoreFlashUntil)
            {
                scoreFlashUntil = -1f;
                SetRendererColor(stateColor);
            }
            else if (scoreFlashUntil < 0f && isActiveTarget && activeColorPulse > 0f)
            {
                float pulse = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f * activeColorPulse;
                SetRendererColor(Color.Lerp(stateColor, Color.white, pulse));
            }
        }

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void Awake()
        {
            scoreTrigger = GetComponent<Collider>();
            scoreTrigger.isTrigger = true;
            propertyBlock = new MaterialPropertyBlock();
            basePosition = transform.position;
            baseScale = transform.localScale;

            // SetActiveTarget may have run before Awake (game wiring order).
            if (stateColor == default)
            {
                stateColor = inactiveColor;
            }

            // Lateral movement runs along the backboard face, perpendicular to
            // the scoring direction, so the hoop never drifts backwards.
            lateralAxis = transform.right.normalized;

            if (game == null)
            {
                game = FindFirstObjectByType<HoopsGame>();
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>();
            }

            // Repaint now that renderers exist; SetActiveTarget may have run
            // before Awake (game wiring order) and found none to tint.
            SetRendererColor(stateColor);
        }

        private void OnValidate()
        {
            points = Mathf.Max(1, points);

            if (localScoringDirection.sqrMagnitude <= 0.001f)
            {
                localScoringDirection = Vector3.forward;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null)
            {
                return;
            }

            // Balls and any scorable prop lying around the court both count.
            HoopsThrowable throwable = rb.GetComponent<HoopsThrowable>();
            HoopsScorable scorable = rb.GetComponent<HoopsScorable>();
            if (throwable == null && scorable == null)
            {
                return;
            }

            bool canScore = scorable != null ? scorable.CanScore : throwable.CanScore;
            if (!canScore)
            {
                return;
            }

            Vector3 scoringDirection = transform.TransformDirection(localScoringDirection.normalized);
            if (Vector3.Dot(rb.linearVelocity.normalized, scoringDirection) < minimumDirectionDot)
            {
                return;
            }

            game?.RegisterScore(this, throwable, scorable);
        }

        private void SetRendererColor(Color color)
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            foreach (Renderer targetRenderer in targetRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_Color", color);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
