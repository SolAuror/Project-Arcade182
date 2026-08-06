# Air Footy: core design and JD upgrades

**Core game:** Diego  
**Gameplay, feedback and systems update:** JD  
**Consolidated:** 6 August 2026

This is the canonical design overview for Air Footy. Air Footy remains Diego's
game: JD's work extends the original arcade loop and presentation rather than
replacing its authorship or visual identity.

## The design in one sentence

Air Footy is a compact neon 3D air-hockey game where players move around a
physics-driven ball, kick it through goals, and turn positioning, timing and
wall rebounds into short arcade matches.

## Ownership and design layers

| Layer | Diego's core | JD's upgrade |
|---|---|---|
| Play space | Compact 3D arena, flat movement, walls and goal mouths | Team-aware bounds and a second authored arena for four-team play |
| Ball and scoring | Physics ball, rebounds, goals and first-to-five head-to-head rules | Stall detection, controlled re-drops, deliberate strike authority and overtime lethality |
| Kicks | Readable physical kick/redirect interaction: get into position, choose a lane and send the ball back into play | Pulse, charge, dash strikes, turbo techniques and a shared charge resource make intention and timing explicit |
| Presentation | Cosmic/neon pitch, goals and arcade spectacle | Impact, rally, goal, AI, camera, crowd, jumbotron, audio and charge feedback |
| Opponent play | Head-to-head arcade opponent frame | Predictive shot planning, telegraphed commitments, shared strike rules and lighter four-team threat AI |
| Match structure | Short head-to-head game | Four-team/two-ball elimination, optional two-player overtime and shared score/ticket/hub flow |

## Diego's core air-hockey design

The original game is built around the readable rhythm of air hockey:

1. Move within a compact arena and read the ball's current momentum.
2. Get around the ball so a kick can send it toward a goal or useful wall.
3. Defend the goal mouth, recover from a rebound and create the next opening.
4. Score five goals before the opponent does.

The ball remains a physical object. Walls, rounded corners, goal triggers and
momentum create the play; the visual layer does not decide the result. The
original cosmic/neon pitch, goals, team colours and arcade framing are the
identity of the game and remain the visual foundation.

The kick is the core expressive action. A good kick is explained by the
player's position and aim, not by a hidden target correction. The updated
strike motor preserves that relationship for both the human and AI: all active
ball velocity changes go through the same authoritative path.

## JD's upgraded controls

JD changed the interaction from mostly passive contact and automatic speed into
deliberate, authored decisions. Movement still determines position and aim;
the new actions determine when and how strongly the ball is kicked.

| Action | Keyboard/mouse | Gamepad | Design role |
|---|---|---|---|
| Move and aim | WASD/arrows | Left stick/D-pad | Camera-relative movement; the last meaningful direction becomes dash aim |
| Pulse | Left mouse | South button | Tap or hold to kick from range; charge increases radius and impulse |
| Dash | Right mouse or Shift | Right trigger/east button | Short committed movement; contact uses the same authoritative dash-kick path |
| Turbo Pulse | Pulse during the prepared window | Same pulse input | Slightly stronger pulse with a distinct overdrive presentation |
| Turbo Dash | Dash during the prepared window | Same dash input | Faster/longer dash with afterburner feedback |

Pulse and dash draw from three sequentially recharging shared charges. The
charge pips, ring and dash aim indicator communicate availability, direction,
range and commitment. Input buffering makes the actions responsive without
removing the timing skill. Missed dashes have a short recovery so a whiff has a
cost without freezing the player.

The strike motor distinguishes tap, charged and perfect kicks. It enforces
range, cooldown, aim validity, one strike per physics step and recovery after a
miss. Turbo is a timing technique, not a separate control system.

## Feedback and FX upgrades

Feedback amplifies authoritative gameplay events; it never awards goals,
changes score or silently corrects a shot.

| Event or system | Feedback |
|---|---|
| Pulse charge | Expanding pulse ring, charge colour and hold-to-release readability |
| Dash | Aim line, dash trail, thrusters, reactor glow and missed-dash recovery |
| Turbo | Magenta/cyan overdrive stabiliser, glow and trail treatment |
| Ball contact | Touch-specific audio, speed trail, hover ring, shadow/presentation and camera trauma |
| Deliberate rally | Rally Heat counter, tiered speed caps, glow and telemetry |
| Goal | Goal burst, score punch, short message, camera response and team crowd reaction |
| AI commitment | Visible shot line, telegraph glow, dash trail and AI pulse wave |
| Match state | Countdown, kick-off/re-drop banner, score, crowd, stadium lights and jumbotron clock |
| Overtime | Clock changes to amber/red, armed ball flashes in its owner's colour, AI pulse rings become visible and vaporisation receives a burst/audio sting |

The fixed arena, player, AI, ball and UI support objects are authored in
prefabs/scenes. Short-lived waves, goal bursts, popups and other event FX are
created at runtime because their position and lifetime are match state.

## AI upgrades

### Head-to-head AI

The opponent uses a readable six-part loop:

`Recover → Predict Intercept → Acquire Shot Lane → Charge → Strike → Cooldown`

It predicts a defensive lane for fast incoming balls, then constructs one of
three inspectable shots when attacking:

- near-post direct shot;
- far-post direct shot;
- side-wall bank shot using a mirrored goal point.

The planner scores lane clearance, defender separation, reposition cost and
controlled variety, then adds deterministic bounded aim error. The AI moves
behind the ball, shows its charge/shot line, strikes through the same
`AirFootyStrikeMotor3D` and `BallController3D` path as the player, and recovers
after a hit or miss. Difficulty changes reaction delay, telegraph timing and
aim error; it does not grant hidden mass, restitution or force advantages.

### Four-team AI

The four-team mode uses a cheaper threat-response loop because three AI sides
can be managing two balls at once. Each side:

1. scores active balls by threat to its own goal minus travel cost;
2. chooses a reachable ball and remains inside its team movement area;
3. chooses an opponent goal and positions behind the ball;
4. strikes when its range and shared cooldown allow it;
5. returns to a defensive anchor when no safe attack is available.

This keeps the mode legible and affordable while preserving deliberate team
strikes. It intentionally does not run four copies of the more expensive
head-to-head lane-construction planner.

## Modes and overtime

Head-to-head is one human against one AI in a first-to-five match. The human
may choose Blue or Red, and the broadcast camera and score labels follow that
choice.

Four-team play uses Blue, Red, Green and Gold, two balls and team-side movement
areas. The selected team is human-controlled; the other active teams are AI.
Five goals conceded eliminates a team, disables its goal and striker, and the
last active team wins.

The five-minute overtime contingency is optional in two-player and mandatory
in four-player. At overtime the pitch becomes pulse-only: kicks and passive
body shoves no longer move the ball, while dash remains a movement/escape
tool. A ball starts inert after overtime begins or a reset, becomes lethal only
after a pulse, and is owned by the team that last pulsed it. A lethal armed ball
can vaporise a striker and route the result through the normal concede,
respawn and elimination systems.

## Where to go next

- [Implementation, tuning and playtest reference](AirFooty_Implementation_and_Playtest.md)
- `Assets/0_Diego/Scenes/AirFootyFinal.unity`: current host scene
- `Assets/0_Diego/Resources/Prefabs/AirFooty/`: authored arena and feedback assets
- `Assets/0_Diego/Scripts/3D/`: ball, player, AI, match, feedback and presentation systems

The old phase, AI, presentation, overtime and JD-revision notes were
consolidated into the two documents above. The legacy `(LEGACY)AirFooty.unity`
scene remains historical source material, not the current implementation
target.
