# Labyrinth Crawler

First-person spellcasting survival crawler built for Unity 6. The run has no final floor: clear an increasingly hostile generated maze, choose an upgrade, and continue until death. Score is recorded through `PlayerScoreCarrier` and converted to hub tickets.

**Scene:** `Assets/0_Jd/Scenes/Sc_LabyrinthCrawler.unity`

**Root prefab:** `Assets/0_Jd/Minigames/LabyrinthCrawler/LabyrinthCrawlerGame.prefab`

**Run orchestrator:** `LabyrinthCrawlerGame`

## Runtime ownership

| Owner | Responsibility |
|---|---|
| `LabyrinthCrawlerGame` | Run state, timer, stage scaling, score, player combat setup, enemy/exit/secret spawning, upgrades, death, and scene flow. |
| `ArcadeGen3D` | Shared grid topology, room instantiation, obstacle-safe generation, wall openings, space classification, and building materialization. The hub uses the same generator through a rules-null compatibility lane. |
| `LabyrinthSecretPass` | Labyrinth-only post-generation secret sites, shortest-path analysis, illusory plugs, and caches. |
| `EnemyController` | Graph patrol, perception, chase/tracking, casting, knockback, pit death, and enemy audio. |
| `SpellCaster` and spell definitions | Shared mana, cooldown, damage, projectile, hitscan, and area-spell behavior. |
| `RetroPresenter` | Camera target, point-filtered presentation, fog, skybox instance, retro shader globals, and restoration. |
| `StormDirector` | Lightning envelope, bolt, directional flash, flat ambient override, thunder, and the local-light shadow budget. |

Keep global render-state ownership split between `RetroPresenter` and `StormDirector`; both restore what they change when disabled.

## Current authored stage rules

These values reflect `LabyrinthCrawlerGame.prefab`, not just the C# field defaults.

- Floor 1 starts from a 5×4 grid. Width and depth each grow by 1 after a normal clear.
- The footprint is a connected organic blob targeting 65% of the grid.
- Recursive-backtracking corridors are braided at 35% of eligible dead ends.
- Pits start at 2 and add 2 per floor. They are reserved before carving.
- One plaza is requested per floor, sized 2×2 to 3×3.
- Procedural buildings start at 0 and add 1 per floor. Bounds are 1×1 to 2×2, height is capped at 3 full cells, and each requests one street entrance.
- Once a procedural building exists, the exit has a 35% chance to move into one of its deeper cells.
- Enemies use `2 + 2 × (floor - 1) + floor((floor - 1) / 3)`.
- The score multiplier is the current floor number.

`Stasis Sigil` consumes the next size increase. Other floor-scaled counts still use the new floor number, so it freezes space rather than overall difficulty.

## Maze-generation algorithms

Generation is deliberately staged so hazards add route length without making a floor unwinnable:

1. **Special cells** — select the start and provisional exit. The crawler uses a center start and a far active exit.
2. **Connected organic footprint** — grow a randomized frontier from the center until the requested fill is reached; choose the farthest active cell as the exit.
3. **Obstacle-first reservation** — choose pits and planned building footprints before corridor carving. Each placement is accepted only if a breadth-first connectivity guard confirms the remaining walkable cells are still one connected region.
4. **Building planning** — `BuildingPlanUtility` creates a seeded organic footprint, column heights, clustered half-storeys, roof types, and entrances. A valid building may become the exit destination.
5. **Maze carve** — iterative depth-first recursive backtracking uses a stack to create a spanning tree over walkable cells.
6. **Post-carve topology** — braid eligible dead ends into loops, open plazas, reveal/conjoin pit shafts, open procedural building halls and entrances, and place authored buildings.
7. **Classification and dressing** — resolve each active cell to `NarrowStreet`, `Plaza`, `BuildingInterior`, `SolidBuilding`, or `Pit`; then dress walls, instantiate planned upper floors/roofs, and run a development-build exit-reachability assertion.
8. **Crawler content** — respawn the player, configure the stand-on exit, spawn enemies away from the start, then place secrets.

The hub lane leaves crawler-only inputs at inert values and avoids extra `UnityEngine.Random` draws, preserving the hub generator's historical sequence.

## Secret-site algorithms

`LabyrinthSecretPass` examines the final open-door graph:

- **Treasure room:** a non-start/non-exit leaf with exactly one open boundary.
- **Shortcut:** a closed adjacent boundary whose existing open-graph route is at least 4 edges long. Opening it creates a meaningful loop.
- **Exit blocker:** an edge selected from the breadth-first shortest path between start and exit, excluding the configured endpoint buffer.

Sites are shuffled, de-duplicated by a value-type edge key, kept at least 2 Manhattan cells apart, and capped at 3 illusions after floor bonuses. At most one treasure room is rewarded per floor. Pathfinding permits opened procedural-building interiors, while secret shortcut candidates still reject pits and sealed building mass.

