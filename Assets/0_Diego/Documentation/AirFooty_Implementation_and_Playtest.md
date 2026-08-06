# Air Footy: implementation, tuning and playtest reference

**Canonical companion to:** [Core design and JD upgrades](README.md)  
**Maintainer:** JD  
**Consolidated:** 6 August 2026

This document records the current implementation contracts, important tuning,
asset boundaries, match flow and repeatable validation checks. It is the
technical companion to the design overview; it is not a second design source.

## Authoritative systems

| System | Responsibility |
|---|---|
| `GameManager3D` | Kick-off/countdown, goals, re-drops, clock, overtime, head-to-head result, four-team elimination, persistence and return flow |
| `BallController3D` | Planar velocity, speed cap, damping, wall sweeps, stalls, touch metadata, strike authority and overtime lethality |
| `PlayerMovement3D` | Camera-relative movement, dash movement and team-area bounds |
| `PlayerActions3D` | Pulse/dash input, charge presentation, aim, turbo timing and local action feedback |
| `AirFootyStrikeMotor3D` | Shared player/AI pulse, kick, dash-contact range, strength, cooldown and recovery rules |
| `AirFootyAbilityChargeBank3D` | Three shared charges and sequential recovery |
| `AIPlayer3D` | Predictive head-to-head interception and shot construction |
| `AirFootySideAI3D` | Four-team multi-ball threat selection and team strikes |
| `AirFootyRallyDirector` | Alternating deliberate strikes, Rally Heat, speed tiers and rally presentation |
| `AirFootyCameraFx` / `AirFootyCinemachineCameraRig` | Impact trauma, restrained broadcast follow and team perspective |
| `AirFootyFeedbackUtility` | One-shot goal bursts, messages and renderer flashes |
| `AirFootyCrowdDirector` / `AirFootyCrowd*.cs` | Team crowd reactions driven by match state |
| `AirFootyMatchClock3D` | Diegetic `M:SS` clock and overtime display on the jumbotrons |
| `GoalZone3D` | Team-owned goal trigger and score notification; never owned by AI or FX |

## Key tuning and rules

### Ball

| Setting | Current value | Meaning |
|---|---:|---|
| Ordinary maximum speed | `12 m/s` | Upper cap for normal play |
| Passive contact cap | `4.5 m/s` | Maximum newly created dribble energy; faster incoming shots retain most momentum |
| Linear damping | `0.035` | Keeps the ball from becoming permanently static or uncontrollably fast |
| Abandoned threshold | `0.4 m/s` for `1.25 s` | Starts the controlled re-drop check |
| Near-striker grace | `3 s` within `1.35 m` | Prevents a ball near a player from re-dropping too early |
| Wall/corner restitution | `0.98` | Preserves the air-hockey rebound character |

The manager owns re-drops and blocks them during goals, countdowns and match
completion. Active shots must pass through `ApplyStrike` or its pulse/dash
wrappers. Y velocity is removed and collision-aware sweeps keep movement and
ball contacts planar.

### Player actions

| Action | Current implementation rule |
|---|---|
| Pulse | Tap/hold charge maps to pulse radius and impulse; tap is a `TapKick`, a charged release is a `ChargedKick` |
| Perfect release | Near the full-charge point; uses the perfect kick speed and feedback |
| Dash kick | Contact during a committed dash uses the same strike motor and authoritative ball path |
| Charges | Three shared charges, sequentially recharging; disabling actions clears state and refills |
| Input buffer | Short buffer for pulse/dash input so frame timing does not swallow a deliberate press |
| Miss recovery | A miss applies a small recovery window; it cannot be used to chain free strikes |
| Safety | One strike per physics step; invalid aim/range, cooldown or disabled state rejects the action |

### Modes

| Mode | Arena | Ball count | Win/end rule |
|---|---|---:|---|
| Head-to-head | Long Blue/Red pitch | 1 | First to five goals |
| Four-team | Square Blue/Red/Green/Gold arena | 2 | Five conceded eliminates a team; last active team wins |

`AirFootySessionConfig` carries selected mode, human team and overtime from
`MainMenuUI` into the match. `GameManager3D` discovers the authored goals,
balls and team members, assigns player/AI control, constrains team areas,
tracks concessions independently and routes the result through
`PlayerScoreCarrier`.

## Overtime implementation

Overtime is enabled after `overtimeTriggerSeconds` (currently `300`) when the
mode/config allows it. It is optional for two players and forced on for four.
The final thirty seconds use `clockAlertSeconds` (currently `30`) for the
amber warning; the jumbotrons read `OVERTIME` once the clock expires.

The ball state is deliberately simple:

```text
overtime/reset -> INERT
INERT -- pulse by team T --> ARMED(owner = T)
armed ball reset ------------> INERT
```

