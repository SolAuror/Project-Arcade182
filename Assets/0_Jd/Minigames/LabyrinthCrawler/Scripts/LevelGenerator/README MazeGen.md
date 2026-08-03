# Arcade Maze Generation

`ArcadeGen3D` is the shared Unity 6 grid generator used by both the arcade hub and Labyrinth Crawler. `Dun_Gen2D` is an older demonstration generator and is not part of the crawler runtime.

## Active 3D types

| Type | Role |
|---|---|
| `ArcadeGen3D` | Builds and carves the room grid, runs crawler-only topology passes, classifies cells, and invokes completion callbacks. |
| `ArcadeMazeRules` | Per-generation rules object. The crawler supplies one each floor; a null rules lane preserves the hub's authored setup. |
| `Room3D` | Stores grid index, wall references/open state, pit/building flags, and final `SpaceType`. |
| `WallSocket` | Selects authored solid/open/outer wall models during cosmetic dressing. |
| `RoomDecorSocket` | Selects cell-level cosmetic crowns after topology is final. |
| `BuildingComponent` / `BuildingPlanUtility` | Shared authored/runtime building representation and seeded footprint/height/roof/entrance planner. |

All generated rooms are parented beneath `Generated Rooms` in the generator's local space.

## Generation entry points

```csharp
[SerializeField] private ArcadeGen3D maze;

public void RegenerateAuthoredMaze()
{
    maze.CreateArcade();
}

public bool GenerateCrawlerFloor(ArcadeMazeRules rules, Action onReady)
{
    return maze.GenerateWithRules(rules, onReady);
}
```

- `CreateArcade()` uses the generator's inspector-authored hub settings.
- `GenerateWithRules(...)` uses a temporary rules object and clears it before invoking the completion callback.
- `RegenerateMazeFromInspector()` supports edit-mode preview through the same completion path.
- Runtime R-key regeneration occurs only when `Allow Runtime Keyboard Regenerate` is enabled. It is disabled on the crawler prefab.

## Core algorithm

The corridor carve is iterative recursive backtracking:

1. Instantiate one `Room3D` per active cell.
2. Mark the start visited and push it onto a stack.
3. Choose a random unvisited walkable cardinal neighbor.
4. Open both sides of the shared wall, mark the neighbor, and push it.
5. Pop when no unvisited neighbor remains.
6. Finish when the stack is empty.

This creates a spanning tree (a perfect maze) over the walkable cells. The crawler can then braid selected dead ends to introduce loops.

## Crawler rules lane

Before carving, the crawler rules lane:

- grows a connected organic footprint from the center;
- chooses a far active exit;
- reserves pits;
- creates seeded building plans and reserves their footprints;
- rejects obstacle placements when a breadth-first connectivity guard says the remaining walkable region would split.

After carving, it runs these passes in order:

1. braid eligible dead ends;
2. open plaza interiors;
3. reveal pit floors and join adjacent shafts;
4. mark and open procedural building ground-floor halls;
5. instantiate authored buildings;
6. classify every active cell;
7. dress shared walls and decor;
8. materialize `BuildingComponent` upper floors and roofs;
9. assert start-to-exit reachability in editor/development builds.

The rules-null hub lane leaves crawler-only counts at zero/full-rectangle defaults and returns before their random draws. This protects the hub's historical generation sequence.

## Room prefab requirements

Every selectable room prefab must:

- have `Room3D` on its root;
- assign north, south, east, and west wall GameObjects;
- expose enabled renderers with dimensions matching the other room variants;
- use compatible `WallSocket` components when wall dressing is enabled.

`Roof Object` is optional. `Spawn Weight` controls random regular-room selection; zero keeps a prefab valid but removes it from weighted selection.

Wall-dependent decor should be parented under its owning wall so opening that wall also hides the decor.

## Buildings

`BuildingPlanUtility` creates one deterministic plan from its local seed:

- a connected organic/L/T-style footprint inside the requested bounds;
- per-column full heights;
- clustered optional half-storeys;
- street-facing entrances;
- roof types/yaws that are supported by the assigned kit.

`ArcadeGen3D` uses the footprint for obstacle-safe placement, opens the planned ground-floor hall and entrances after carving, and delegates the visible structure to `BuildingComponent`. The superseded per-cell massing implementation is compiled out under `#if false` temporarily for comparison and should not receive new work.

## Space classification

After topology is final, each cell is assigned one `SpaceType`:

- `None`
- `NarrowStreet`
- `Plaza`
- `BuildingInterior`
- `SolidBuilding`
- `Pit`

Gameplay systems should query `GetSpaceType(...)` instead of re-deriving categories from raw masks.

## Legacy 2D generator

`Dun_Gen2D` remains for the old 2D demonstration only. It still uses `UnityEngine.Input.GetKeyDown(KeyCode.R)` and assumes legacy camera/prefab setup, so it is not compatible with the project's Input System-only runtime without modernization. Do not use it for new crawler work.

## Troubleshooting

- **No rooms:** assign at least one prefab with `Room3D` and enabled renderers.
- **Overlaps/gaps:** make every room variant's renderer bounds agree.
- **Missing openings:** verify all four `Room3D` wall references.
- **Indoor exit unreachable:** verify planned entrances face a walkable street and run the development reachability assertion.
- **Walls doubled:** enable `De Double Shared Walls` only when the wall meshes render correctly from their owning side.
- **No crawler buildings:** the crawler rules must request procedural buildings; the edit-mode rules-null preview intentionally has none.
