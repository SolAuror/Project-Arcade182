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
        StasisSigil
    }

    /// <summary>
    /// One reward card offered after a stage clear.
    /// </summary>
    public class LabyrinthUpgrade
    {
        public LabyrinthUpgradeKind Kind;

        /// <summary>Spell slot this card targets; -1 for player-stat cards.</summary>
        public int SpellSlot = -1;

        public string Title;
        public string Description;
    }
}
