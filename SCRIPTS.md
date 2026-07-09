# Script Directory

Master index of every gameplay script in the project, what it does, and where it runs.

**Scope legend** — `SYSTEM`: integral, runs in every (or nearly every) scene · `HUB`: arcade hub / meta loop · `LAB`: Labyrinth Crawler · `HOOPS`: Hoops · `ATOM`: Atom Smasher · `MENU`: main/pause menus · `EDITOR`: editor-only tooling.

All paths are relative to `Assets/0_Jd/Scripts/` unless noted.

## System-integral

| Script | Scope | Purpose |
|---|---|---|
| `Controller/Controller.cs` (+ `.Movement`, `.Camera`, `.Jump`) | SYSTEM | Shared player controller core: movement, gravity, grounding, camera-mode state machine, input wiring. |
| `Controller/Controller.FirstPerson / .ThirdPerson / .TopDown / .Isometric / .Platformer` | SYSTEM | Per-camera-mode movement and look rules for the shared controller. |
| `Controller/Controller.HeadBob.cs` | SYSTEM | First-person walk bob (figure-8 on the FP vcam), speed-scaled, grounded-only. Active in hub and labyrinth. |
| `PlayerSpawn.cs` | SYSTEM | Scene-start spawn marker. Spawns the player prefab if missing; only an explicit respawn call moves an existing player. |
| `PhysGrab/Sol.GrabManager.cs` | SYSTEM | Singleton physics grab/carry/throw system (crosshair or mouse mode). |
| `PhysGrab/Sol.GrabbableComponent.cs` | SYSTEM | Marks a rigidbody as grabbable by the GrabManager. |
| `Outline/Sol.OutlineManager.cs` | SYSTEM | Hover-outline raycaster matching the grab interaction mode. |
| `Outline/Sol.OutlineComponent.cs` | SYSTEM | Per-object outline registration. |
| `Outline/SolOutlineRendererFeature.cs` | SYSTEM | URP render feature drawing the outlines. |
| `Minigames/Shared/PlayerScoreCarrier.cs` | SYSTEM | Persistent progression (PlayerPrefs): tickets, per-game last/best scores, minigame completion flags, golden coin, game-beaten flag. |
| `Arcade/ArcadeMetaBootstrap.cs` | SYSTEM | `RuntimeInitializeOnLoadMethod` bootstrap: applies saved options, injects the pause menu everywhere, spawns `HubGameLoop` in the hub. No scene wiring needed. |
| `UI/PauseMenuController.cs` | SYSTEM/MENU | Esc pause menu (persistent singleton, prefab-authored via `Resources/UI/PauseMenu`). Minigames: Resume / Quit to Hub (+ Atom Smasher 2D-3D view toggle). Hub: Resume / Volume / Quit to Menu / Quit Game. |
| `UI/SimpleUiBuilder.cs` | SYSTEM/MENU | UGUI construction helpers used by the editor UI setup to build the menu prefabs, plus the runtime `EnsureEventSystem` helper. |
| `UI/ArcadeOptions.cs` | SYSTEM/MENU | Persisted options store (master volume) shared by main and pause menus. |
| `Assets/Shared/Input/InputSystem_Actions.cs` | SYSTEM | Generated Input System actions asset wrapper (Player, AtomSmasher, Hoops, LabyrinthCrawler maps). Do not hand-edit. |

## Hub & overarching loop

