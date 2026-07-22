using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class GoalZone : MonoBehaviour
{
    public enum ScoringSide { Player, AI }

    [SerializeField] private ScoringSide pointGoesTo;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private AudioSource goalSound;
    [SerializeField] private ParticleSystem goalParticles;

    private bool goalActivated;

    private void OnTriggerEnter2D(Collider2D other)
    {
        BallController ball = other.GetComponent<BallController>();
        if (ball == null || goalActivated) return;

        goalActivated = true;
        if (gameManager.GoalScored(pointGoesTo))
        {
            if (goalSound != null) goalSound.Play();
            if (goalParticles != null) goalParticles.Play();
        }
    }

    public void AllowGoal()
    {
        goalActivated = false;
    }
}
