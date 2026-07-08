using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Reusable regenerating resource pool used by <see cref="SpellCaster"/>.
    /// Casters without a Mana component cast for free (useful for simple enemies).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Shared/Mana")]
    public class Mana : MonoBehaviour
    {
        [Header("Mana")]
        [Tooltip("Maximum mana.")]
        [SerializeField, Min(0f)] private float maxMana = 100f;

        [Tooltip("Mana restored per second.")]
        [SerializeField, Min(0f)] private float regenPerSecond = 8f;

        [Tooltip("Start with a full pool.")]
        [SerializeField] private bool startFull = true;

        private float current;
        private bool initialized;

        public float Max => maxMana;

        public float Current
        {
            get
            {
                EnsureInitialized();
                return current;
            }
        }

        public float Normalized => maxMana > 0f ? Mathf.Clamp01(Current / maxMana) : 0f;

        public float RegenPerSecond
        {
            get => regenPerSecond;
            set => regenPerSecond = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            EnsureInitialized();
            if (regenPerSecond <= 0f || current >= maxMana)
            {
                return;
            }

            current = Mathf.Min(maxMana, current + regenPerSecond * Time.deltaTime);
        }

        private void OnValidate()
        {
            maxMana = Mathf.Max(0f, maxMana);
            regenPerSecond = Mathf.Max(0f, regenPerSecond);
        }

        public bool CanSpend(float cost)
        {
            return cost <= 0f || Current >= cost;
        }

        public bool TrySpend(float cost)
        {
            EnsureInitialized();
            if (cost <= 0f)
            {
                return true;
            }

            if (current < cost)
            {
                return false;
            }

            current -= cost;
            return true;
        }

        public void Restore(float amount)
        {
            EnsureInitialized();
            if (amount <= 0f)
            {
                return;
            }

            current = Mathf.Min(maxMana, current + amount);
        }

        public void IncreaseMax(float amount, bool restoreByAmount = true)
        {
            EnsureInitialized();
            if (amount <= 0f)
            {
                return;
            }

            maxMana += amount;
            if (restoreByAmount)
            {
                current = Mathf.Min(maxMana, current + amount);
            }
        }

        public void ResetToMax()
        {
            EnsureInitialized();
            current = maxMana;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            current = startFull ? maxMana : 0f;
        }
    }
}
