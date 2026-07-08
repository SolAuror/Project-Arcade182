using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Sol/Minigames/Atom Smasher Target")]
    public class AtomSmasherTarget : MonoBehaviour
    {
        [SerializeField] private AtomSmasherGame game;
        [SerializeField] private int scoreValue = 100;
        [SerializeField] private bool requiredTarget = true;
        [SerializeField] private bool deactivateOnHit = true;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color hitColor = Color.black;

        private bool hasBeenHit;
        private MaterialPropertyBlock propertyBlock;

        public int ScoreValue => scoreValue;
        public bool RequiredTarget => requiredTarget;
        public bool HasBeenHit => hasBeenHit;

        private void Awake()
        {
            if (game == null)
            {
                game = FindFirstObjectByType<AtomSmasherGame>();
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>();
            }

            propertyBlock = new MaterialPropertyBlock();
            ApplyColor(activeColor);
        }

        private void OnValidate()
        {
            scoreValue = Mathf.Max(0, scoreValue);
        }

        private void OnCollisionEnter(Collision collision)
        {
            AtomSmasherBall ball = collision.rigidbody != null
                ? collision.rigidbody.GetComponent<AtomSmasherBall>()
                : collision.collider.GetComponentInParent<AtomSmasherBall>();

            if (ball != null)
            {
                TryHit(ball);
            }
        }

        public void AssignGame(AtomSmasherGame owningGame)
        {
            game = owningGame;
        }

        public void ResetTarget()
        {
            hasBeenHit = false;
            gameObject.SetActive(true);
            ApplyColor(activeColor);
        }

        public bool TryHit(AtomSmasherBall ball)
        {
            if (hasBeenHit)
            {
                return false;
            }

            hasBeenHit = true;
            ApplyColor(hitColor);
            game?.RegisterTargetHit(this, ball);

            if (deactivateOnHit)
            {
                gameObject.SetActive(false);
            }

            return true;
        }

        private void ApplyColor(Color color)
        {
            if (targetRenderers == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();

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