Only a pulse arms a ball. An armed ball can vaporise a striker, and the normal
scoring/concede path decides credit, respawn and elimination. Being hit by a
ball armed by the same team is not a special case. `ApplyStrike` and passive
movement contact are refused during overtime; dash remains movement and an
escape option. AI sides hold an overtime standoff, pulse from range, show a
pulse ring and break off when an armed ball is dangerously close.

Vaporised strikers are suppressed renderer-by-renderer, collider-by-collider
and control-by-control rather than by disabling the whole GameObject. This
avoids re-running Input System `OnEnable` wiring. Permanent elimination still
deactivates the team. `GameManager3D.IsSuppressed` prevents a dead player from
receiving control during reset.

Important tuning fields:

- `GameManager3D`: `overtimeTriggerSeconds`, `clockAlertSeconds`,
  `respawnDelaySeconds` (`1.5`), `vaporiseCameraTrauma`.
- `AirFootySideAI3D`: `overtimeStandoff` (`1.9`),
  `overtimeDangerRadius` (`1.3`).
- `AirFootyMatchClock3D`: `faceDisplayCamera` is off by default so the clock
  remains part of the stadium; enable only if a board reads poorly from the
  broadcast camera.

These are first-pass values and still require hands-on balance testing.

## Authored versus runtime content

Fixed content belongs in the scene or prefab. The idempotent maintenance pass
(`Sol > Project Maintenance > Bake Fixed Runtime Assets`) validates/fills the
authored Air Footy arena, player, AI, ball, rally, selection-panel and display
camera support objects after prefab reconstruction.

Runtime creation is intentionally limited to match-state content: pulse waves,
goal bursts, score/status popups, temporary trails and vaporisation feedback.
FX may observe authoritative events but may not create gameplay colliders,
award goals or mutate score. Stadium decoration must not add colliders inside
the playable arena, and local Air Footy post-processing must not modify the
shared project profile.

## UI status and deferred direction

The current UI is functional but has known structural limitations:

- menu panels still mix legacy UGUI `Text` and TextMesh Pro;
- the match banner and result currently share `gameOverText` responsibilities;
- four-team scores are compressed into two text fields;
- panels mostly hard-cut and lack a shared motion language;
- the scoreboard does not yet clearly show leading, near-elimination,
  eliminated or overtime armed-ball state.

The next UI pass should split banner/result ownership first, then decide whether
the shared `SimpleUiBuilder` migrates to TMP, rebuild the scoreboard as one row
per team, add restrained unscaled-time transitions, and preserve the
broadcast-overlay treatment. New panels must call
`ArcadeInputCoordinator.SetMenuFocus`; do not bake
`ArcadeButtonSelectionFeedback` into scenes. This is a planned follow-up, not
an implemented feature claim.

## Repeatable playtest checklist

### Head-to-head

- Start once from Blue and once from Red perspective.
- Confirm countdown disables movement, pulse, dash, AI and ball motion.
- Confirm each striker stays in its half and slides around curved rails.
- Test passive save, tap kick, full pulse, dash kick, turbo pulse and turbo dash.
- Confirm charges spend and recover in order; pause clears held input.
- Leave the ball slow and unattended; confirm one controlled re-drop after the
  grace period, and no premature re-drop near a striker.
- Verify AI near-post, far-post and bank attempts, visible telegraph, reactive
  save, shared strike path and recovery.
- Score into both goals and finish a first-to-five match.

### Four-team and overtime

- Start once as Blue, Red, Green and Gold; confirm only the selected team is
  human-controlled.
- Confirm both balls start, stop, reset and re-drop independently.
- Confirm team semicircle limits prevent centre camping without removing
  defensive movement.
- Concede five goals into each side and verify only that side is eliminated.
- Continue until one team remains; verify winner, score and return flow.
- In overtime, confirm the clock, inert/armed state, owner colour, pulse-only
  rule, AI pulse ring, vaporisation, respawn and elimination credit.

### Feedback and integration

- Compare passive, tap, charged/perfect, dash, wall and goal contacts; each
  should read differently without obscuring the ball.
- Check camera response from every selectable team perspective.
- Check charge, dash aim, AI telegraph, ball trail, hover ring, Rally Heat,
  score UI, crowd, lights, audio and jumbotron response.
- Launch from the Air Footy menu and arcade cabinet.
- Quit through pause and finish normally; confirm score, best score, tickets,
  completion unlock, configured hub return and one concise standalone
  persistence notice when no carrier exists.

| Date | Build/commit | Scope | Result | Follow-up |
|---|---|---|---|---|
| 6 Aug 2026 | Documentation consolidation | Automated compile/import evidence only | Hands-on result not claimed | Run both modes, all four team perspectives and overtime in Play Mode |
