using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GoalZone3D : MonoBehaviour
{
    public enum ScoringSide { Player, AI }

    [SerializeField] private ScoringSide pointGoesTo;
    [SerializeField] private AirFootyTeam goalOwner;
    [SerializeField] private GameManager3D gameManager;
    [SerializeField] private AudioSource goalSound;
    [SerializeField] private ParticleSystem goalParticles;
    [SerializeField] private AirFootyShotTracker3D shotTracker;
    [SerializeField] private Color playerGoalColor = new Color(0.12f, 0.62f, 1f, 1f);
    [SerializeField] private Color aiGoalColor = new Color(1f, 0.18f, 0.25f, 1f);

    private readonly HashSet<int> ballsInside = new();

    public AirFootyTeam OwnerTeam
    {
        get
        {
            if (goalOwner == AirFootyTeam.None)
            {
                goalOwner = ResolveGoalOwner();
            }
            return goalOwner;
        }
    }

    private void Awake()
    {
        if (goalOwner == AirFootyTeam.None)
        {
            goalOwner = ResolveGoalOwner();
        }
        if (gameManager == null)
        {
            gameManager = GetComponentInParent<GameManager3D>();
        }

        if (shotTracker == null)
        {
            Debug.LogError(
                $"{nameof(GoalZone3D)} on {name} requires an authored shot tracker reference.",
                this);
        }

        if (goalSound == null)
        {
            Debug.LogError(
                $"{nameof(GoalZone3D)} on {name} requires an authored goal AudioSource. " +
                "Check the authored goal prefab.",
                this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        BallController3D ball = other.GetComponentInParent<BallController3D>();
        if (ball == null || !ballsInside.Add(ball.GetInstanceID())) return;

        if (gameManager != null &&
            gameManager.GoalConceded(OwnerTeam, ball, out AirFootyTeam scoringTeam))
        {
            shotTracker?.RecordGoal(transform.position, scoringTeam);
            PlayGoalFeedback();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        BallController3D ball = other.GetComponentInParent<BallController3D>();
        if (ball != null)
        {
            ballsInside.Remove(ball.GetInstanceID());
        }
    }

    public void AllowGoal()
    {
        ballsInside.Clear();
    }

    private void PlayGoalFeedback()
    {
        Color color = AirFootyTeamMember3D.ColorFor(OwnerTeam);

        if (goalSound != null)
        {
            if (goalSound.clip != null)
            {
                goalSound.Play();
            }
            else
            {
                goalSound.pitch = OwnerTeam == AirFootyTeam.Blue ? 0.92f : 1.05f;
                goalSound.PlayOneShot(AirFootyFeedbackUtility.GoalClip);
            }
        }

        if (goalParticles != null)
        {
            goalParticles.Play();
        }
        else
        {
            AirFootyFeedbackUtility.SpawnGoalBurst(
                transform.position + Vector3.up * 0.45f,
                color);
        }

        AirFootyWorldPopup.Spawn(
            transform.position + Vector3.up * 1.15f,
            $"{AirFootyTeamMember3D.DisplayName(OwnerTeam)} CONCEDES!",
            color);
    }

    private AirFootyTeam ResolveGoalOwner()
    {
        AirFootyTeam inferred =
            AirFootyTeamMember3D.InferFromHierarchy(transform);
        if (inferred != AirFootyTeam.None)
        {
            return inferred;
        }

        return pointGoesTo == ScoringSide.Player
            ? AirFootyTeam.Red
            : AirFootyTeam.Blue;
    }
}
