# PotatoFarmer

Farming and animal pieces for Valheim with their own models. Gullinbursti's Trough, a bronze and wood feeding trough that animals eat from, and Gullinkambi's Roost, a black metal nesting box that gathers and hatches eggs. Player docs: [package/README.md](package/README.md).

## Layout
- `src/` – the plugin (BepInEx 5 + Jötunn). `Assets.cs` loads models and textures for any piece; `Trough.cs`, `Feeding.cs` and `Stock.cs` are the trough; `Roost.cs` is the roost (it uses `Stock.cs` too).
- `assets/` – each piece's model and textures (`<name>.mesh`, `<name>_d.png`, `<name>_n.png`, `<name>_m.png`), loaded at runtime (no Unity asset bundle).
- `tools/convert.py` – turns a `.glb` (e.g. from Meshy) into `assets/`: `python3 -I tools/convert.py model.glb assets <name> [length_m] [pixels_per_m]`. Mirrors glTF into Unity's axes, lays the long side on X, stands it on y = 0, sizes and shrinks the textures (the Potato look, below), and repacks the metal/roughness and normal maps for Valheim's `Custom/Piece` shader (metal in RGB, smoothness in alpha; normal X in alpha).
- `tools/bake.py` – for high-poly AI models (millions of triangles): `python3 -I tools/bake.py full.glb game.glb [triangles=20000] [texture_px=1024]` decimates, lays out fresh UVs (xatlas) and bakes the color and metal from the full model; run `convert.py` on the result. Needs pymeshlab, xatlas, scipy (and libopengl0 on Linux). `decimate.py` is the older, simpler version.
- `tools/even_pixels.py` – evens out a converted model's pixel size when Meshy gave some UV islands far more texture than others: convert at a larger size, then `python3 -I tools/even_pixels.py assets <name> <px_per_m>` merges each island's texels into blocks so every part lands near the same density.
- `package/` – Thunderstore files. The build copies the DLL and `assets/` into `package/plugins/`.

## Build
Tests (no game needed): `mcs -out:test/t.exe test/StockTest.cs src/Stock.cs && mono test/t.exe`, run from the repo root.

`./build.sh` with `VALHEIM_MANAGED`, `BEPINEX` (a profile's BepInEx folder with Jötunn) and optionally `CSC` set.

## The Potato look
Measured from the game's own pieces, so every model matches:
- **Texture density: about 50 pixels per metre of surface.** Valheim's forge is ~50, the reinforced chest ~35 (their textures are 64-256 px). `convert.py` works out the size from the model's surface and UV area and rounds to a power of two (the trough: 256 px, evened out with `tools/even_pixels.py`; the cask: 768 px, ~58 px/m in game).
- **Island-aware shrinking.** AI models pack many small UV islands; each new pixel takes its colour only from the island covering most of it, so neighbouring islands don't smear into blotches.
- **Sharp pixels:** point filtering with mipmaps, like the game's textures.
- **Material:** a copy of the reinforced chest's (`ironchest`, shader `Custom/Piece`), with the piece's color, normal and metal maps. Metal 1, metal gloss 0.6 (the chest uses 0.68), brightness 1.

## Adding a piece
1. Convert its `.glb` with `tools/convert.py` into `assets/` under a new name.
2. In `Plugin.cs`, clone a fitting vanilla piece with Jötunn, call `Rebuild` with `Assets.LoadMesh(name)` and a `PieceLook(name, 1f, 0.6f)`, add box colliders, and give it an "Enabled" setting in its own section.
3. Never rename a piece's prefab name after release: worlds save pieces by it.

## How the trough works
- The piece is a copy of the vanilla wooden table (for building, wear and break effects) with its looks and colliders replaced, and a copy of the reinforced chest's material with the trough's textures.
- Food is stored in the trough's ZDO as `Prefab:count;…`; only the owner changes it (add, take back, hand out a bite) through RPCs.
- A postfix on `MonsterAI.FindClosestConsumableItem` runs when an animal found nothing on the ground: the nearest reachable trough holding food it eats drops one item in its bowl, and the animal eats it the vanilla way.
- No `BepInProcess` filter, so dedicated servers load it and know the piece.
