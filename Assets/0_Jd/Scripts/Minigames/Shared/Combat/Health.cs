using UnityEngine;
using UnityEngine.Events;

namespace Sol.Minigames
{
    /// <summary>
    /// Reusable health pool for players, enemies, and props in any minigame.
    /// Damage from the owning faction is ignored unless friendly fire is enabled.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Shared/Health")]
    public class Health : MonoBehaviour
    {
        [Header("Health")]
        [Tooltip("Maximum health.")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        [Tooltip("Which side this health belongs to.")]
        [SerializeField] private Faction faction = Faction.Neutral;

        [Tooltip("Allow damage dealt by the same faction.")]
        [SerializeField] private bool allowFriendlyFire;

        [Header("Events")]
        [Tooltip("Invoked with the damage amount whenever damage is applied.")]
        [SerializeField] private UnityEvent<float> onDamaged = new UnityEvent<float>();

        [Tooltip("Invoked once when health reaches zero.")]
        [SerializeField] private UnityEvent onDied = new UnityEvent();

        private float current;
        private bool initialized;
        private bool isDead;

        public float Max => maxHealth;

        public float Current
        {
            get
            {
                EnsureInitialized();
                return current;
            }
        }

        public float Normalized => maxHealth > 0f ? Mathf.Clamp01(Current / maxHealth) : 0f;
        public bool IsDead => isDead;

        public Faction Faction
        {
            get => faction;
            set => faction = value;
        }

        public UnityEvent<float> OnDamaged => onDamaged;
        public UnityEvent OnDied => onDied;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
        }

        public void TakeDamage(float amount, Faction source)
        {
            EnsureInitialized();
            if (isDead || amount <= 0f)
            {
                return;
            }

            if (!allowFriendlyFire && source == faction)
            {
                return;
            }

            current = Mathf.Max(0f, current - amount);
            onDamaged.Invoke(amount);

            if (current <= 0f)
            {
                isDead = true;
                onDied.Invoke();
            }
        }

        public void Heal(float amount)
        {
            EnsureInitialized();
            if (isDead || amount <= 0f)
            {
                return;
            }

            current = Mathf.Min(maxHealth, current + amount);
        }

        public void IncreaseMax(float amount, bool healByAmount = true)
        {
            EnsureInitialized();
            if (amount <= 0f)
            {
                return;
            }

            maxHealth += amount;
            if (healByAmount && !isDead)
            {
                current = Mathf.Min(maxHealth, current + amount);
            }
        }

        public void ResetToMax()
        {
            EnsureInitialized();
            current = maxHealth;
            isDead = false;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            current = maxHealth;
        }
    }
}
