# Labyrinth Crawler runtime asset boundary

**Maintainer:** JD<br>
**Revised:** 1 August 2026

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

Some scripts can still fill a missing authored reference for old or test prefabs. Those branches are recovery paths, not the normal scene setup. The maintenance bake and validation pass should keep production prefabs on the authored route.

The same project maintenance pass also authors Air Footy's fixed selection UI and display camera; this prevents unrelated scene-start UI construction from becoming the normal path.
