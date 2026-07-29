namespace Sol.Minigames
{
    public enum LabyrinthUpgradeKind
    {
        UnlockSpell,
        SpellDamage,
        SpellCooldown,
        SpellRadius,
        MaxHealth,
        MaxMana,
        ManaRegen,
        MoveSpeed,
        LifeOnKill,
        StasisSigil,
        ManaOnKill,
        Overcharge,
        SecondWind,
        Cartographer
    }

    /// <summary>
    /// One reward card offered after a stage clear. Adding a new card is three
    /// touches: a <see cref="LabyrinthUpgradeKind"/> value, an offer block in
    /// <see cref="LabyrinthUpgradeSystem.BuildChoices"/> (set <see cref="Weight"/>
    /// and any availability gate there), and an apply arm in
    /// <see cref="LabyrinthUpgradeSystem.Apply"/>.
    /// </summary>
    public class LabyrinthUpgrade
    {
        public LabyrinthUpgradeKind Kind;

        /// <summary>Spell slot this card targets; -1 for player-stat cards.</summary>
        public int SpellSlot = -1;

        public string Title;
        public string Description;

        /// <summary>
        /// Relative draft weight; higher shows more often. 1 is the common
        /// baseline. Drop below 1 for build-defining or run-swinging cards so a
        /// growing pool does not bury the staples under rare picks.
        /// </summary>
        public float Weight = 1f;
    }
}
