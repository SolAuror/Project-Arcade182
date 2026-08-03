# Runtime asset audit

**Audited by:** JD<br>
**Date:** 1 August 2026<br>
**Unity:** 6000.0.76f1

I reviewed runtime calls that create objects, primitives, materials or prefab instances. The goal is not to remove procedural play; it is to keep fixed layout out of `Awake` and make the remaining runtime ownership explicit.

## Baked in this pass

| Area | Fixed content now authored in prefabs/assets | Guarded fallback kept? |
|---|---|---|
| Air Footy arena | Halfway line, centre circle and goal accents | Yes, for old/test prefabs. |
| Air Footy player | Pulse ring, dash aim, three charge pips, turbo stabiliser, two thrusters and reactor light | Yes. |
| Air Footy AI | Shot line, telegraph light, dash trail and audio source | Yes. |
| Air Footy ball | Speed trail, audio source, hover component, ring, shadow mesh and materials | Yes. |
| Air Footy rally | Rally Heat point light | Yes. |
| Air Footy scene shell | Mode/team selection panels, buttons and display camera | Yes. |
| Labyrinth enemies | `AudioSource` and `HitFlash` support components | Yes. |
| Labyrinth flyer | `FlyingEnemyVisual`, two wing meshes and an emissive material | Yes. |

The one-shot authoring command is `Sol > Project Maintenance > Bake Fixed Runtime Assets`. It edits the source prefabs idempotently and validates the expected children afterwards.

## Deliberate runtime generation

| System | Why it stays runtime-owned |
|---|---|
| Hub and Labyrinth mazes | Layout, room weights, exits, buildings and decoration are the designed procedural structure. Instances still come from authored prefabs. |
| Labyrinth enemies and secrets | Count and placement depend on the generated stage graph. |
| Projectiles and balls | They are live gameplay entities with variable count and lifetime. |
| Pulse waves, bursts, trails, popups and melee arcs | They are short-lived event feedback. Fixed emitters and materials are authored where practical. |
| Air Footy broadcast follow targets and noise profile | They depend on the selected team and active output camera, and the temporary noise asset must not be saved into a shared prefab. |
| Retro render target and material instances | Resolution, camera ownership and storm parameters change at runtime; instances prevent shared asset mutation. |
| Arcade preview material instance | Each cabinet needs its own live render texture without changing the source material. |
| Input/bootstrap helpers | They represent session state and cross-scene ownership rather than visual assets. |

## Fallback policy

Production prefabs should pass validation with no fixed child creation. A fallback may add a missing component for an old scene or isolated test, but it must:

1. check for the authored object first;
2. avoid mutating a shared material asset;
3. own and clean up any material instance it creates;
4. stay outside the normal prefab path;
5. remain visible in the audit so it is not mistaken for intended authoring.

## Follow-up boundary

I did not convert transient FX into hundreds of prefab variants, and I did not bake a sample maze into the production scene. Both changes would increase maintenance cost while removing runtime behaviour the games rely on.
