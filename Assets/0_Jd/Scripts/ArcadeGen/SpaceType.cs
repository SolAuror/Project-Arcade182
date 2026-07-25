namespace Sol
{
    /// <summary>
    /// How the generator classifies a single maze cell once carving and every
    /// post-carve pass are done. This is the explicit vocabulary behind the loose
    /// "streets vs buildings" language: gameplay systems (audio reverb, lighting,
    /// fog, minimap tinting, encounter rules) can ask "am I on a street, in a
    /// plaza, or indoors?" straight from one value instead of re-deriving it from
    /// the generator's raw active/pit/block/authored/plaza masks.
    ///
    /// <para>The categories are mutually exclusive and cover every cell. On the hub
    /// lane (no pits, buildings, or plazas) every instantiated cell resolves to
    /// <see cref="NarrowStreet"/>.</para>
    /// </summary>
    public enum SpaceType
    {
        /// <summary>Cell is not part of the level - outside the organic footprint, so no room is instantiated. The default before generation runs.</summary>
        None = 0,

        /// <summary>Walkable single-width corridor: the common exterior lane between buildings. The classic all-corridor maze is entirely narrow streets.</summary>
        NarrowStreet = 1,

        /// <summary>Walkable open-air square - a rare, deliberately widened exterior region carved by the plaza pass. Exterior, roofless.</summary>
        Plaza = 2,

        /// <summary>Walkable interior of a procedural building's open hall, reached through its single entrance. Indoors.</summary>
        BuildingInterior = 3,

        /// <summary>Building mass the walkable graph routes around: an authored building's footprint (its interior is prefab-owned and opaque to the generator), or any non-walkable building cell. Exterior massing.</summary>
        SolidBuilding = 4,

        /// <summary>A walkable-graph hole the maze carves around and reveals as a void.</summary>
        Pit = 5,
    }
}
