# PixelDungeon (Unity)

Builds tile prefabs in Unity from the dungeon 3D tile textures made by `dungeon-tile` (`export/dungeon3d/<theme>.png` + `.json`).
Unity 2021.3 or later, works with both Built-in and URP. The spec (object sizes, face directions) is in `.claude/skills/dungeon-tile/references/object-spec.md`.

## Installation
Copy the whole folder anywhere under your game project's `Assets/` (e.g. `Assets/PixelDungeon/`). Keep the `Editor/` folder name as is.

## Usage
1. Import `export/dungeon3d/<theme>.png` and `<theme>.json` into the **same folder** (e.g. `Assets/Dungeon/`).
   - Because a JSON with the same name sits next to the PNG, the import options are set automatically: Point filter, mipmaps off, no compression, Clamp, NPOT kept.
2. In the Project window, right-click the JSON → **`Create Tile Prefabs`**.
3. Results (in `<json folder>/<theme>_tiles/`):

| Asset | Contents |
|---|---|
| `Wall_1.prefab` ~ `Wall_4.prefab` | Wall 1 × 1 × 0.25. Pivot at the bottom center. BoxCollider |
| `Floor_1.prefab` ~ `Floor_4.prefab` | Floor 1 × 0.25 × 1. Pivot at the bottom center. BoxCollider |
| `FloorEdge_1.prefab` ~ `FloorEdge_4.prefab` | Floor edge dither decal (moss, dust) 1 × 1, a flat quad facing up. Pivot at the center. No collider, no shadows. Uses `<theme>_decal_L1.mat` (alpha cutout, decal layer 1). Only created when the JSON has `floor_edge` |
| `Door.prefab` | A door tile the same size as a wall (pivot at the bottom center). Children: `FrameLeft`, `FrameRight` (0.0625 each), `Hinge` → `Leaf` (0.875 × 1 × 0.21875). `DungeonDoor` component |
| `<theme>_TileSet.asset` | Prefab list (`walls[]`, `floors[]`, `door`, `floorEdges[]`) plus `RandomWall()`, `RandomFloor()`, `AddFloorEdges()` |
| `<theme>.mat`, `<theme>_decal_L1.mat`, `Meshes/*.asset` | Shared materials (opaque; decal layer 1) and meshes |

- Running it again after a texture update only refreshes the meshes, material, and prefab contents, so placed instances keep their references.
- From code: `DungeonTilePrefabMenu.CreateFor(jsonTextAsset, "Assets/Dungeon/temple.json")` (Editor only)

## Doors
- `DungeonDoor.SetOpen(true)` / `Toggle()`: rotates the leaf `openAngle` (default 90°) around the `Hinge` toward −Z (the camera, the room side). There are no separate open and closed textures.
- In the Inspector, toggling `isOpen` shows the result in the editor as well.
- For a door in a wall that runs north–south, rotate the whole `Door` prefab 90° around Y.

## Floor edge decals
Moss and dust where a floor meets a wall. Put one decal on each side of a floor cell whose neighbor is a wall (a floor walled on all 4 sides gets 4).

```csharp
var floor = Instantiate(tileSet.RandomFloor(), cellCenter, Quaternion.identity);
tileSet.AddFloorEdges(floor.transform, wallNorth, wallEast, wallSouth, wallWest);
```

- Each decal is a random variant, parented to the floor, at `floorTopY + 0.002 × edgeLayer` (0.252).
- The texture's top edge faces the wall. Rotation around Y: north (+Z) 0°, east 90°, south 180°, west 270° (from the JSON `rotationBySide`).
- Don't add one on a side that leads to a door cell.

## Decal layers (carpets etc.)
Flat textures with no thickness (floor edge decals, carpets, rugs) all lie on the floor top plane, so depth can't order them. They are ordered by **decal layer** instead: a higher layer always covers a lower one (1 = floor edge, 2 = carpets).

- Decal materials: alpha cutout, ZTest on, **ZWrite off**, render queue `AlphaTest + layer` (`DungeonDecalLayer.RenderQueue`). Opaque walls and floors still hide decals correctly through depth.
- Height: `DungeonDecalLayer.Height(floorTopY, layer)` = floor top + 0.002 × layer.
- For a new flat decoration such as a carpet: make a quad, set up its material with `DungeonTilePrefabMenu.SetupDecalMaterial(mat, 2)` (Editor) and place it at `DungeonDecalLayer.Height(0.25f, 2)`.
- Two edge decals overlapping in a corner cell are the same layer; they are drawn to look the same in either order.

## Mesh rules
- Unity axes: +X right, +Y up, +Z away from the camera. **front faces −Z**, back faces +Z.
- Face directions: front is seen from the front, back is seen from behind, on top the front edge is at the bottom of the texture, and side is the end face seen from outside. On the right end the texture's left is the front edge; on the left end the texture's left is the back edge.
- Faces with no art:
  - The floor's back and sides reuse its front (both are 32×8).
  - The door frame's sides stretch the front's edge column of pixels.
  - Bottom faces stretch the front's bottom row of pixels.

## Placement (not decided yet)
Where to place walls within a cell and how high relative to the floor is not decided yet (object-spec.md, "Not decided yet").
The prefabs only define their own pivot (bottom center), so place them by the placement rules once those are set.
