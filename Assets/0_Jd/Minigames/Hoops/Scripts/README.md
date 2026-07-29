# Hoops

Physical throwing range. Grab balls (and any scorable props lying around) with the phys-grab system and sink them into the glowing target hoop. **60 seconds, no score cap** — the round simply ends at time and your score converts to tickets.

**Scene:** `Sc_Hoops` · **Manager:** `HoopsGame` (prefab `Prefabs/HoopsManager.prefab`)

## Court

Three hoop posts of different sizes, each a pole + backboard + rim assembly with a `HoopsScoreZone`:

| Hoop | Size points |
|---|---|
| Small | 3 |
| Medium | 2 |
| Large | 1 |

Only one hoop is the **active target** at a time (it glows and breathes toward white; the HUD names it). Scoring on it picks a new target. Press the reset key (`Hoops/ResetBall`) to return balls to their spawns — and anything that leaves the arena bounds (balls over the fence, props in the void) walks back to its spawn automatically after 2 seconds, unless you're holding it. The bounds box is on `HoopsGame` (`arenaCenter`/`arenaSize`, gizmo-visible).

## Difficulty stages (by successful goals)

| Goals | Stage | Hoop behavior |
|---|---|---|
| 0–2 | 1 · static | Hoops hold still. |
| 3–5 | 2 · SLIDING | Active hoop slides on one axis — up/down its pole or left/right along its backboard. Never toward you, so no clipping. |
| 6+ | 3 · WILD | Both axes at once (wandering loop), **and** each new target has a 20% chance to go **WINGED** — hoop + backboard lift off the pole and bob in the air for double points. |

## Scoring

`score = item points × hoop difficulty × streak multiplier`

- **Item points:** balls are worth 1 (`HoopsThrowable.points`); props bring their own value (`HoopsScorable.points`).
- **Hoop difficulty:** size points × movement stage (1/2/3), ×2 more when winged.
- **Streak:** consecutive goals within a 10s window step the multiplier up to ×4.

Tickets: 1 per point via `PlayerScoreCarrier`.

## Key scripts (this folder)

`HoopsGame` (round/stages/scoring + audio hooks) · `HoopsScoreZone` (movement modes + score feedback) · `HoopsThrowable` (ball physics, flight trail, impact squash) · `HoopsScorable` (prop values) · `HoopsHud` (score, pulsing countdown, target line with stage tag, streak meter)

## Tuning quick-reference (inspector)

- `roundSeconds`, `slideAtGoals`, `dualSlideAtGoals`, `wingedFromGoals`, `wingedHoopChance`, `wingedBonusMultiplier` on `HoopsGame`.
- Per-hoop `points`, `poleTravel`, `backboardTravel`, `moveSpeed` on each `HoopsScoreZone`.
- Streak window/steps under the Streak header; audio clip slots under Audio (silent until assigned).
