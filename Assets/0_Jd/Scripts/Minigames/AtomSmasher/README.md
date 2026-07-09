# Atom Smasher

2D pinball-launcher hybrid on a vertical board plane. Aim with the mouse (trajectory arc shown), release to launch, and smash every **required atom** to clear the wave. Waves loop endlessly with escalating hazards until you run out of shots (or time, in timer mode).

**Scene:** `Sc_AtomSmasher` · **Manager:** `AtomSmasherGame` · Board camera is standalone (not the player rig) — the pause menu offers a **2D (orthographic) ⇄ 3D (perspective)** view toggle, and it carries the shake/FOV effects.

## Core rules

- **Ball economy:** start with 5 balls; every wave clear awards +3. Balls accumulate with no cap (the HUD's Peggle-style rack shows them, collapsing past 12 into a "+N"). Run out with required atoms left and the round ends.
- One ball in play at a time — unless quantum effects split it.
- The ball drains at the bottom of the arena, settles out after ~1.25s below crawl speed, or times out at 14s.
- **Chain multiplier:** each atom smashed by the same ball raises that ball's multiplier (+1 per hit). Popups heat from gold to red as the chain grows.
- **Ball bonus:** any balls still alive when the wave clears are smashed in a spark burst and pay `250 × their chain multiplier` each.
- Player-actuated **flippers** (left/right bumper inputs) swat the ball back into play.
- Wave clear holds the board ~1.2s before rebuilding with shuffled atom positions.

## Wave escalation

| From wave | Addition |
|---|---|
| 1 | Paired obstruction bars (one per side, extra pieces every 2 waves), 1 quantum target, guaranteed quantum-charged atoms (more every 2 waves). |
| 2 | Moving targets · **Unstable atoms** (vibrating; deflect direct hits — kill them off a rebound). |
| 3 | **Explosive atoms** (detonate the area, consume the ball) · **Black holes** — gravity wells that curve your shot if you skim the pull radius, swallow the ball past the event horizon, and vacuum every electron spark on the board. |
| 6 | **Pits open**: the two floor covers protected the pits through wave 5; now one random pit opens per wave. Balls falling through drain as usual. |
| 10 | The wave roll picks evenly between left pit, right pit, or **both** open. |

**Quantum effects** (smashing a charged atom): weighted roll between *Speed Boost*, *Split Ball* (up to 9 simultaneous balls), and *Restock* (+3 balls to the rack and another random atom becomes quantum-charged).

## Scoring

`points = atom score × ball chain multiplier` — tickets: 0.1 per point via `PlayerScoreCarrier`.

## Key scripts (this folder)

`AtomSmasherGame` (waves/spawning/scoring/feel) · `AtomSmasherLauncher` (aim + arc) · `AtomSmasherBall` · `AtomSmasherTarget` + `MovingTarget` / `QuantumTarget` / `UnstableTarget` / `ExplosiveTarget` · `AtomSmasherStaticBar` / `MovingBar` / `Rotator` (obstructions) · `AtomSmasherBumper` (flippers) · `AtomSmasherPitTrap` (pit covers) · `AtomSmasherBlackHole` · `AtomSmasherElectron` (sparks) · `AtomSmasherCameraFx` (shake/FOV) · `AtomSmasherHud`

Editor setup: `BlackHoleTrapSetup` (builds/wires the black hole prefab) and `GameplayTweaksSetup` (laser retune + pit covers) under `Sol → Setup`.

## Tuning quick-reference (inspector, on `AtomSmasherGame` unless noted)

- Waves/rules: `startingShots`, `shotsPerWaveClear`, `ballClearBonus`, `restockShots`, `useTimerMode`, spawn areas and per-hazard wave gates. `spawnClearRadius` controls the physics clearance every atom/obstruction spawn keeps from walls, bumpers, and covers.
- Feel: `hitstopSeconds` / `hitstopTimeScale`, `waveClearDelaySeconds`, popup colors, `shakeOn*` sliders.
- Pits: `coveredThroughWave` (5), `bothOpenFromWave` (10) on the `AtomSmasherPitTrap` covers.
- Black hole: gravity/pull/event-horizon radii on the `Obstruction_BlackHole` prefab.
