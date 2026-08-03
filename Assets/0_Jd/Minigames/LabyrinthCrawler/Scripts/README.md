# Labyrinth Crawler code overview

**Maintainer:** JD<br>
**Revised:** 1 August 2026

Labyrinth Crawler builds each stage from authored maze pieces, spawns a growing enemy pack and asks me to reach the exit while managing health, mana, spell cooldowns and secrets. Between cleared stages I choose one of three upgrades; death records the run and returns through the shared arcade flow.

## Main ownership

- `LabyrinthCrawlerGame` owns the run, generated stage, enemies, score, exit and upgrade transition.
- `ArcadeGen3D` in `Assets/0_Jd/Scripts/LevelGenerator/` owns maze generation.
- `EnemyController` owns perception and movement policy; `SpellCaster` owns spell legality and cooldown.
- `LabyrinthSecretPass` reads the generated graph and places shortcuts, blockers and caches.
- `RetroPresenter` owns low-resolution presentation, fog and temporary material instances.
- `StormDirector` owns storm timing and drives the sky, bolt, fog and light response.

## Detailed documents

- [Documentation index](../Documentation/README.md)
- [Enemy AI and state graph](../Documentation/AI.md)
- [Shader diagrams and pseudocode](../Documentation/Shaders.md)
- [Runtime asset boundary](../Documentation/RuntimeAssets.md)

## Authoring boundary

Rooms, enemies, spells, UI, stable VFX and materials are prefabs/assets. The maze instance, placements, stage enemies, projectiles, transient FX, ripple samples and resolution-dependent render target are runtime state by design.
