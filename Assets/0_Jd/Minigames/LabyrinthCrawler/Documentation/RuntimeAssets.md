# Labyrinth Crawler runtime asset boundary

**Maintainer:** JD<br>
**Revised:** 5 August 2026

I keep stable presentation in prefabs and runtime variability in the systems that need it.

## Authored assets

- Room, roof, pit and upper-cell prefabs.
- Caster, stalker and flyer enemy prefabs.
- Flyer wing meshes/material and enemy support components.
- Exit pad, beacon, secret cache, illusory wall, projectile and spell-impact prefabs.
- HUD, upgrade, run-over and player feedback UI.
- Storm, PS1, ripple and present materials.
- Spell definitions and fixed combat VFX.

## Deliberately generated during play

- The maze instance and its `Generated Rooms` parent.
- Building placements, room decoration choices, secret links and caches selected from authored prefabs.
- Stage enemy instances and projectiles.
- One-shot damage numbers, melee arcs, burst effects and hit VFX.
- Ripple samples stored in a per-renderer property block.
- The resolution-dependent `RenderTexture`, sky material instance and present material instance.
- Clear camera/output canvas used to route the low-resolution target while the presenter is active.

The maze is the primary designed exception: baking one layout would remove the stage-to-stage structure. Runtime material instances in `RetroPresenter` are also deliberate because storm flashes and render textures must not dirty shared assets.

## Guarded fallbacks

Only two compatibility fallbacks remain:

- `LabyrinthRuntimeUtility.EnsureSphereTrigger` can repair an old/test trigger and emits a warning.
- `Room3D` can construct a minimal missing wall socket for an old/test room and emits a warning.

Stable production support content no longer has a silent runtime fallback. Missing game timer/audio/containers, player combat/overlay, enemy hit/audio/wing support, projectile audio/prefabs, hitscan beam or HUD title wave is reported as an authoring error.

## Completed migration

The one-time authored-content migration is complete and its temporary editor tooling has been removed. Missing production references are reported directly at runtime. The retained regression tools cover deterministic maze signatures and the 40-seed pit/building solvability audit.
