# Fitting a rope to any creature's neck (Valheim)

How PotatoFarmer's hitching post puts a rope loop around a tied animal's neck, vanilla or modded, without per-creature setup. The code is `src/NeckRing.cs` (the fit) and `src/Post.cs` (picking bones, drawing the rope). Written up because most of it applies to anything that has to sit on a creature's body: collars, harnesses, saddles, effects.

**Bones alone don't work.** A creature's neck bone is a point somewhere inside the neck, and on some rigs it isn't even inside: the hen's `spine3` and `head` run up the back of its neck, so a ring centred on them sits on the eyes. A circle sized from `Character.GetRadius()` is too big on some animals and too small on others. The ring has to come from the body's real surface.

**Reading the posed body: the scale rule.** `SkinnedMeshRenderer.BakeMesh(mesh, true)` gives the current pose in the renderer's own space *without* its scale. Put it through the renderer's full `transform.localToWorldMatrix`. `BakeMesh(mesh, false)` with position and rotation only is the same thing. The other two combinations are wrong whenever the renderer is scaled, and most creatures are:

| Creature | Renderer scale |
|---|---|
| Wolf | 195 |
| Lox | 75 |
| Hen | 60 |
| Greydwarf | 50 |
| Fox (modded) | 1.95 |
| Boar | 1.3 |
| Prowler, Razorback (modded) | 1 |

This was checked against the game's own skinning math (bone matrices × bind poses × rest vertices) on every readable mesh above, and it matched to within a millimetre. Code that gets the rule wrong still works on creatures with scale 1, which makes the bug easy to miss. Baking works on meshes that aren't CPU-readable too, triangles included. Use only the most detailed level of a `LODGroup`.

**Cut the triangles, not the points.** Low-poly bodies have vertices 10-20 cm apart, so a thin slice of points can't tell neck from mane or wing. Instead:

1. Weld the baked vertices by position (0.5 mm grid). UV seams and hard edges split vertices that share a place, and without welding every outline breaks into pieces.
2. Cut the triangles with a plane square to the neck. Join cut triangles that share a crossed edge (union-find), so each separate outline (neck, fur shell, an ear, a leg) comes out as its own group of points.
3. Keep the outline that surrounds the neck's centre line. If several do, keep the biggest, so a rope goes over the fur rather than the skin under it.
4. A rope pulled tight around an outline lies on its convex hull, so the ring is the hull, pushed out by the rope's thickness. That gives the "resting on the body" look without any physics.

**Measure on the rest pose, not the live pose.** The first working version measured the body as it stood at the moment of tying. Most animals looked fine, but the result changed with the pose: the same bear got its ring right behind the head on one tie and around its shoulders and front legs on the next. The fix is to measure on the **rest (bind) pose**, the pose every skinned mesh is built around:

- For each readable skinned mesh that has the neck bone, `bindposes[slot]` maps the mesh's rest vertices (`sharedMesh.vertices`) straight into the neck bone's own space. Do that for every mesh and cut there.
- The head bone's rest position in the same space is `bindposes[neck] * inverse(bindposes[head]) * origin`.
- Multiply by the neck bone's `lossyScale` to work in metres (bones often carry the renderer's big scale). Divide the ring's centre back by it to store it in the bone's units.
- Store the ring in the neck bone's space and draw it through `neck.localToWorldMatrix`. It then follows the neck exactly the way the skin does, at any pose and any star size, and it's the same every time.

Meshes that aren't CPU-readable (the hen, the modded sheep) have no rest vertices to read. They fall back to the live-pose method below, which suits them; neither has a pose that throws it off.

**Where along the neck.** Cut planes from the neck bone to the head bone (21 steps):

- **Narrowest outline:** the ring goes where the outline is smallest, because that's where a real rope slides to. On near-ties (within 2%) it takes the one nearest the body.
- **Head end sets the size:** no ring more than 1.2× the outline at the head end, so the shoulders can't win.
- **A touch back:** finally 1.5 cm towards the body, so the ring rests on the neck rather than against the head.
- **Rounding:** low-poly hulls have long straight sides (the lox). The ring is stored as a radius at 48 angles, smoothed 8 times with a 1-2-1 kernel, and never let below 94% of the hull. The rope eases around corners and sinks slightly into fur there.
- **Live pose only:** the centre line is re-fitted through the middles of the outlines (power iteration on their spread), when that stays within about 45° of the bone line, because some rigs' bones run along the back of the neck (the hen's). And the ring stops at 85% of the way to the head bone, because in a live pose with the head lowered the cut at the head bone runs through the skull and jaw.

**One exception, by prefab name** at the top of `Post.cs`: creatures with no neck at all (Greydwarf, Greyling) wear a waist belt instead, the same fit between the two lowest spine bones.

**Getting the data.** `src/NeckData.cs` (off in releases; set `Capture = true`) writes `BepInEx/PotatoFarmer-necks/<creature>.txt` the first time each creature type is tied. Every line is one record, tab-separated, in metres, in the creature's own space (x right, y up, z forward, root scale undone):

| Record | Fields |
|---|---|
| `creature` | prefab, radius, head bone, neck bone |
| `bone` | index, name, parent, position, rotation |
| `bonematrix` | bone index, full transform (16 floats, column-major) |
| `mesh` | index, name, enabled, readable, vertex count, bone count, root bone |
| `meshbone` | mesh, bone slot, bone index |
| `matrix` | mesh, renderer's full transform |
| `bind` | mesh, slot, bind pose |
| `raw` | mesh, 0 or 1 (BakeMesh useScale), vertex as baked |
| `local` | mesh, rest-pose vertex (readable meshes only) |
| `tri` | mesh, three vertex numbers |
| `v` | mesh, vertex in creature space, the two strongest bone weights |

With `raw` and `matrix`, any reading of the mesh can be rebuilt offline. With `local`, `bind` and `bonematrix`, the game's own skinning can be recomputed as ground truth.

**How it was built.** One capture build that changed nothing on screen. Then offline work in Python on the dumped data: drawing each animal's body with the proposed ring, side view and cross-section, until all of them looked right. Only then one game build. Testing 12 creatures (vanilla and modded) this way took two capture runs and a handful of in-game checks. The rest-pose step came last, from in-game testing: an animal tied while running showed how much the live pose mattered. The earlier approach of adjusting in game and looking each time had taken far more rounds without converging.
