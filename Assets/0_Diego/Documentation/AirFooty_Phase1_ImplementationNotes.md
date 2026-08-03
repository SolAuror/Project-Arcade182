# Air Footy physics and player feedback

**Original game:** Diego<br>
**Update:** JD<br>
**Revised:** 1 August 2026

This document records the current ball and player layer. I keep it separate from AI and match rules so tuning one area does not hide responsibility in another.

## Ball ownership

`BallController3D` is the authority for planar velocity, speed caps, wall sweeps, stalls, touch metadata and deliberate strike application.

- Ordinary maximum speed: `12 m/s`.
- Passive contact cap: `4.5 m/s` of newly created dribble energy; a faster incoming shot keeps most of its momentum.
- Linear damping: `0.035`.
- Abandoned-ball threshold: `0.4 m/s` for `1.25 s`.
- Near-striker grace: `3 s` within `1.35 m`.
- Wall and rounded-corner sweep restitution: `0.98`.
- Touch types: passive, tap, charged and dash.

The manager owns countdowns and re-drops. This prevents a ball from resetting during a goal, another countdown or match completion.

## Player actions

| Action | Keyboard/mouse | Gamepad | Result |
|---|---|---|---|
| Move and aim | WASD/arrows | Left stick/D-pad | Camera-relative movement; the latest meaningful direction becomes dash aim. |
| Pulse | Left mouse | South button | Tap or hold. The radial wave uses player-to-ball direction and spends one charge. |
| Dash | Right mouse or Shift | Right trigger/east button | A short committed move; ball contact uses the authoritative dash-strike path. |

Pulse and dash share three sequentially recharging charges. The UI ring, aim arrow and charge pips show range, direction, availability and timing. Turbo Pulse and Turbo Dash reward overlapping the two actions without adding a separate input.

## Feedback ownership

- `PlayerActions3D`: charge ring, dash aim, pips, turbo stabiliser, thrusters and local action cues.
- `BallController3D`: speed trail, hover presentation, contact audio and authoritative touch events.
- `AirFootyRallyDirector`: alternating-strike count, tier speed caps and rally glow.
- `AirFootyCameraFx` and `AirFootyCinemachineCameraRig`: impact trauma, restrained broadcast follow and team perspective.
- `AirFootyFeedbackUtility`: one-shot goal bursts, messages and short renderer flashes.

The fixed parts above are prefab-authored. Only time-limited waves, popups and bursts are created during play.

## Safety rules

- All velocity changes from an active shot go through `ApplyStrike` or the pulse/dash wrappers that call it.
- A second strike in the same physics step is rejected.
- Y velocity is removed and arena movement uses collider-aware sweeps.
- Pause, countdown, goal and match-end transitions clear held input.
- FX may observe authoritative events but may not award goals or change score.
