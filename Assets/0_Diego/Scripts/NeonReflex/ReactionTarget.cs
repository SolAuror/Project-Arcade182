using System.Collections;
using UnityEngine;

namespace NeonReflex
{
    [DisallowMultipleComponent]
    public class ReactionTarget : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [Tooltip("Authored materials for the two target kinds. Swapped in as " +
                 "shared materials, so spawning never clones one at runtime.")]
        [SerializeField] private Material realMaterial;
        [SerializeField] private Material fakeMaterial;
        [SerializeField] private ParticleSystem hitParticles;

        private GameManager gameManager;
        private TargetSpawner targetSpawner;
        private bool isFake;
        private bool finished;

        public bool HasRequiredReferences =>
            targetRenderer != null && realMaterial != null && fakeMaterial != null;

        private void Awake()
        {
            if (!HasRequiredReferences)
            {
                Debug.LogError(
                    $"{name} requires an authored Renderer plus real and fake target materials. " +
                    "Check the authored Neon Reflex target prefab.",
                    this);
                enabled = false;
            }
        }

        public void Setup(GameManager manager, TargetSpawner spawner, float lifetime, bool fakeTarget)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            gameManager = manager;
            targetSpawner = spawner;
            isFake = fakeTarget;

            Material kindMaterial = isFake ? fakeMaterial : realMaterial;
            if (targetRenderer != null && kindMaterial != null)
            {
                targetRenderer.sharedMaterial = kindMaterial;
            }

            StartCoroutine(LifetimeTimer(lifetime));
        }

        public void ClickTarget()
        {
            if (!isActiveAndEnabled || finished) return;
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
