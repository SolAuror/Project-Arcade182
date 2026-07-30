using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GoalZone3D : MonoBehaviour
{
    public enum ScoringSide { Player, AI }

    [SerializeField] private ScoringSide pointGoesTo;
    [SerializeField] private GameManager3D gameManager;
    [SerializeField] private AudioSource goalSound;
    [SerializeField] private ParticleSystem goalParticles;
    [SerializeField] private Color playerGoalColor = new Color(0.12f, 0.62f, 1f, 1f);
    [SerializeField] private Color aiGoalColor = new Color(1f, 0.18f, 0.25f, 1f);

    private bool goalActivated;

    private void Awake()
    {
        if (goalSound == null)
        {
            goalSound = gameObject.AddComponent<AudioSource>();
            goalSound.playOnAwake = false;
            goalSound.spatialBlend = 0.25f;
            goalSound.volume = 0.75f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        BallController3D ball = other.GetComponent<BallController3D>();
        if (ball == null || goalActivated) return;

        goalActivated = true;
        if (gameManager.GoalScored(pointGoesTo))
        {
            PlayGoalFeedback();
        }
    }

    public void AllowGoal()
    {
        goalActivated = false;
    }

    private void PlayGoalFeedback()
    {
        Color color = pointGoesTo == ScoringSide.Player ? playerGoalColor : aiGoalColor;

        if (goalSound != null)
        {
            if (goalSound.clip != null)
            {
                goalSound.Play();
            }
            else
            {
                goalSound.pitch = pointGoesTo == ScoringSide.Player ? 1.05f : 0.92f;
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
            pointGoesTo == ScoringSide.Player ? "BLUE GOAL!" : "RED GOAL!",
            color);
    }
}
