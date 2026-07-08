namespace Sol.Minigames
{
    /// <summary>
    /// Combat allegiance shared by <see cref="Health"/>, <see cref="Projectile"/>, and the spell framework.
    /// Minigame-agnostic: any minigame can reuse it.
    /// </summary>
    public enum Faction
    {
        Player = 0,
        Enemy = 1,
        Neutral = 2
    }
}
