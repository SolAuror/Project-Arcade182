# JD PhysGrab

First-person physics grabbing for explicitly marked objects.

## Namespace And Types

```csharp
using Sol.Grab;
```

- `GrabManager`: scene-level grab input and raycasting.
- `GrabbableComponent`: marks an object as grabbable.
- `GrabMode`: mouse or crosshair raycasting.
- `GrabInputBinding`: attack or interact grab input.
- `HoldDistanceOrigin`: camera or assigned transform.

## Scene Setup

Add one `GrabManager` to a manager object. `Sc_ArcadeExterior` has it on `GameManager`.

Assign `Gameplay Camera` or keep the gameplay camera tagged `MainCamera`.

Important `GrabManager` fields:

- `Raycast Distance` and `Raycast Layer Mask`: control what can be reached. Exclude the `Player` layer.
- `Grab Input`: `Attack` uses left click; `Interact` uses the interact action.
- `Grab Mode`: use `Crosshair` for first-person play, or `Mouse` for cursor-driven modes.
- `Scroll Sensitivity`: held-distance adjustment speed.
- `Rotation Mode` and `Rotation Sensitivity`: held-object rotation.
- `Is Locking Enabled`: allows right-click freezing.
- `Is Frozen`: freezes all grabbable rigidbodies.

## Grabbable Setup

Add these to a prop:

1. `Collider` on the same GameObject as `GrabbableComponent`.
2. `Rigidbody` on the same object or a parent.
3. `GrabbableComponent`.

Objects without `GrabbableComponent` are ignored.

## Controls

- Hold left click: grab and release.
- Mouse wheel: adjust held distance.
- Right click: freeze or unfreeze the aimed or held object.
- Middle click: toggle held-object rotation.

## Code API

```csharp
GrabManager.Instance.HeldObject;
GrabManager.Instance.HoveredObject;
GrabManager.Instance.ForceRelease();
GrabManager.Instance.SetGrabMode(GrabMode.Mouse);
GrabManager.Instance.CurrentGrabMode;
```

Check `GrabManager.Instance` for `null` before using it.

The shared `Player.Controller` changes grab mode automatically when camera modes swap. It also keeps grab distance at `10` for crosshair targeting and `30` for mouse targeting, so only call `SetGrabMode` yourself for custom one-off behaviour.

## Troubleshooting

- Object is ignored: add `GrabbableComponent`.
- Physics movement or freezing fails: check its `Rigidbody`.
- Ray hits the player: exclude the `Player` layer.
- No camera is found: assign `Gameplay Camera` or use the `MainCamera` tag.
