using System;
using UnityEngine;

namespace Finn.Minigames
{
    /// <summary>
    /// A single pachinko ball. Purely physical: it falls, bounces off pegs and bumpers,
    /// and reports when it is done (drained, settled, or timed out). The ball never
    /// scores anything itself — lights detect the ball, not the other way around.
    /// </summary>
    [AddComponentMenu("Finn/Fungus Pachinko/Fungus Ball")]
    [RequireComponent(typeof(Rigidbody))]
    public class FungusBall : MonoBehaviour
    {
        [SerializeField] private float settleSpeedThreshold = 0.05f;
        [SerializeField] private float settleSeconds = 2f;
        [SerializeField] private float maxLifetimeSeconds = 30f;

        /// <summary>Raised exactly once when the ball is finished, before it is destroyed.</summary>
        public event Action<FungusBall> Finished;

        private Rigidbody body;
        private float settleTimer;
        private float lifeTimer;
        private bool finished;

        private void Awake()
        {
            // Enforced in code so a mis-set prefab can't break the board: the ball lives on
            // the machine's local XY plane and must never tunnel through a peg.
            body = GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezePositionZ |
                               RigidbodyConstraints.FreezeRotationX |
                               RigidbodyConstraints.FreezeRotationY;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void FixedUpdate()
        {
            if (finished)
            {
                return;
            }

            lifeTimer += Time.fixedDeltaTime;
            if (lifeTimer >= maxLifetimeSeconds)
            {
                Finish();
                return;
            }

            if (body.linearVelocity.sqrMagnitude < settleSpeedThreshold * settleSpeedThreshold)
            {
                settleTimer += Time.fixedDeltaTime;
                if (settleTimer >= settleSeconds)
                {
                    Finish();
                }
            }
            else
            {
                settleTimer = 0f;
            }
        }

        /// <summary>Retires the ball (drain, settle, or timeout). Safe to call repeatedly.</summary>
        public void Finish()
        {
            if (finished)
            {
                return;
            }

            finished = true;
            Finished?.Invoke(this);
        }
    }
}
