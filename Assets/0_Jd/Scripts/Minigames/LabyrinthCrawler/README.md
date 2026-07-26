# Labyrinth Crawler

First-person spellcasting roguelike. A stopwatch runs while you fight through regenerating dungeon mazes — reach each stage's exit pad, pick a boon, and dive into a bigger maze. Death ends the run; your score converts to tickets back in the hub.

**Scene:** `Sc_LabyrinthCrawler` · **Manager:** `LabyrinthCrawlerGame` (prefab `Assets/0_Jd/Minigames/LabyrinthCrawler/LabyrinthCrawlerGame.prefab`)

## Run structure

- Stage 1 starts as a 3×3 maze (`ArcadeGen3D` with dungeon room prefabs); each cleared stage grows the maze by +1 in both axes.
- Enemy packs scale with stage: 2 base, +1 per stage, plus a bonus enemy every 2 stages (`LabyrinthMazeRules`). Enemies wander their rooms until they spot you (range + line of sight), then chase and cast.
- The **exit pad** spawns in the end room. Standing on it clears the stage instantly when every enemy is dead, otherwise after an interruptible 1.5s dwell. The pad breathes when idle, beckons when the stage is clearable, and ramps green→white while you channel.
- Between stages: choose **1 of 3 upgrades** (`LabyrinthUpgradeSystem`) — spell unlocks/empowers, cooldown cuts, radius, vitals. Time is frozen while choosing.
- Fall out of the map? Below y = −5 you're teleported back to the stage start room.

## Spells (SpellCaster slots)

| Slot | Spell | Behavior |
|---|---|---|
| Attack | **Laser** (`Spell_Laser`) | Sustained hold-to-fire hitscan beam (~50 DPS, ~25 mana/s). Passes through your own fireballs; **shoots enemy projectiles out of the air**. |
| Cast | **Fireball** (`Spell_Fireball`) | Projectile burst damage. Friendly projectiles never collide with each other; opposing projectiles can intercept. |
| Pulse | **Pulse** (`Spell_Pulse`) | Radial blast around you: damage + **knockback crowd control** (staggers enemies) with an expanding shockwave — and **reflects enemy projectiles**, sending them back as your own shots. Your escape button when packs swarm. |

Spells unlock progressively (one at start; upgrades unlock the rest). Casting without mana flashes the mana bar red.

## Scoring

- **Stage clear:** `(par − clearTime) × 10` points; par = 20s + 6s per stage.
- **Run end:** bonus of `kills × stages cleared × 5`.
- Tickets: 0.1 per point via `PlayerScoreCarrier`.

## Presentation and storm lighting

- `RetroPresenter` renders the gameplay camera to a 240-line, point-filtered target and applies the `Arcade/PS1/Present` posterize/dither pass. Overlay UI remains at native resolution.
- The canonical sky material is `GameMaterials/M_StormSky.mat`, using `Shaders/StormSky.shader` plus the cloud, skyline, and entity-mask textures in `GameMaterials/Textures/`. The material is cloned at runtime so a lightning pulse never dirties the authored asset.
- `StormDirector` creates distant, world-space lightning: a short-lived bolt, a directional light, sky illumination, entity reveal, fog lift, and a shared directional flash for the PS1 shaders. The pulse is intentionally a burst of light rather than a hard strobe.
- The dungeon uses flat olive ambient light so unlit corridors retain readable silhouettes. Lit-room point lights provide local spill; `StormDirector` grants realtime hard shadows only to the nearest two active point lights and refreshes the selection as the player moves. Room prefabs keep point-light shadows disabled in their serialized state.
- `PS1Lit.shader` and `PS1IllusoryWall.shader` share `Shaders/LabyrinthPS1Lighting.hlsl`: ambient and the main directional light are evaluated per vertex, while local additional lights and their shadows are evaluated per fragment. Both URP Forward and Forward+ paths are supported, so normal and illusory walls receive the same light spill.
- Additional-light shadows are enabled in the mobile URP asset to keep the lighting model consistent between quality tiers.

`RetroPresenter` owns the scene skybox, fog, camera target, retro shader globals, and their restoration. `StormDirector` owns the storm envelope, flat ambient override, local shadow budget, and their restoration. Keep those responsibilities separate when extending the effect so state does not leak back into the arcade hub.

## Storm authoring workflow

- Run `Sol → Labyrinth Crawler → Author Storm Sky Scaffold` to repair or install the authored assets and prefab wiring. It is safe to rerun: existing textures, material tuning, storm settings, and thunder assignments are preserved.
- Run `Sol → Labyrinth Crawler → Validate Storm Sky Render` for a quick offscreen sky check. It writes `StormSkyValidation.png` to the parent project folder, outside the repository.
- `T_EntitySilhouette.png` is blockout art intended to be replaced. The scaffold only generates it when the texture is missing, so replacement art will not be overwritten.
- Thunder clips and final sky/entity artwork are deferred polish; the visual storm works without assigned audio.

## Key scripts (this folder)

`LabyrinthCrawlerGame` (run orchestrator) · `RetroPresenter` (low-resolution presentation, fog, and sky lifecycle) · `StormDirector` (lightning and shadow budget) · `EnemyController` (wander/chase/cast/knockback) · `PlayerSpellInput` · `LabyrinthExitPad` · `LabyrinthUpgrade` / `LabyrinthUpgradeSystem` / `LabyrinthUpgradeScreen` · `LabyrinthHud`

Shared framework: `Health`, `Mana`, `SpellCaster`, spell definitions, `HitFlash`, `DamagePopup`, `SpellBurstVisual`, `PlayerHitFeedback` (red hit flash + low-health heartbeat) — see [SCRIPTS.md](../../../../../SCRIPTS.md).

## Tuning quick-reference (inspector)

- `LabyrinthMazeRules` → starting size, growth, enemy counts.
- `fallRespawnY` → fall-out respawn height.
- Par/score fields under the Score header.
- Audio clip slots under the Audio header (silent until assigned).
- `RetroPresenter` → target resolution, vertex-snap scale, fog color/density, and fog response to lightning.
- `StormDirector` → strike interval and pulse shape, world-space bolt placement, flash intensity, ambient color, and maximum shadowed point lights.
- `M_StormSky` → cloud motion/contrast, palette, skyline, entity placement, and entity visibility.
