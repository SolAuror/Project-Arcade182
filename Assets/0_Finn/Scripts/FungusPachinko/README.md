# Fungus Pachinko Ball

Finn's pachinko machine, starring **Fungus** — a little white squirrel-looking creature perched on the dropper (placeholder spheres for now). Slide the dropper along the top of the board, drop a ball, and watch it rattle down through pegs and bumpers. Every **light** the ball passes through turns off for **+1 point**. You get **5 balls**; when they're gone your points convert straight to tickets.

**Scene:** `Sc_FungusPachinko` · **Manager:** `FungusGameController` (prefab `Assets/0_Finn/Prefabs/FungusPachinkoMachine.prefab`)

## Controls (action map `FungusPachinko`)

| Input | Action |
|---|---|
| `A` / `D` | Slide the dropper left/right along the rail |
| `Space` | Drop the ball |

Input is locked while a ball is in play — aim, drop, watch, repeat.

## Scoring

- **+1 point** per light turned off (a light only counts once).
- **Tickets: 1 per point** via the shared `PlayerScoreCarrier`.
- **All lights out = +50 bonus**, folded into the recorded score before the 1:1 conversion (a perfect 25-light board records 75 and pays 75 tickets). Clearing every light also ends the game immediately.

## Key scripts (this folder)

`FungusGameController` (round flow, payout, hub return) · `FungusDropper` (the only input reader — rail movement + DropRequested) · `FungusBall` (plane-locked physics, settle/lifetime timeouts) · `FungusLight` (trigger light, turns off once) · `FungusLightBank` (aggregates all lights, all-out detection) · `FungusDrain` (bottom trigger that retires balls) · `FungusHud` (event-driven labels + result panel)

Everything lives under the single `FungusPachinkoMachine` prefab root — the scene just hosts one instance, so the whole machine can later be embedded in the hub as a physical cabinet.

## Tuning quick-reference (inspector)

- `ballsPerGame`, `allLightsBonusPoints`, `ticketsPerPoint`, `returnDelaySeconds` on `FungusGameController`.
- `railHalfWidth`, `moveSpeed` on `FungusDropper`.
- `settleSpeedThreshold`, `settleSeconds`, `maxLifetimeSeconds` on `FungusBall`.
- Ball bounciness lives in `Resources/Material/PM_FungusBounce` (pegs/bumpers/walls share it).

## Rebuilding the board

`Tools > Finn > Build Fungus Pachinko` (or batch mode via `Finn.EditorTools.FungusPachinkoBuilder.BuildFromCommandLine`) regenerates the materials, prefabs, and scene. **Rerunning it overwrites manual edits to the machine prefab and scene** — treat the builder as the source of truth until the board is hand-tuned, then stop rerunning it.
