# JD Outline

URP outlines for explicitly marked objects.

## Namespace And Types

```csharp
using Sol.Outline;
```

- `OutlineManager`: scene-level hover detection.
- `OutlineComponent`: marks an object as outlineable.
- `SolOutlineRendererFeature`: renders registered outlines.

`OutlineManager` uses `Sol.Grab.GrabMode` for mouse or crosshair raycasting.

## Renderer Setup

`SolOutlineRendererFeature` is already added to:

`Assets/Shared/Settings/PC_Renderer.asset`

It uses:

- `Hidden/Arcade/OutlineMask`
- `Hidden/Arcade/OutlineFullscreen`

## Scene Setup

Add one `OutlineManager` to a manager object. `Sc_ArcadeExterior` has it on `GameManager`.

Assign `Gameplay Camera` or keep the gameplay camera tagged `MainCamera`.

Important fields:

- `Raycast Distance`: maximum detection distance.
- `Detection Layer Mask`: detected layers. Exclude the `Player` layer.
- `Ray Mode`: use `Crosshair` for first-person play, or `Mouse` for cursor-driven modes.

## Outlineable Setup

1. Add `OutlineComponent` to the object.
2. Ensure the object or a child has a `Renderer`.
3. Set `Outline Color` and `Outline Width`.

Optional fields:

- `Always Visible`: keeps the outline active without hover.
- `Priority`: draws the outline through other objects.

`OutlineComponent` and `GrabbableComponent` can be used on the same prop.

## Code API

```csharp
outline.ShowOutline();
outline.HideOutline();

OutlineManager.Instance.CurrentOutlinedObject;
OutlineManager.Instance.SetRayMode(GrabMode.Mouse);
OutlineManager.Instance.CurrentRayMode;
```

Check `OutlineManager.Instance` for `null` before using it.

The shared `Player.Controller` keeps outline ray mode matched to grab mode. It also keeps outline distance at `10` for crosshair targeting and `30` for mouse targeting, so both systems switch together.

## Troubleshooting

- Nothing outlines: check the active URP renderer has `SolOutlineRendererFeature`.
- One object is ignored: add `OutlineComponent` and check its child renderers.
- Ray hits the player: exclude the `Player` layer.
- No camera is found: assign `Gameplay Camera` or use the `MainCamera` tag.
