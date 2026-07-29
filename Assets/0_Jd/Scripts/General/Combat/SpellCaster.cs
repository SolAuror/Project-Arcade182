using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.Minigames
{
    /// <summary>
    /// Reusable spell loadout: a list of <see cref="SpellDefinition"/> slots with
    /// runtime unlock/level/cooldown state. Drives casts for players (input) and
    /// enemies (AI). Upgrades mutate slot state here, never the shared assets.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Sol/Minigames/Shared/Spell Caster")]
    public class SpellCaster : MonoBehaviour
    {
        [Serializable]
        public class SpellSlot
        {
            [Tooltip("Spell asset cast from this slot.")]
            public SpellDefinition definition;

            [Tooltip("Available from the start, or locked until unlocked at runtime.")]
            public bool unlockedAtStart = true;
        }

        public class SlotState
        {
            public bool Unlocked;
            public int Level = 1;
            public float CooldownRemaining;
            public float DamageMultiplier = 1f;
            public float CooldownMultiplier = 1f;
            public float RadiusBonus;
        }

        [Header("Spells")]
        [SerializeField] private List<SpellSlot> slots = new List<SpellSlot>();

        private List<SlotState> states;
        private Mana mana;
        private Health health;
        private float manaCostMultiplier = 1f;

        /// <summary>Lowest the mana-cost multiplier can be driven (75% max total discount).</summary>
        public const float MinManaCostMultiplier = 0.25f;

        public int SlotCount => slots.Count;

        /// <summary>Global mana-cost scale from discount upgrades; 1 = full price.</summary>
        public float ManaCostMultiplier => manaCostMultiplier;

        private void Awake()
        {
            mana = GetComponent<Mana>();
            health = GetComponent<Health>();
            EnsureStates();
        }

        /// <summary>Replaces the loadout. The first <paramref name="unlockedCount"/> slots start unlocked.</summary>
        public void ConfigureSlots(IList<SpellDefinition> definitions, int unlockedCount)
        {
            slots.Clear();
            if (definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    slots.Add(new SpellSlot
                    {
                        definition = definitions[i],
                        unlockedAtStart = i < unlockedCount
                    });
                }
            }

            states = null;
            EnsureStates();
        }

        public SpellDefinition GetDefinition(int index)
        {
            return index >= 0 && index < slots.Count ? slots[index].definition : null;
        }

        public SlotState GetState(int index)
        {
            EnsureStates();
            return index >= 0 && index < states.Count ? states[index] : null;
        }

        public bool IsUnlocked(int index)
        {
            SlotState state = GetState(index);
            return state != null && state.Unlocked;
        }

        /// <summary>0 = ready, 1 = cooldown just started. For HUD cooldown wheels.</summary>
        public float GetCooldownNormalized(int index)
        {
            SlotState state = GetState(index);
            SpellDefinition definition = GetDefinition(index);
            if (state == null || definition == null)
            {
                return 0f;
            }

            float duration = definition.CooldownSeconds * state.CooldownMultiplier;
            return duration > 0f ? Mathf.Clamp01(state.CooldownRemaining / duration) : 0f;
        }

        public void Unlock(int index)
        {
            SlotState state = GetState(index);
            if (state != null)
            {
                state.Unlocked = true;
            }
        }

        public void EmpowerDamage(int index, float percent)
        {
            SlotState state = GetState(index);
            if (state != null)
            {
                state.DamageMultiplier *= 1f + Mathf.Max(0f, percent);
                state.Level++;
            }
        }

        public void ReduceCooldown(int index, float percent)
        {
            SlotState state = GetState(index);
            if (state != null)
            {
                state.CooldownMultiplier *= Mathf.Clamp01(1f - percent);
                state.Level++;
            }
        }

        public void AddRadius(int index, float amount)
        {
            SlotState state = GetState(index);
            if (state != null)
            {
                state.RadiusBonus += Mathf.Max(0f, amount);
                state.Level++;
            }
        }

        /// <summary>Cuts every spell's mana cost by <paramref name="percent"/> (0.15 = -15%), clamped to <see cref="MinManaCostMultiplier"/>.</summary>
        public void ReduceManaCost(float percent)
        {
            manaCostMultiplier *= Mathf.Clamp01(1f - percent);
            manaCostMultiplier = Mathf.Max(MinManaCostMultiplier, manaCostMultiplier);
        }

        /// <summary>Restores full mana cost; called on a fresh run so discounts never leak between runs.</summary>
        public void ResetManaCostMultiplier()
        {
            manaCostMultiplier = 1f;
        }

        /// <summary>
        /// Casts the slot if it is unlocked, off cooldown, the caster is alive, and
        /// mana (when present) can pay the cost. The context's per-slot runtime
        /// values are filled in before the spell resolves.
        /// </summary>
        public bool TryCast(int index, SpellCastContext context)
        {
            SpellDefinition definition = GetDefinition(index);
            SlotState state = GetState(index);
            if (definition == null || state == null || !state.Unlocked || state.CooldownRemaining > 0f)
            {
                return false;
            }

            if (health != null && health.IsDead)
            {
                return false;
            }

            if (mana != null && !mana.TrySpend(definition.ManaCost * manaCostMultiplier))
            {
                return false;
            }

            context.Level = state.Level;
            context.DamageMultiplier = state.DamageMultiplier;
            context.RadiusBonus = state.RadiusBonus;

            definition.Cast(context);
            state.CooldownRemaining = definition.CooldownSeconds * state.CooldownMultiplier;
            return true;
        }

        private void Update()
        {
            EnsureStates();
            foreach (SlotState state in states)
            {
                if (state.CooldownRemaining > 0f)
                {
                    state.CooldownRemaining = Mathf.Max(0f, state.CooldownRemaining - Time.deltaTime);
                }
            }
        }

        private void EnsureStates()
        {
            if (states != null && states.Count == slots.Count)
            {
                return;
            }

            states = new List<SlotState>(slots.Count);
            foreach (SpellSlot slot in slots)
            {
                states.Add(new SlotState { Unlocked = slot.unlockedAtStart });
            }
        }
    }
}
