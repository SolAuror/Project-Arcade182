# Air Footy: JD's design revision

**Original game:** Diego<br>
**Update work:** JD<br>
**Revision date:** 1 August 2026<br>
**Status:** implemented; final hands-on balance remains an ongoing playtest task

## Position

Air Footy is Diego's game. I used Diego's compact 3D arena, first-to-five structure, physics ball, goals and neon presentation as the frame for my update. I did not treat that work as a replaceable mass. My goal was to make the existing game ask for clearer decisions while keeping its identity and authorship intact.

## What I changed

The earlier build could maintain a rally through passive body contact and automatic ball speed. I changed the interaction around deliberate play:

- The ball can slow and, when genuinely abandoned, return through a controlled re-drop.
- Pulse and dash give the player authored direction and strength instead of relying only on collision drift.
- A shared charge bank limits repeated abilities and makes commitment visible.
- Rally Heat rewards alternating deliberate strikes rather than wall rebounds.
- The head-to-head AI predicts interceptions, constructs near-post, far-post and bank shots, telegraphs its commitment, uses the same strike path as the player, and recovers after the attempt.
- Hit, save, goal, charge, dash and rally feedback make cause and effect easier to read.
- The four-team mode adds a second ball, team-side movement areas, selectable player colour, AI-controlled remaining teams and elimination after five conceded goals.
- Match results connect to the project's score, ticket, unlock, pause and hub-return systems.

## Design reasoning

I used three practical rules.

1. **Direction should come from intention.** A strong shot follows a pulse or committed dash, so the result is easier to explain and repeat.
2. **Challenge should remain legible.** The AI plans and telegraphs rather than receiving hidden physics advantages.
3. **Feedback should explain the simulation.** FX amplify contact quality, possession pressure and scoring; they do not decide the outcome.

This makes the main loop: read the ball, move around it, choose a lane, commit, watch the result, then recover.

## Scope I kept

- Compact arcade matches rather than a football simulation.
- Flat arena movement and simple controls.
- Physics rebounds, goals and first-to-five head-to-head play.
- Diego's cosmic/neon presentation and existing asset organisation.

## Scope I added

- Pulse, dash and turbo timing.
- Predictive head-to-head AI.
- Four-team elimination with two balls.
- Stronger camera, crowd, sound, UI and particle feedback.
- Shared arcade progression and scene-flow integration.

## Related documents

- [Air Footy index](README.md)
- [AI state model and pseudocode](AirFooty_AI.md)
- [Physics and feedback implementation](AirFooty_Phase1_ImplementationNotes.md)
- [Modes, AI and integration](AirFooty_Phase2_ImplementationNotes.md)
- [Playtest protocol](AirFooty_Phase0_PlaytestNotes.md)
- [Stadium presentation](AirFooty_SetDressing_Blockout.md)
