using System.Collections;
using UnityEngine;

namespace NeonReflex
{
    public class ReactionTarget : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color realColour = new Color(0f, 1f, 0.85f);
        [SerializeField] private Color fakeColour = new Color(1f, 0.05f, 0.15f);
        [SerializeField] private ParticleSystem hitParticles;

        private GameManager gameManager;
        private TargetSpawner targetSpawner;
        private bool isFake;
        private bool finished;

        public void Setup(GameManager manager, TargetSpawner spawner, float lifetime, bool fakeTarget)
        {
            gameManager = manager;
            targetSpawner = spawner;
            isFake = fakeTarget;

            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (targetRenderer != null) targetRenderer.material.color = isFake ? fakeColour : realColour;

            StartCoroutine(LifetimeTimer(lifetime));
        }

        public void ClickTarget()
        {
            if (finished) return;
            finished = true;

            if (hitParticles != null)
            {
                hitParticles.transform.SetParent(null);
                hitParticles.Play();
                Destroy(hitParticles.gameObject, 2f);
            }

            gameManager.TargetHit(isFake);
            RemoveTarget();
        }

        private IEnumerator LifetimeTimer(float lifetime)
        {
            yield return new WaitForSeconds(lifetime);
            if (finished) yield break;

            finished = true;
            gameManager.TargetExpired(isFake);
            RemoveTarget();
        }

        private void RemoveTarget()
        {
            targetSpawner.RemoveTarget(this);
            Destroy(gameObject);
        }
    }
}
