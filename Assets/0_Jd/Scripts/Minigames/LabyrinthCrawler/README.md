# Labyrinth Crawler

First-person spellcasting roguelike. A stopwatch runs while you fight through regenerating dungeon mazes — reach each stage's exit pad, pick a boon, and dive into a bigger maze. Death ends the run; your score converts to tickets back in the hub.

**Scene:** `Sc_LabyrinthCrawler` · **Manager:** `LabyrinthCrawlerGame` (prefab `Prefabs/Minigames/LabyrinthCrawler/LabyrinthCrawlerGame.prefab`)

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

## Key scripts (this folder)

`LabyrinthCrawlerGame` (orchestrator + audio hooks) · `EnemyController` (wander/chase/cast/knockback) · `PlayerSpellInput` · `LabyrinthExitPad` · `LabyrinthUpgrade` / `LabyrinthUpgradeSystem` / `LabyrinthUpgradeScreen` · `LabyrinthHud`

Shared framework: `Health`, `Mana`, `SpellCaster`, spell definitions, `HitFlash`, `DamagePopup`, `SpellBurstVisual`, `PlayerHitFeedback` (red hit flash + low-health heartbeat) — see [SCRIPTS.md](../../../../../SCRIPTS.md).

## Tuning quick-reference (inspector)

- `LabyrinthMazeRules` → starting size, growth, enemy counts.
- `fallRespawnY` → fall-out respawn height.
- Par/score fields under the Score header.
- Audio clip slots under the Audio header (silent until assigned).
