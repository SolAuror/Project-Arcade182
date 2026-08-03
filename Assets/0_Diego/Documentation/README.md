# Air Footy documentation

**Game by Diego; updated by JD**<br>
**Current revision:** 1 August 2026

Air Footy remains Diego's game. I use his arena, rules and neon presentation as the frame for the changes I made; I do not describe the original work as disposable or interchangeable.

## Documents

- [Design revision and authorship](AirFooty_Jds-Research-Revisions.md)
- [AI state graph and pseudocode](AirFooty_AI.md)
- [Physics and player feedback](AirFooty_Phase1_ImplementationNotes.md)
- [AI, four-team mode and arcade integration](AirFooty_Phase2_ImplementationNotes.md)
- [Playtest protocol](AirFooty_Phase0_PlaytestNotes.md)
- [Stadium presentation](AirFooty_SetDressing_Blockout.md)

## Source map

- `AirFooty_2Player.prefab`: head-to-head authored arena.
- `AirFooty_4Player.prefab`: four-team, two-ball authored arena.
- `Scenes/AirFootyFinal.unity`: menu and match host scene.
- `Scripts/3D/`: ball, player, AI, match, feedback and presentation code.
- `Scripts/AirFootySessionConfig.cs`: menu-to-match selection.
- `Resources/Materials/AirFooty/`: authored gameplay and presentation materials.

The old `(LEGACY)AirFooty.unity` scene is retained as historical source material. It is not the current implementation target.
