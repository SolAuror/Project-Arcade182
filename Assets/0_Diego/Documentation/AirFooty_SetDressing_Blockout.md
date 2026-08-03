# Air Footy stadium presentation

**Original game and visual frame:** Diego<br>
**Update and integration:** JD<br>
**Revised:** 1 August 2026

The stadium keeps Diego's cosmic/neon identity and surrounds both authored arena prefabs with readable team colour. I use the original pitch and goals as the visual centre; the added stands, crowds, pylons, portals and jumbotrons support them rather than replacing them.

## Authored layouts

| Prefab | Arena | Teams | Crowd role |
|---|---|---|---|
| `AirFooty_2Player.prefab` | Long head-to-head pitch | Blue and Red | Celebrates goals, reacts to rallies and reinforces the two ends. |
| `AirFooty_4Player.prefab` | Square, four goals, two balls | Blue, Red, Green and Gold | Gives each side a readable home colour and reacts around the full bowl. |

The fixed stadium mesh, materials, crowd blocks, lights, portal furniture and pitch markings are authored assets. Crowd animation, team-light intensity, score texture updates and one-shot celebration effects remain runtime state.

## Contracts

- Stadium decoration must not add gameplay colliders inside the arena.
- Team furniture faces inward towards the pitch.
- Crowd and jumbotron systems read match state; they do not change score.
- The active camera must keep both the ball and goal mouths readable from every selectable team perspective.
- Post-processing changes stay local to Air Footy and must not alter the shared project profile.

## Prefab maintenance

Run `Sol > Project Maintenance > Bake Fixed Runtime Assets` after deleting or rebuilding fixed feedback children. The pass is idempotent: it fills missing pitch markings, player indicators, AI telegraphs, ball hover parts, trails, audio sources, rally lights, selection panels and display camera, then validates both Air Footy prefabs and the host scene.

I keep transient pulse waves, goal bursts and score popups runtime-owned because their count, position and lifetime are match events rather than authored layout.
