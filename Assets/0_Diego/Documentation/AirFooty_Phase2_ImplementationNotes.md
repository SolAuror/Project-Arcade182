# Air Footy AI, modes and arcade integration

**Original game:** Diego<br>
**Update:** JD<br>
**Revised:** 1 August 2026

## Head-to-head mode

The 2-player prefab is one human against one AI. The human may choose Blue or Red; the broadcast camera and score labels rotate to match that choice. The first team to score five goals wins.

`AIPlayer3D` uses a six-state decision loop. It predicts defensive intercepts, selects a near-post, far-post or bank lane, moves behind the ball, telegraphs the plan, strikes through the shared motor and recovers. Its reactive pulse can save a fast incoming threat, and its dash spends the same charge resource used by the player. Full pseudocode and the state graph are in [AirFooty_AI.md](AirFooty_AI.md).

## Four-team mode

The 4-player prefab supports Blue, Red, Green and Gold around a square arena. It uses two balls and an elimination rule: five goals conceded removes that team; the final active team wins.

I use `AirFootySessionConfig` to carry the selected mode and human team from the menu. `GameManager3D` then:

1. discovers active goals, balls and team members;
2. assigns the selected team to player input;
3. assigns `AirFootySideAI3D` to the remaining teams;
4. constrains each striker to its team semicircle;
5. tracks goals conceded and elimination independently;
6. disables an eliminated team's goal and striker objects;
7. records the human result when one team remains.

The side AI is intentionally lighter than the head-to-head planner. With two balls and three opponents, it scores threats by approach toward its own goal, chooses a reachable ball, positions behind it, selects an opponent goal and strikes when in range. This keeps four-team play challenging without running four copies of the more expensive lane-construction loop.

## Overarching system integration

- `MainMenuUI` uses authored mode/team panels, selects the matching prefab and prepares the HUD. Runtime construction is now only a fallback for older scenes.
- `GameManager3D` records score and tickets through `PlayerScoreCarrier`.
- The shared pause menu owns resume and quit flow.
- Arcade cabinets launch the Air Footy scene through the shared launcher.
- Match completion returns to the configured hub scene.
- Team-aware crowd, arena light, goal, jumbotron and camera systems observe match state without owning it.

## Current limitations

- Four-team side AI favours clarity and threat response over the head-to-head AI's bank-shot planning.
- Final difficulty values still require hands-on observation across all four human perspectives.
- Standalone sessions cannot persist progress when no `PlayerScoreCarrier` is present; this is expected and logged once.
