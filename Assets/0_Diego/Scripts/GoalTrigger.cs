using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    public enum ScoringPlayer
    {
        LeftPlayer,
        RightPlayer
    }

    [SerializeField] private ScoringPlayer pointGoesTo;
    [SerializeField] private ScoreManager scoreManager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Ball ball = other.GetComponent<Ball>();

        if (ball == null)
        {
            return;
        }

        if (pointGoesTo == ScoringPlayer.LeftPlayer)
        {
            scoreManager.AddLeftScore();
        }
        else
        {
            scoreManager.AddRightScore();
        }

        ball.ResetBall();
    }
}

