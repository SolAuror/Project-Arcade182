# Labyrinth Crawler documentation

**Maintainer:** JD<br>
**Revised:** 1 August 2026

Labyrinth Crawler is my regenerating first-person spell-combat game. The maze layout is intentionally built at runtime; stable presentation, enemies, spells, UI and room parts are authored as assets.

- [Enemy AI, pseudocode and state graph](AI.md)
- [Storm sky, PS1 and ripple shaders](Shaders.md)
- [Runtime and prefab asset boundary](RuntimeAssets.md)
- [Script-level overview](../Scripts/README.md)

## Runtime loop

1. Generate a connected maze from authored room prefabs.
2. Place the player, exit, enemies and secrets using the generated graph.
3. Let the player clear enemies or risk an interruptible early exit.
4. Score the stage, offer one of three upgrades and grow the next layout.
5. End the run on player death and record progress through the shared arcade systems.