An illusory wall copies the selected `WallSocket` model when possible, keeps a solid collider for enemies/spells, ignores player collisions, emits shader ripples on contact, and reveals only after the player crosses its mid-plane. The cube visual is a fallback for legacy wall kits without a compatible socket.

## Enemy algorithm

Enemies use a small finite-state controller rather than a NavMesh:

- **Patrol:** choose an open neighboring room, avoid immediate backtracking, and revalidate the doorway with physics so an illusion is not revealed by AI pathing.
- **Detect/chase:** require both range and line of sight; turn and close distance until attack range.
- **Attack:** cast shared `SpellCaster` slot 0 toward the player's configured target height.
- **Track:** pursue the last seen position for a fixed time after line of sight breaks.
- **Wander fallback:** drift only when no maze graph is available.
- **Knockback/death:** pulse impulse temporarily overrides locomotion; crossing the enemy pit-kill plane routes through normal death and score reporting.

The controller caches its current patrol room and reuses its four-entry option list. A full nearest-room scan now occurs only when combat or knockback invalidates that cache.

## Exit, falls, and upgrades

- The exit pad clears immediately when no enemies remain. With live enemies it requires an interruptible 1.5-second dwell.
- The portal beacon appears when the floor is clear; `Cartographer` also reveals it after 30 seconds while enemies remain.
- Player falls below y = -10 return to the start room. Current-health damage escalates through 25%, 25%, 50%, then 99% for subsequent falls.
- Each fall has a 3.5% chance to advance one floor without damage, time bonus, or an upgrade pick.
- Upgrade drafts use weighted random selection without replacement. Locked-spell cards take priority; other cards are gated by current components and stack caps.
- Banked secret-cache boons are spent as extra picks during the next already-paused upgrade draft.

## Spells

| Input slot | Spell | Authored base behavior |
|---|---|---|
| Attack | Laser | 5 damage, 2.5 mana, 0.1-second cooldown, 30-unit hitscan, continuous while held. Can destroy hostile projectiles. |
| Cast | Fireball | 25 damage, 15 mana, 0.5-second cooldown, 22-unit/s projectile. |
| Pulse | Pulse | 30 damage, 25 mana, 1.2-second cooldown, 5-unit radius, knockback 10. Reflects hostile projectiles. |

Only the first configured spell starts unlocked. Runtime upgrades modify the `SpellCaster` slot state rather than the ScriptableObject assets.

## Scoring and persistence

- **Kill:** `25 × current floor multiplier`.
- **Floor time bonus:** `max(0, round((par - clear time) × 10))`.
- **Par:** `20 + 0.8 × max(0, current grid area - 20)` seconds. This uses the configured grid bounds, not the randomized active-cell count.
- **Secret room:** `100 × multiplier`.
- **Secret shortcut:** `50 × multiplier`.
- **Exit-path blocker:** no score; it is an obstacle, not a discovery.
- **Hoard cache:** `500 × multiplier`.
- **Tickets:** 0.1 ticket per final score point.

There is no separate death bonus. Older documentation that described `kills × stages × 5` is obsolete.

`PlayerScoreCarrier` is authoritative. The `TimedMazeEscape.LastScore` and `TimedMazeEscape.BestScore` PlayerPrefs keys remain only as migration inputs for older saves.

## Presentation

- `RetroPresenter` targets 240 vertical lines, point filtering, the `Arcade/PS1/Present` pass, vertex-snap globals, exponential olive fog, and a runtime clone of `M_StormSky`.
- `StormDirector` uses its own `System.Random` so weather does not perturb maze/gameplay randomness.
- The storm temporarily grants hard shadows only to the nearest active point lights (two in the authored prefab); every light appears once in the budget list and its authored shadow mode is restored on disable.
- `PS1Lit.shader` and `PS1IllusoryWall.shader` share `Shaders/LabyrinthPS1Lighting.hlsl`.

## Compatibility surfaces

- `LabyrinthCrawlerGame.CompleteEscape()` remains as a compatibility entry point for `MazeExitInteractable`; generated crawler exits disable the clerk and use `LabyrinthExitPad`.
- The generated `InputSystem_Actions.cs` wrapper is authoritative. Do not hand-edit it. The removed direct Q/middle-mouse pulse fallback was unreachable once the `LabyrinthCrawler/Pulse` action shipped.
- The predecessor per-cell roof/massing implementation is retained under `#if false` in `ArcadeGen3D` for short-term comparison. Runtime and editor assemblies use `BuildingComponent` exclusively.
- Old serialized massing keys may remain in existing prefab YAML until Unity next saves those prefabs; no active code reads them.

## Authoring and validation

- `Sol → Labyrinth Crawler → Author Storm Sky Scaffold` repairs storm assets and prefab wiring while preserving existing tuning.
- `Sol → Labyrinth Crawler → Validate Storm Sky Render` writes `StormSkyValidation.png` to the outer project directory.
- Relevant generated-maze verification should include multiple seeds, exit reachability without crossing pits, indoor exits, illusion placement, enemy pit deaths, upgrade pause/resume, and render-state restoration on scene exit.