| Script | Scope | Purpose |
|---|---|---|
| `ArcadeGen/ArcadeGen3D.cs` | HUB, LAB | Weighted-prefab maze generator (DFS carve). Drives the hub arcade and every labyrinth stage. |
| `ArcadeGen/Room3D.cs` | HUB, LAB | Maze cell: wall flags, spawn weight, wall transforms. |
| `ArcadeGen/Room2D.cs`, `ArcadeGen/SolMazeGen2D.cs` | legacy | Older 2D maze generator pair; not part of the current loop. |
| `ArcadeGen/EndRoomExitClerkActivator.cs` | HUB | Activates the clerk desk on a still-closed wall of the generated end room. |
| `MazeExitInteractable.cs` | HUB, LAB | Interactable clerk. Hub: sells the golden coin for 1,000,000 tickets. Labyrinth: legacy stage-exit hook (replaced by the exit pad). |
| `Arcade/HubGameLoop.cs` | HUB | Auto-spawned each hub load: regenerates the maze so every return from a minigame gets a fresh layout. |
| `Arcade/GoldenExitDoor.cs` | HUB | Doorframe that redeems the golden coin: beats the game and returns to the main menu. Sealed (dark) without the coin, gold when unlocked. Authored into a room prefab. |
| `ArcadeMachineLauncher.cs` | HUB | Arcade cabinet: aim-and-interact to launch a minigame scene; optional live RenderTexture preview screen. |
| `HubHud.cs` | HUB | Hub overlay: live ticket total. |
| `UI/MainMenu.cs` | MENU | "Insert Coin to Exit" main menu: Start Game, Play Minigames (per-game unlock after first completion), Options, Quit. |

## Shared minigame framework (`Minigames/Shared/`)

| Script | Scope | Purpose |
|---|---|---|
| `MinigameTimer.cs` | LAB | Reusable stopwatch/countdown timer component. |
| `Combat/Faction.cs` | LAB | Player/Enemy/Neutral faction enum. |
| `Combat/Health.cs` | LAB | Reusable health pool with damage/death events and faction filtering. |
| `Combat/Mana.cs` | LAB | Regenerating mana pool; records failed spends for HUD/audio feedback. |
| `Combat/SpellDefinition.cs` | LAB | Base ScriptableObject for spells (name, damage, mana, cooldown). |
| `Combat/HitscanSpellDefinition.cs` | LAB | Instant beam spell (the laser; sustained while held). |
| `Combat/ProjectileSpellDefinition.cs` | LAB | Projectile spell (fireball). |
| `Combat/AoeSpellDefinition.cs` | LAB | Radial burst spell (pulse): damage + knockback crowd control + shockwave visual. |
| `Combat/SpellCastContext.cs` | LAB | Per-cast data (caster, aim ray, faction, runtime bonuses). |
| `Combat/SpellCaster.cs` | LAB | Slot-based loadout with unlock/level/cooldown state; casts for player input and enemy AI. |
| `Combat/Projectile.cs` | LAB | Moving spell projectile with faction-filtered impact. Friendly projectiles pass through each other; supports mid-air shoot-down (laser) and pulse reflection (flips owner and flies back). |
| `Combat/HitFlash.cs` | LAB | Tints renderers briefly when the sibling Health takes damage. |
| `Combat/DamagePopup.cs` | LAB, ATOM, HOOPS, HUB | Floating world-space number/message popup (damage, score, clerk dialogue). |
| `Combat/SpellBurstVisual.cs` | LAB, HOOPS | Procedural expanding shockwave sphere (pulse blast, enemy deaths, hoop score/activation flares). |
| `Combat/PlayerHitFeedback.cs` | LAB | Player damage overlay: red hit flash + low-health heartbeat vignette. Overlay authored on the player prefab (runtime build only as fallback). |
| `PlayerScoreCarrier.cs` | SYSTEM | (listed above) score/ticket/coin persistence. |

## Labyrinth Crawler (`Minigames/LabyrinthCrawler/`)

| Script | Purpose |
|---|---|
| `LabyrinthCrawlerGame.cs` | Run orchestrator: stage/maze sizing, enemy spawning (scaling packs), scoring & par times, upgrades flow, fall-out respawn, audio hooks, score/ticket recording. |
| `EnemyController.cs` | Maze enemy: wanders while idle, chases with line-of-sight, casts spells in range, takes pulse knockback, death burst. |
| `PlayerSpellInput.cs` | Binds player input to SpellCaster slots (attack / cast / pulse). |
| `LabyrinthExitPad.cs` | Stand-on stage exit: instant when enemies are dead, interruptible dwell otherwise; animated pad visual. |
| `LabyrinthUpgrade.cs` / `LabyrinthUpgradeSystem.cs` | Upgrade definitions and the 1-of-3 roll/apply logic between stages. |
| `LabyrinthUpgradeScreen.cs` | Prefab-authored upgrade choice UI (pauses time while open). |
| `LabyrinthHud.cs` | Run HUD: timer, score, vitals, spell slots with cooldowns, dwell bar, mana-fail flash, run-over panel. |

