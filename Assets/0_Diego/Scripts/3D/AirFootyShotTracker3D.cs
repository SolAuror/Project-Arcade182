using UnityEngine;

public enum AirFootyShotClassification
{
    Direct,
    OneBank,
    MultiBank,
    Save
}

[DisallowMultipleComponent]
[RequireComponent(typeof(BallController3D))]
public sealed class AirFootyShotTracker3D : MonoBehaviour
{
    [Header("Classification")]
    [SerializeField, Min(0f)] private float minimumBankSpeed = 1.5f;

    [Header("Feedback")]
    [SerializeField] private Color oneBankColor = new Color(1f, 0.78f, 0.18f, 1f);
    [SerializeField] private Color multiBankColor = new Color(1f, 0.32f, 0.82f, 1f);
    [SerializeField] private Color playerSaveColor = new Color(0.2f, 0.82f, 1f, 1f);
    [SerializeField] private Color aiSaveColor = new Color(1f, 0.3f, 0.22f, 1f);

    private BallController3D ball;
    private Rigidbody ballBody;
    private AirFootyTeam shootingTeam;
    private int bankCount;
    private bool shotActive;

    public bool ShotActive => shotActive;
    public AirFootyTeam ShootingTeam => shootingTeam;
    public int BankCount => bankCount;

    private void Awake()
    {
        ball = GetComponent<BallController3D>();
        ballBody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        ball.DeliberateStrike += HandleDeliberateStrike;
        ball.CollisionEntered += HandleCollision;
        ball.ShotSequenceReset += ResetShot;
        ball.Stalled += ResetShot;
    }

    private void OnDisable()
    {
        ball.DeliberateStrike -= HandleDeliberateStrike;
        ball.CollisionEntered -= HandleCollision;
        ball.ShotSequenceReset -= ResetShot;
        ball.Stalled -= ResetShot;
    }

    public void RecordGoal(Vector3 goalPosition, AirFootyTeam scoringTeam)
    {
        if (!shotActive)
        {
            return;
        }

        AirFootyShotClassification classification = bankCount switch
        {
            0 => AirFootyShotClassification.Direct,
            1 => AirFootyShotClassification.OneBank,
            _ => AirFootyShotClassification.MultiBank
        };

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        AirFootyPlaytestTelemetry.Instance?.RecordShotOutcome(
            classification,
            shootingTeam,
            scoringTeam);
#endif

        if (classification == AirFootyShotClassification.OneBank)
        {
            AirFootyWorldPopup.Spawn(
                goalPosition + Vector3.up * 1.55f,
                "BANK SHOT!",
                oneBankColor);
        }
        else if (classification == AirFootyShotClassification.MultiBank)
        {
            AirFootyWorldPopup.Spawn(
                goalPosition + Vector3.up * 1.55f,
                "RICOCHET!",
                multiBankColor);
        }

        ResetShot();
    }

    private void HandleDeliberateStrike(
        AirFootyTeam team,
        AirFootyTouchType touchType)
    {
        if (team == AirFootyTeam.None)
        {
            return;
        }

        if (shotActive && team != shootingTeam)
        {
            RecordSave(team, transform.position);
        }

        shotActive = true;
        shootingTeam = team;
        bankCount = 0;
    }

    private void HandleCollision(Collision collision)
    {
        if (!shotActive || collision == null)
        {
            return;
        }

        AirFootyTeam defender = ResolveStrikerTeam(collision.collider);
        if (defender != AirFootyTeam.None && defender != shootingTeam)
        {
            RecordSave(defender, collision.transform.position);
            return;
        }

        if (IsArenaRailCollision(collision))
        {
            bankCount++;
        }
    }

    private void RecordSave(AirFootyTeam defendingTeam, Vector3 position)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        AirFootyPlaytestTelemetry.Instance?.RecordShotOutcome(
            AirFootyShotClassification.Save,
            shootingTeam,
            defendingTeam);
#endif

        Color saveColor = defendingTeam == AirFootyTeam.Player
            ? playerSaveColor
            : aiSaveColor;
        AirFootyWorldPopup.Spawn(
            position + Vector3.up * 1.05f,
            "SAVE!",
            saveColor);
        ResetShot();
    }

    private bool IsArenaRailCollision(Collision collision)
    {
        if (collision.collider == null ||
            collision.collider.isTrigger ||
            collision.collider.attachedRigidbody != null ||
            collision.contactCount <= 0)
        {
            return false;
        }

        Vector3 normal = collision.GetContact(0).normal;
        float speed = ballBody != null
            ? new Vector2(ballBody.linearVelocity.x, ballBody.linearVelocity.z).magnitude
            : collision.relativeVelocity.magnitude;
        return Mathf.Abs(normal.y) < 0.5f && speed >= minimumBankSpeed;
    }

    private static AirFootyTeam ResolveStrikerTeam(Collider collider)
    {
        if (collider == null)
        {
            return AirFootyTeam.None;
        }
        AirFootyTeamMember3D member =
            collider.GetComponentInParent<AirFootyTeamMember3D>();
        if (member != null && member.Team != AirFootyTeam.None)
        {
            return member.Team;
        }

        if (collider.GetComponentInParent<PlayerMovement3D>() != null)
        {
            return AirFootyTeam.Player;
        }
        if (collider.GetComponentInParent<AIPlayer3D>() != null)
        {
            return AirFootyTeam.AI;
        }

        return AirFootyTeam.None;
    }

    private void ResetShot()
    {
        shotActive = false;
        shootingTeam = AirFootyTeam.None;
        bankCount = 0;
    }

    private void OnValidate()
    {
        minimumBankSpeed = Mathf.Max(0f, minimumBankSpeed);
    }
}
