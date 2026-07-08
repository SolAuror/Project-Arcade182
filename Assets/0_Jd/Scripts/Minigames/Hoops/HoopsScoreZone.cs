using UnityEngine;

namespace Sol.Minigames
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Sol/Minigames/Hoops Score Zone")]
    public class HoopsScoreZone : MonoBehaviour
    {
        [SerializeField] private HoopsGame game;
        [SerializeField] private int points = 1;
        [SerializeField] private Vector3 localScoringDirection = Vector3.forward;
        [SerializeField, Range(-1f, 1f)] private float minimumDirectionDot = 0.25f;
        [SerializeField] private Renderer[] targetRenderers;
        [SerializeField] private Color inactiveColor = new Color(0.2f, 0.2f, 0.2f);
        [SerializeField] private Color activeColor = Color.green;

        private Collider scoreTrigger;
        private MaterialPropertyBlock propertyBlock;

        public int Points => points;

        public void AssignGame(HoopsGame owningGame)
        {
            game = owningGame;
        }

        public void SetActiveTarget(bool active)
        {
            if (scoreTrigger == null)
            {
                scoreTrigger = GetComponent<Collider>();
            }

            scoreTrigger.enabled = active;
            SetRendererColor(active ? activeColor : inactiveColor);
        }

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;
        }

        private void Awake()
        {
            scoreTrigger = GetComponent<Collider>();
            scoreTrigger.isTrigger = true;
            propertyBlock = new MaterialPropertyBlock();

            if (game == null)
            {
                game = FindFirstObjectByType<HoopsGame>();
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<Renderer>();
            }
        }

        private void OnValidate()
        {
            points = Mathf.Max(1, points);

            if (localScoringDirection.sqrMagnitude <= 0.001f)
            {
                localScoringDirection = Vector3.forward;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb == null)
            {
                return;
            }

            HoopsThrowable scoreObject = rb.GetComponent<HoopsThrowable>();
            if (scoreObject == null || !scoreObject.CanScore)
            {
                return;
            }

            Vector3 scoringDirection = transform.TransformDirection(localScoringDirection.normalized);
            if (Vector3.Dot(rb.linearVelocity.normalized, scoringDirection) < minimumDirectionDot)
            {
                return;
            }

            game?.RegisterScore(this, scoreObject);
        }

        private void SetRendererColor(Color color)
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

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
