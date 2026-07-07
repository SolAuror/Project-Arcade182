# Shared Player Controller

This is the ready-to-use player for the arcade hub and minigames. `Controller` handles movement, sprinting, gravity, grounding, and jumping. Cinemachine handles where the camera sits, how it follows, and Third Person wall avoidance.

The useful files are:

- Prefab: `Assets/Shared/Controller.prefab`
- Main component and shared settings: `Assets/Shared/Scripts/Controller/Controller.cs`
- Shared movement: `Assets/Shared/Scripts/Controller/Controller.Movement.cs`
- Shared jumping: `Assets/Shared/Scripts/Controller/Controller.Jump.cs`
- Camera coordinator: `Assets/Shared/Scripts/Controller/Controller.Camera.cs`
- Mode behavior and tuning: `Controller.FirstPerson.cs`, `Controller.ThirdPerson.cs`, `Controller.TopDown.cs`, `Controller.Isometric.cs`, and `Controller.Platformer.cs`
- Input actions: `Assets/Shared/InputSystem_Actions.inputactions`

## Quick Setup

Drag `Controller.prefab` into the scene, place it slightly above a floor collider, and choose `Camera Mode` on its `Controller`.

The selected camera mode automatically selects its movement rules. There is no second movement-mode setting to keep in sync.

Each mode has its own `Movement Speed Multiplier` and `Turn Speed Multiplier`. Leave them at `1` to use the shared movement settings unchanged, or adjust one mode without affecting the others.

Keep only one active shared player, gameplay camera, and Audio Listener in a scene. The output camera stays tagged `MainCamera` because the grab and outline systems use `Camera.main`.

The controller automatically hides the Player layer in First Person and renders it in every other mode.

## Controls

- Move: `WASD`, arrow keys, or gamepad left stick
- Look or orbit: mouse or gamepad right stick. Up input looks up.
- Jump: `Space` or gamepad south button in First Person, Third Person, Isometric, and Platformer
- Sprint: `Left Shift` or gamepad left-stick press
- Unlock or relock the cursor: `Escape`
- Hold pointer mode in Isometric: `Tab`

Grab-object rotation temporarily suppresses camera look, so mouse movement can rotate the held object instead.

## Camera And Movement Modes

### First Person

Uses `CinemachineFollow` and `CinemachinePanTilt`. The camera sits at the upper-body target and freely looks around.

Horizontal look rotates the player, while Cinemachine keeps vertical pitch on the camera. Movement uses the full camera-relative ground plane, sprint works with meaningful forward input, and jumping is enabled. Strafing and backing up do not turn the player away from the current view.

### Third Person

Uses `CinemachineOrbitalFollow`, `CinemachineRotationComposer`, and `CinemachineDeoccluder`.

The camera freely orbits around the upper-body target. Movement is camera-relative, the player turns toward movement, sprint works forward, and jumping is enabled. The Deoccluder pulls the camera around or in front of walls that block the player.

### Top Down

Uses a fixed orthographic `CinemachineFollow` camera above the player.

Movement is screen-relative: `W` moves toward the top of the Game view, `S` toward the bottom, `A` left, and `D` right. Sprint works in any meaningful direction, and jumping is disabled. This is the only jump-disabled mode. It does not use look input, so the cursor stays unlocked.

### Isometric

Uses an orthographic `CinemachineOrbitalFollow` and `CinemachineRotationComposer`.

The camera can orbit horizontally while its elevated angle stays fixed. Movement is camera-relative, sprint works forward, and jumping is enabled. It intentionally has no collision extension so the orthographic framing stays stable.

By default, Isometric keeps the cursor locked and uses the crosshair for grab and outline rays. Hold `Tab` to pause orbit, show the cursor, hide the crosshair, and aim grab or outline rays from the mouse position.

### Platformer

Uses a fixed side-on orthographic `CinemachineFollow`.

Only left and right movement are accepted. Sprint works in either horizontal direction, and jumping is enabled.

## Tuning Cinemachine

Expand `CinemachineRig` inside the shared prefab to tune a mode. Each child `CinemachineCamera` owns its own lens and follow behavior.

- Change a camera's Lens settings for field of view or orthographic size.
- Change `Follow Offset` on TopDown or Platformer to move their fixed framing.
- Change `Radius` and axis ranges on an `Orbital Follow` to tune orbit distance and limits.
- Keep Isometric's vertical orbit axis disabled if its pitch should remain fixed.
- Tune ThirdPerson's `Cinemachine Deoccluder` to change wall avoidance.
- Keep the Player layer excluded from collision and grounding masks.

