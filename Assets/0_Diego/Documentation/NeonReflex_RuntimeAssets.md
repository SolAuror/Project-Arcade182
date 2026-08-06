# Neon Reflex runtime asset boundary

**Maintainer:** Diego<br>
**Revised:** 6 August 2026

Neon Reflex keeps its arena, camera, UI, managers, spawn markers and target presentation authored. Runtime creation is limited to the short-lived targets and their optional hit feedback.

## Authored production content

- `Assets/0_Diego/Scenes/NeonReflex.unity` owns the arena, gameplay camera, UI hierarchy, game systems and thirteen ordered spawn points.
- `Energy Sphere.prefab` owns the target mesh, collider, renderer and `ReactionTarget` component.
- `EnergySphere.mat` and `EnergySphereFake.mat` provide the real/fake target appearances without per-instance material cloning.
- The shared `ArcadeInput` resource prefab owns the session input coordinator and EventSystem; Neon Reflex does not construct its own UI event hierarchy.

## Deliberately retained runtime content

- `TargetSpawner` instantiates reaction-target prefab instances because their timing, position, size and real/fake state are gameplay.
- Targets destroy themselves when hit or expired. The small target count does not justify a pool.
- An optional authored hit-particle child may detach, play and destroy itself after a hit. This is temporary VFX.

No permanent arena, camera, UI, material, collider or manager content is synthesized during normal play. Missing required authored references now produce a clear error and disable the affected system instead of fabricating replacements.

## Completed migration

The one-time authored-content migration is complete and its temporary editor tooling has been removed. Keep the existing spawn-point array order intact because that order controls which positions unlock in early levels. Runtime validation now reports any missing production reference directly.
