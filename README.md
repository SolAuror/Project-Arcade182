# Insert Coin to Exit

**Unity:** 6000.0.76f1<br>
**Documentation revision:** 1 August 2026

Insert Coin to Exit is a shared arcade project by Finn, Diego and JD. I use a regenerating arcade maze as the frame for several short games: I find a cabinet, play it, bank tickets, return to a changed hub, and work towards the Golden Coin and exit door.

## Current games

| Game | Original owner | Current state |
|---|---|---|
| Air Footy | Diego | Diego's 3D arcade game, updated by me (JD) with deliberate pulse/dash play, stronger AI, clearer player feedback and FX, a challenging four-team/two-ball mode, and integration with the shared arcade systems. |
| Labyrinth Crawler | JD | Regenerating first-person dungeon stages, spell combat, three enemy movement archetypes, upgrades, secret walls, a PS1 presentation layer, and storm lighting. |
| Atom Smasher | JD | A 2D pinball-launcher game with escalating targets, obstructions and hazards. |
| Hoops | JD | A timed physical shooting game with moving and bonus hoops. |
| Fungus Pachinko | Finn | A five-ball pachinko game built around clearing board lights. |
| Neon Reflex | Diego | A reaction game based on selecting spawned targets quickly. |

Air Footy remains Diego's game. I treated his arena, rules and visual direction as the frame for my changes, not as a replaceable mass. My work extends the way the original game reads and plays while keeping its authorship visible in the folder structure and documentation.

## Main loop

1. I enter the arcade hub and explore the generated layout.
2. I use a cabinet to launch a minigame.
3. The result records score, tickets and unlock progress through `PlayerScoreCarrier`.
4. Returning to the hub creates a fresh maze layout.
5. The exit clerk sells the Golden Coin for 1,000,000 tickets; the Golden Exit Door consumes it to finish the game.

## Project layout

- `Assets/0_Diego/` contains Diego's games and Air Footy documentation.
- `Assets/0_Finn/` contains Finn's work.
- `Assets/0_Jd/` contains my games, hub systems, shared controller and editor tooling.
- `Assets/Shared/` contains scenes, input and assets shared at runtime.
- [SCRIPTS.md](SCRIPTS.md) is the code index.
- [Air Footy documentation](Assets/0_Diego/Documentation/README.md) covers ownership, modes, AI and integration.
- [Labyrinth Crawler documentation](Assets/0_Jd/Minigames/LabyrinthCrawler/Documentation/README.md) covers its AI, shaders and runtime asset policy.
- [Runtime asset audit](Documentation/RUNTIME_ASSET_AUDIT.md) records what is prefab-authored and what remains deliberately procedural.

## Build scenes

The normal route is `Sc_MainMenu` to `Sc_ArcadeHub`, then into a minigame scene. Air Footy selects its 2-player or 4-player prefab from `AirFootyFinal`; the other current JD games use their own `Sc_*` scenes.

## Working rule

I keep source prefabs, scenes and materials under version control. Runtime construction is reserved for state that actually changes during play: maze layouts, spawned enemies, projectiles, one-shot FX, temporary UI messages and resolution-dependent render targets. Fixed presentation objects belong in prefabs.
