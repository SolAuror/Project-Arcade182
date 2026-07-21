using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Sol.Minigames
{
    /// <summary>
    /// Builds valid 1-of-N reward choices each stage and applies the pick to the
    /// player's <see cref="SpellCaster"/>, <see cref="Health"/>, <see cref="Mana"/>,
    /// and <see cref="Player.Controller"/>. Unlock cards take priority while any
    /// spell slot is still locked; empower cards are only offered for unlocked
    /// spells. Run-scoped picks (move speed stacks, life on kill, growth skips)
    /// live here and reset on <see cref="Bind"/>.
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

        [Tooltip("Move speed added per card as a fraction of base speed (0.08 = +8%). Stacks add, they do not compound.")]
        [SerializeField, Min(0f)] private float moveSpeedPercentStep = 0.08f;

        [Tooltip("Maximum move speed stacks; the card stops appearing at the cap.")]
        [SerializeField, Min(1)] private int moveSpeedMaxStacks = 5;

        [Tooltip("Health restored per enemy kill added per card.")]
        [SerializeField, Min(0f)] private float lifeOnKillStep = 5f;

        [Tooltip("Mana restored per enemy kill added per card.")]
        [SerializeField, Min(0f)] private float manaOnKillStep = 8f;

        [Tooltip("Mana cost reduction per card (0.15 = -15% on every spell). Clamped by the caster's floor.")]
        [SerializeField, Range(0f, 0.9f)] private float manaCostPercentStep = 0.15f;

        [Tooltip("Fraction of max health the player returns with when a Second Wind charge saves them.")]
        [SerializeField, Range(0.05f, 1f)] private float reviveHealthFraction = 0.35f;

        [Tooltip("How many Second Wind charges a single run may bank; the card stops appearing at the cap.")]
        [SerializeField, Min(1)] private int maxReviveCharges = 2;

        [Tooltip("With Cartographer, the exit beacon reveals this many seconds into a stage even while enemies live. Without it, the beacon only reveals once the room is cleared.")]
        [SerializeField, Min(1f)] private float cartographerRevealSeconds = 30f;

        private SpellCaster caster;
        private Health health;
        private Mana mana;
        private Player.Controller controller;
        private int moveSpeedStacks;
        private float lifeOnKillHeal;
        private float manaOnKillRestore;
        private int reviveChargesRemaining;
        private int reviveChargesGranted;
        private bool skipNextMazeGrowth;
        private bool hasCartographer;

        /// <summary>Health restored on each enemy kill (0 until Vampiric Pact is picked).</summary>
        public float LifeOnKillHeal => lifeOnKillHeal;

        /// <summary>Mana restored on each enemy kill (0 until Soul Siphon is picked).</summary>
        public float ManaOnKillRestore => manaOnKillRestore;

        /// <summary>Fraction of max health a Second Wind revive returns the player to.</summary>
        public float ReviveHealthFraction => reviveHealthFraction;

        /// <summary>
        /// Seconds into a stage after which the exit beacon reveals even with
        /// enemies alive; <see cref="float.PositiveInfinity"/> until Cartographer
        /// is picked, so the default rule (reveal on room clear) stands alone.
        /// </summary>
        public float ExitRevealAfterSeconds => hasCartographer ? cartographerRevealSeconds : float.PositiveInfinity;

        public void Bind(SpellCaster playerCaster, Health playerHealth, Mana playerMana, Player.Controller playerController)
        {
            caster = playerCaster;
            health = playerHealth;
            mana = playerMana;
            controller = playerController;

            // Bind marks a fresh run; clear all run-scoped upgrade state.
            moveSpeedStacks = 0;
            lifeOnKillHeal = 0f;
            manaOnKillRestore = 0f;
            reviveChargesRemaining = 0;
            reviveChargesGranted = 0;
            skipNextMazeGrowth = false;
            hasCartographer = false;
            if (controller != null)
            {
                controller.ExternalSpeedMultiplier = 1f;
            }

            caster?.ResetManaCostMultiplier();
        }

        /// <summary>
        /// Spends a banked Second Wind charge if one is available. The game calls
        /// this from its death handler to decide whether to revive instead of end.
        /// </summary>
        public bool TryConsumeReviveCharge()
        {
            if (reviveChargesRemaining <= 0)
            {
                return false;
            }

            reviveChargesRemaining--;
            return true;
        }

        /// <summary>
        /// True (once) when a Stasis Sigil pick should cancel the upcoming maze
        /// growth. The game calls this right before applying growth.
        /// </summary>
        public bool ConsumeMazeGrowthSkip()
        {
            bool skip = skipNextMazeGrowth;
            skipNextMazeGrowth = false;
            return skip;
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

                pool.Add(new LabyrinthUpgrade
                {
                    Kind = LabyrinthUpgradeKind.LifeOnKill,
                    Title = "Vampiric Pact",
                    Description = $"Restore {lifeOnKillStep:0.#} health on every kill.",
                    Weight = 0.8f
                });

                if (reviveChargesGranted < maxReviveCharges)
                {
                    pool.Add(new LabyrinthUpgrade
                    {
                        Kind = LabyrinthUpgradeKind.SecondWind,
                        Title = "Second Wind",
                        Description = $"A lethal blow instead leaves you at {Mathf.RoundToInt(reviveHealthFraction * 100f)}% health (one save).",
                        Weight = 0.4f
                    });
                }
            }

            if (controller != null && moveSpeedStacks < moveSpeedMaxStacks)
            {
                pool.Add(new LabyrinthUpgrade
                {
                    Kind = LabyrinthUpgradeKind.MoveSpeed,
                    Title = "Fleet Foot",
                    Description = $"Move {Mathf.RoundToInt(moveSpeedPercentStep * 100f)}% faster (stack {moveSpeedStacks + 1} of {moveSpeedMaxStacks})."
                });
            }

            pool.Add(new LabyrinthUpgrade
            {
                Kind = LabyrinthUpgradeKind.StasisSigil,
                Title = "Stasis Sigil",
                Description = "The next maze stays the same size (one use).",
                Weight = 0.7f
            });

            if (!hasCartographer)
            {
                pool.Add(new LabyrinthUpgrade
                {
                    Kind = LabyrinthUpgradeKind.Cartographer,
                    Title = "Cartographer",
                    Description = $"The exit portal also reveals after {Mathf.RoundToInt(cartographerRevealSeconds)}s, even before the room is cleared.",
                    Weight = 0.7f
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

                pool.Add(new LabyrinthUpgrade
                {
                    Kind = LabyrinthUpgradeKind.ManaOnKill,
                    Title = "Soul Siphon",
                    Description = $"Restore {manaOnKillStep:0.#} mana on every kill.",
                    Weight = 0.8f
                });

                // Only offer the discount while it can still bite (above the floor).
                if (caster != null && caster.ManaCostMultiplier > SpellCaster.MinManaCostMultiplier + 0.001f)
                {
                    pool.Add(new LabyrinthUpgrade
                    {
                        Kind = LabyrinthUpgradeKind.Overcharge,
                        Title = "Overcharge",
                        Description = $"Every spell costs {Mathf.RoundToInt(manaCostPercentStep * 100f)}% less mana.",
                        Weight = 0.8f
                    });
                }
            }

            List<LabyrinthUpgrade> choices = new List<LabyrinthUpgrade>(count);

            // Unlock cards are mandatory offers while a spell is still locked, so
            // they jump the queue; everything else is a weighted draw so a growing
            // pool can keep staples common and run-swinging cards rare.
            Shuffle(unlockCards);
            foreach (LabyrinthUpgrade unlock in unlockCards)
            {
                if (choices.Count < count)
                {
                    choices.Add(unlock);
                }
            }

            WeightedDrainInto(pool, choices, count);
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
                case LabyrinthUpgradeKind.MoveSpeed:
                    if (controller != null && moveSpeedStacks < moveSpeedMaxStacks)
                    {
                        moveSpeedStacks++;
                        controller.ExternalSpeedMultiplier = 1f + moveSpeedPercentStep * moveSpeedStacks;
                    }

                    break;
                case LabyrinthUpgradeKind.LifeOnKill:
                    lifeOnKillHeal += lifeOnKillStep;
                    break;
                case LabyrinthUpgradeKind.StasisSigil:
                    skipNextMazeGrowth = true;
                    break;
                case LabyrinthUpgradeKind.ManaOnKill:
                    manaOnKillRestore += manaOnKillStep;
                    break;
                case LabyrinthUpgradeKind.Overcharge:
                    caster?.ReduceManaCost(manaCostPercentStep);
                    break;
                case LabyrinthUpgradeKind.SecondWind:
                    reviveChargesRemaining++;
                    reviveChargesGranted++;
                    break;
                case LabyrinthUpgradeKind.Cartographer:
                    hasCartographer = true;
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

        /// <summary>
        /// Moves up to <paramref name="max"/> cards from <paramref name="source"/>
        /// into <paramref name="destination"/>, drawing without replacement with a
        /// probability proportional to each card's <see cref="LabyrinthUpgrade.Weight"/>.
        /// Zero/negative weights fall back to a uniform pick so a mis-set weight
        /// never silently drops a card from the pool.
        /// </summary>
        private static void WeightedDrainInto(List<LabyrinthUpgrade> source, List<LabyrinthUpgrade> destination, int max)
        {
            while (destination.Count < max && source.Count > 0)
            {
                float totalWeight = 0f;
                foreach (LabyrinthUpgrade candidate in source)
                {
                    totalWeight += Mathf.Max(0f, candidate.Weight);
                }

                int pickIndex;
                if (totalWeight <= 0f)
                {
                    pickIndex = Random.Range(0, source.Count);
                }
                else
                {
                    float roll = Random.value * totalWeight;
                    pickIndex = source.Count - 1;
                    for (int i = 0; i < source.Count; i++)
                    {
                        roll -= Mathf.Max(0f, source[i].Weight);
                        if (roll <= 0f)
                        {
                            pickIndex = i;
                            break;
                        }
                    }
                }

                destination.Add(source[pickIndex]);
                source.RemoveAt(pickIndex);
            }
        }
    }
}
