using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Sol.Minigames
{
    /// <summary>
    /// Builds valid 1-of-N reward choices each stage and applies the pick to the
    /// player's <see cref="SpellCaster"/>, <see cref="Health"/>, and <see cref="Mana"/>.
    /// Unlock cards take priority while any spell slot is still locked; empower
    /// cards are only offered for unlocked spells.
    /// </summary>
    [Serializable]
    public class LabyrinthUpgradeSystem
    {
        [Header("Upgrade Steps")]
        [Tooltip("Spell damage increase per card (0.25 = +25%).")]
        [SerializeField, Min(0f)] private float damagePercentStep = 0.25f;

        [Tooltip("Spell cooldown reduction per card (0.2 = -20%).")]
        [SerializeField, Range(0f, 0.9f)] private float cooldownPercentStep = 0.2f;

        [Tooltip("Flat radius added per card (area spells only).")]
        [SerializeField, Min(0f)] private float radiusStep = 1f;

        [Tooltip("Max health added per card.")]
        [SerializeField, Min(0f)] private float maxHealthStep = 25f;

        [Tooltip("Max mana added per card.")]
        [SerializeField, Min(0f)] private float maxManaStep = 25f;

        [Tooltip("Mana regen per second added per card.")]
        [SerializeField, Min(0f)] private float manaRegenStep = 2f;

        private SpellCaster caster;
        private Health health;
        private Mana mana;

        public void Bind(SpellCaster playerCaster, Health playerHealth, Mana playerMana)
        {
            caster = playerCaster;
            health = playerHealth;
            mana = playerMana;
        }

        public List<LabyrinthUpgrade> BuildChoices(int count = 3)
        {
            List<LabyrinthUpgrade> unlockCards = new List<LabyrinthUpgrade>();
            List<LabyrinthUpgrade> pool = new List<LabyrinthUpgrade>();

            if (caster != null)
            {
                for (int slot = 0; slot < caster.SlotCount; slot++)
                {
                    SpellDefinition definition = caster.GetDefinition(slot);
                    if (definition == null)
                    {
                        continue;
                    }

                    if (!caster.IsUnlocked(slot))
                    {
                        unlockCards.Add(new LabyrinthUpgrade
                        {
                            Kind = LabyrinthUpgradeKind.UnlockSpell,
                            SpellSlot = slot,
                            Title = $"Unlock {definition.DisplayName}",
                            Description = $"Learn to cast {definition.DisplayName}."
                        });
                        continue;
                    }

                    pool.Add(new LabyrinthUpgrade
                    {
                        Kind = LabyrinthUpgradeKind.SpellDamage,
                        SpellSlot = slot,
                        Title = $"{definition.DisplayName} +Damage",
                        Description = $"{definition.DisplayName} deals {Mathf.RoundToInt(damagePercentStep * 100f)}% more damage."
                    });

                    if (definition.CooldownSeconds > 0f)
                    {
                        pool.Add(new LabyrinthUpgrade
                        {
                            Kind = LabyrinthUpgradeKind.SpellCooldown,
                            SpellSlot = slot,
                            Title = $"{definition.DisplayName} +Haste",
                            Description = $"{definition.DisplayName} cools down {Mathf.RoundToInt(cooldownPercentStep * 100f)}% faster."
                        });
                    }

                    if (definition is AoeSpellDefinition)
                    {
                        pool.Add(new LabyrinthUpgrade
                        {
                            Kind = LabyrinthUpgradeKind.SpellRadius,
                            SpellSlot = slot,
                            Title = $"{definition.DisplayName} +Radius",
                            Description = $"{definition.DisplayName} blast radius grows by {radiusStep:0.#}."
                        });
                    }
                }
            }

            if (health != null)
            {
                pool.Add(new LabyrinthUpgrade
                {
                    Kind = LabyrinthUpgradeKind.MaxHealth,
                    Title = "Fortitude",
                    Description = $"+{maxHealthStep:0} max health (and heal that much)."
                });
            }

            if (mana != null)
            {
                pool.Add(new LabyrinthUpgrade
                {
                    Kind = LabyrinthUpgradeKind.MaxMana,
                    Title = "Deep Well",
                    Description = $"+{maxManaStep:0} max mana (and restore that much)."
                });

                pool.Add(new LabyrinthUpgrade
                {
                    Kind = LabyrinthUpgradeKind.ManaRegen,
                    Title = "Flowing Focus",
                    Description = $"+{manaRegenStep:0.#} mana regenerated per second."
                });
            }

            Shuffle(unlockCards);
            Shuffle(pool);

            List<LabyrinthUpgrade> choices = new List<LabyrinthUpgrade>(count);
            foreach (LabyrinthUpgrade unlock in unlockCards)
            {
                if (choices.Count < count)
                {
                    choices.Add(unlock);
                }
            }

            foreach (LabyrinthUpgrade candidate in pool)
            {
                if (choices.Count < count)
                {
                    choices.Add(candidate);
                }
            }

            return choices;
        }

        public void Apply(LabyrinthUpgrade upgrade)
        {
            if (upgrade == null)
            {
                return;
            }

            switch (upgrade.Kind)
            {
                case LabyrinthUpgradeKind.UnlockSpell:
                    caster?.Unlock(upgrade.SpellSlot);
                    break;
                case LabyrinthUpgradeKind.SpellDamage:
                    caster?.EmpowerDamage(upgrade.SpellSlot, damagePercentStep);
                    break;
                case LabyrinthUpgradeKind.SpellCooldown:
                    caster?.ReduceCooldown(upgrade.SpellSlot, cooldownPercentStep);
                    break;
                case LabyrinthUpgradeKind.SpellRadius:
                    caster?.AddRadius(upgrade.SpellSlot, radiusStep);
                    break;
                case LabyrinthUpgradeKind.MaxHealth:
                    health?.IncreaseMax(maxHealthStep);
                    break;
                case LabyrinthUpgradeKind.MaxMana:
                    mana?.IncreaseMax(maxManaStep);
                    break;
                case LabyrinthUpgradeKind.ManaRegen:
                    if (mana != null)
                    {
                        mana.RegenPerSecond += manaRegenStep;
                    }

                    break;
            }
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