## Hoops (`Minigames/Hoops/`)

| Script | Purpose |
|---|---|
| `HoopsGame.cs` | 60-second free-shoot round: active-hoop selection, goal-staged difficulty (static → sliding → wild + winged), streaks, item × hoop-difficulty scoring, out-of-bounds return (2s grace), audio hooks, ticket recording. |
| `HoopsScoreZone.cs` | Hoop trigger: pole/backboard slide movement, winged flight mode, active-target pulse, score punch/flash/burst feedback. |
| `HoopsThrowable.cs` | Basketball physics for throwable balls: bounce material, flight trail, impact squash, fall reset, per-ball point value. |
| `HoopsScorable.cs` | Marks any prop as throwable-for-points with its own value. |
| `HoopsHud.cs` | Round HUD: score, pulsing countdown, active target line with difficulty/stage tag, streak meter, results. |

## Atom Smasher (`Minigames/AtomSmasher/`)

| Script | Purpose |
|---|---|
| `AtomSmasherGame.cs` | Wave orchestrator: shots/timer rules, target shuffle, obstruction/moving/quantum/special spawning, chain multipliers, hitstop + popups + camera shake, wave-clear beat, ticket recording. |
| `AtomSmasherLauncher.cs` | Mouse-aimed launcher with trajectory arc; follows the player anchor. |
| `AtomSmasherBall.cs` | Plane-locked bouncy ball: drain/settle/lifetime rules, boost visuals, spawn pop and impact squash. |
| `AtomSmasherTarget.cs` | Base atom: score value, hit color, death pop, reset for wave replays. |
| `AtomSmasherMovingTarget.cs` | Patrol motion for moving atoms. |
| `AtomSmasherQuantumTarget.cs` | Marks an atom quantum-charged (split ball / speed boost on smash). |
| `AtomSmasherUnstableTarget.cs` | Vibrating atom that only dies to rebound shots; deflects direct hits. |
| `AtomSmasherExplosiveTarget.cs` | Detonates an area and consumes the ball when smashed. |
| `AtomSmasherStaticBar.cs` / `AtomSmasherMovingBar.cs` | Static and ping-pong obstruction bars. |
| `AtomSmasherRotator.cs` | Spins an obstruction around the board axis (rotor cross). |
| `AtomSmasherBumper.cs` | Player-actuated pinball flipper: press to swat balls with rebound impulse. |
| `AtomSmasherPitTrap.cs` | Pit floor cover: solid through wave 5, then one random pit opens per wave (wave 10+: left / right / both). Balls drain through open pits. |
| `AtomSmasherBlackHole.cs` | Gravity-well obstruction (wave 3+): curves shots, swallows balls past the event horizon, vacuums electron sparks board-wide. |
| `AtomSmasherElectron.cs` | Procedural spark particle released on smashes; bounces off walls, pulled in by black holes. |
| `AtomSmasherCameraFx.cs` | Trauma shake + FOV kick on the standalone board camera (never the player rig). |
| `AtomSmasherHud.cs` | Board HUD: score, wave, targets, chain, timer, status, results, and the Peggle-style ball rack (icon per remaining ball, +N overflow). |

## Editor tooling (`Scripts/Editor/`, editor-only)

| Script | Purpose |
|---|---|
| `ArcadeGen3DEditor.cs` (`0_Jd/Editor/`) | Inspector conveniences for the maze generator. |
| `GameplayTweaksSetup.cs` | One-shot wiring: laser beam retune, pit-cover wave settings in Sc_AtomSmasher, and the ball rack widget in the AtomSmasherHud prefab. |
| `BlackHoleTrapSetup.cs` | Builds the black hole prefab/materials and registers it as an obstacle option in Sc_AtomSmasher. |
| `MainMenuSetup.cs` | Builds the MainMenu + PauseMenu canvas prefabs, bakes the damage overlay into the player prefab, creates Sc_MainMenu, and registers it first in Build Settings. Run via `Sol → Setup → Menus And UI Prefabs`. |
