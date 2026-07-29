using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GoalZone3D : MonoBehaviour
{
    public enum ScoringSide { Player, AI }

    [SerializeField] private ScoringSide pointGoesTo;
    [SerializeField] private GameManager3D gameManager;
    [SerializeField] private AudioSource goalSound;
    [SerializeField] private ParticleSystem goalParticles;

    private bool goalActivated;

    private void OnTriggerEnter(Collider other)
    {
        BallController3D ball = other.GetComponent<BallController3D>();
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
