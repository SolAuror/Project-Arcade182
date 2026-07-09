# Insert Coin to Exit (Project-Arcade182)

A minigame collection by finn, diego and Jd.

## The Game

You are trapped inside an arcade — a maze of rooms lined with playable cabinets that rearranges itself every time you step out of a machine.

**The loop:**

1. Explore the hub maze and play the arcade machines you find.
2. Every minigame pays out **tickets** based on your score. Tickets, best scores, and unlocks persist between runs.
3. The **exit clerk** at the far end of the maze sells the **GOLDEN COIN** for **1,000,000 tickets**.
4. Carry the coin to the **Golden Exit Door** somewhere in the maze and use it to escape the arcade and beat the game.

The hub maze regenerates after every minigame, so the route to the clerk — and back to the door — is never the same twice.

**Menus:** the game boots to the *Insert Coin to Exit* main menu (Start Game / Play Minigames / Options / Quit). Each minigame unlocks for direct play from the menu after you finish it once inside the arcade. **Esc** pauses anywhere: minigames offer *Quit to Hub* (Atom Smasher also has a 2D ⇄ 3D camera toggle), the hub offers *Options / Quit to Menu / Quit Game*.

**Project layout:** each author works in their own `Assets/0_<Name>/` folder; shared player/hub assets live in `Assets/Shared/`. A master index of every script and where it runs is in [SCRIPTS.md](SCRIPTS.md).

**Scenes** (Build Settings): `Sc_MainMenu` → `Sc_ArcadeHub` → `Sc_LabyrinthCrawler` / `Sc_Hoops` / `Sc_AtomSmasher`.

---

## Games by Author

### Jd

| Game | One-liner | Docs |
|---|---|---|
| **Labyrinth Crawler** | First-person spellcasting roguelike: clear regenerating dungeon stages against growing enemy packs, pick a boon between stages, survive as long as you can. | [README](Assets/0_Jd/Scripts/Minigames/LabyrinthCrawler/README.md) |
| **Hoops** | Physical ball-throwing range: 60 seconds to sink shots into moving hoops, with streaks, stage-escalating movement, and winged bonus hoops. | [README](Assets/0_Jd/Scripts/Minigames/Hoops/README.md) |
| **Atom Smasher** | 2D pinball-launcher hybrid: smash every atom on the board across escalating waves of obstructions, hazards, black holes, and quantum effects. | [README](Assets/0_Jd/Scripts/Minigames/AtomSmasher/README.md) |

Jd also authored the hub systems: the maze generator, the arcade machine launchers, the golden-coin escape loop, and the shared player controller / grab / combat framework.

### finn

*Coming soon — scaffolding lives in `Assets/0_Finn/`.*

### diego

*Coming soon — scaffolding lives in `Assets/0_Diego/`.*