The real `FppCam` only renders the final result. Do not position it manually; `CinemachineBrain` moves it to the active Cinemachine camera.

Mode changes use a hard cut. This avoids strange blends between perspective and orthographic cameras.

For code changes, start in the file named after the mode. Those files own the mode's movement direction, sprint rule, look-input rule, player visibility, camera reference, and tuning multipliers. Shared acceleration, gravity, grounding, and camera switching stay in the shared files so fixes apply to every mode.

## Movement Feel

Movement accelerates toward the requested speed rather than snapping instantly. `Acceleration` controls starting and reversing, `Deceleration` controls stopping, and `Air Acceleration` controls steering while airborne.

Jumping is shared by First Person, Third Person, Isometric, and Platformer:

- `Coyote Time`, which allows a jump just after leaving a ledge.
- `Jump Buffer Time`, which remembers a jump pressed just before landing.
- Variable jump height, where releasing jump early produces a shorter jump.
- Stronger falling gravity, which keeps the jump from feeling floaty.

Top Down ignores jump input but keeps gravity, so falling from ledges and switching modes while airborne still behave normally.

## Pseudocode Overview

The main movement flow stays independent from Cinemachine:

```text
apply a changed camera mode
read and clamp movement input
apply the movement and sprint rules linked to that mode
process jump timers when the selected mode allows jumping
apply gravity
move the CharacterController once
```

Camera-relative movement uses the real output camera:

```text
camera right = output camera right flattened onto the ground
camera forward = output camera forward flattened onto the ground
desired movement = right * horizontal input + forward * vertical input

turn player toward desired movement
```

Cinemachine input is filtered before it reaches orbit or Pan Tilt:

```text
if cursor is unlocked, mouse interaction is active, or grab rotation is active:
    return no look input

if active look device is a pointer:
    apply mouse delta sensitivity without frame-rate scaling
else:
    apply controller look speed per second
```

Changing modes only switches which Cinemachine camera is active:

```text
disable the previous mode camera
enable the selected mode camera
hide the Player layer only in First Person
reset the Cinemachine Brain for an immediate cut
lock or unlock the cursor for the selected mode
apply the same crosshair or mouse ray mode to grab and outline
show the crosshair only when crosshair rays are active
```

## Grab And Outline Compatibility

The output `FppCam` remains tagged `MainCamera`, so the grab and outline managers continue to raycast from the final Cinemachine-controlled view.

First Person and Third Person use crosshair rays for both grab and outline. Top Down and Platformer use mouse-position rays. Isometric uses crosshair rays by default, then switches both grab and outline to mouse rays while `Tab` is held.

First Person and Third Person crosshair targeting keeps grab and outline ray distance at `10`. Isometric and mouse targeting use `30`, which accounts for the overhead camera distance and gives cursor-driven modes more room to click around the screen.

Assign `Crosshair Object` on the Controller if the scene has a crosshair UI. Missing references are fine; the controller just skips visibility changes.

## Troubleshooting

- Nothing renders: make sure `FppCam` is enabled and has `CinemachineBrain`.
- Camera does not follow: check that the selected mode camera is assigned on `Controller`.
- Player is invisible outside First Person: make sure the Player layer is included in `FppCam`'s culling mask.
- Multiple cameras fight: keep only one active shared player and output camera.
- Mouse does not look: click the Game view or press `Escape` to relock the cursor.
- Isometric mouse grabbing does not work: hold `Tab` so grab and outline switch from crosshair rays to mouse rays.
- Third Person clips through walls: check its Deoccluder and collision layers.
- Third Person detects the player as a wall: exclude the Player layer and keep `Ignore Tag` set to `Player`.
- Isometric pitch changes: keep its vertical input axis disabled and its vertical orbit range fixed.
- Player cannot jump in Top Down: this is expected.
- Player cannot jump in a jump-enabled mode: check the floor collider and `Ground Layers`.
- Player constantly appears grounded: exclude the Player layer from `Ground Layers`.
- Grab or outline raycasts fail: keep `FppCam` tagged `MainCamera`.

For most minigames, choose a mode and tune only that mode's Cinemachine child. The shared movement defaults can then stay familiar across the arcade.
