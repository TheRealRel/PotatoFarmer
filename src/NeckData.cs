// Test-build data capture for the hitching post's neck ring (Valheim, PotatoFarmer). The first time a creature type is
// roped each session, its skeleton and body surface are written to BepInEx/PotatoFarmer-necks/<creature>.txt, so the
// ring can be designed offline against real data. Nothing on screen changes. Off in releases (Capture = false).
//
// File format (plain text, one record per line, values separated by tabs; positions in metres in the creature's own
// space: x right, y up, z forward, relative to its root, with the root's scale undone):
//   creature  <prefab>  radius <Character radius>  head <m_head bone or ->  neck <bone the mod picks>
//   bone      <index>  <name>  <parent index or -1>  <x y z>  <rotation quaternion x y z w>
//   mesh      <index>  <renderer name>  enabled <bool>  readable <bool>  vertices <n>  bones <n>  rootbone <name>
//   meshbone  <mesh index>  <mesh bone slot>  <bone index (from the bone records) or -1>
//   bonematrix <bone index>  <16 floats, column-major>   the bone's full transform (with scale) in creature space
//   matrix    <mesh index>  <16 floats>   the renderer's full transform (with scale) in creature space
//   bind      <mesh index>  <slot>  <16 floats>   the mesh's bind pose for that bone slot (when available)
//   raw       <mesh index>  <0 or 1 = BakeMesh useScale>  <x y z>   the posed mesh exactly as BakeMesh returns it
//   local     <mesh index>  <x y z>   the mesh's rest-pose vertices (readable meshes only)
//   tri       <mesh index>  <a b c>   triangles (vertex numbers, the same for raw, local and v)
//   bake      <mesh index>  <which way of reading the posed mesh matched the renderer's bounds>  <mismatch score>
//   bounds    <mesh index>  <renderer world bounds centre x y z>  <size x y z>  (creature space, for checking)
//   v         <mesh index>  <x y z>  [<bone slot> <weight> for the two strongest bones, when the mesh is readable]
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using UnityEngine;

namespace PotatoFarmer
{
    internal static class NeckData
    {
        internal const bool Capture = true;
        private static readonly HashSet<string> saved = new HashSet<string>();

        private static string F(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
        private static string V(Vector3 v) => F(v.x) + " " + F(v.y) + " " + F(v.z);
        private static string M(Matrix4x4 m)
        {
            var b = new StringBuilder();
            for (int i = 0; i < 16; i++) b.Append(i == 0 ? "" : " ").Append(F(m[i]));
            return b.ToString();
        }

        // The posed mesh in world space. Creatures import with different scales on the renderer (Boar 1, others 0.01 or
        // less), and how BakeMesh treats that scale differs, so each way is tried and the one whose box matches the
        // renderer's own world bounds is kept. Works on meshes that aren't readable too.
        internal static Vector3[] Surface(SkinnedMeshRenderer skin, Mesh baked, out string how, out float score)
        {
            how = "-"; score = float.MaxValue;
            Vector3[] best = null;
            var target = skin.bounds;
            var full = skin.transform.localToWorldMatrix;
            var noScale = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
            foreach (bool useScale in new[] { false, true })
            {
                Vector3[] local;
                try { skin.BakeMesh(baked, useScale); local = baked.vertices; }
                catch { continue; }
                if (local == null || local.Length == 0) continue;
                foreach (bool scaled in new[] { true, false })
                {
                    var place = scaled ? full : noScale;
                    var points = new Vector3[local.Length];
                    var box = new Bounds(place.MultiplyPoint3x4(local[0]), Vector3.zero);
                    for (int i = 0; i < local.Length; i++) box.Encapsulate(points[i] = place.MultiplyPoint3x4(local[i]));
                    float size = Mathf.Max(target.size.magnitude, 0.001f);
                    float s = Mathf.Abs(Mathf.Log(Mathf.Max(box.size.magnitude, 0.00001f) / size)) + (box.center - target.center).magnitude / size;
                    if (s < score) { score = s; best = points; how = (useScale ? "scaled bake" : "unscaled bake") + (scaled ? ", full transform" : ", no scale"); }
                }
            }
            return best;
        }

        internal static void Save(Character animal, Transform head, Transform neck)
        {
            if (!Capture || !animal) return;
            string name = Utils.GetPrefabName(animal.gameObject);
            if (!saved.Add(name)) return;
            try
            {
                var root = animal.transform;
                // The creature's own space without its scale: positions in metres, the same for every size of creature.
                var space = Matrix4x4.TRS(root.position, root.rotation, Vector3.one).inverse;
                var rootRotation = Quaternion.Inverse(root.rotation);
                var text = new StringBuilder();
                text.Append($"creature\t{name}\tradius\t{F(animal.GetRadius())}\thead\t{(head ? head.name : "-")}\tneck\t{(neck ? neck.name : "-")}\n");

                var bones = root.GetComponentsInChildren<Transform>(true).Where(t => t != root).ToList();
                var index = new Dictionary<Transform, int>();
                for (int i = 0; i < bones.Count; i++) index[bones[i]] = i;
                for (int i = 0; i < bones.Count; i++)
                {
                    var b = bones[i];
                    int parent = b.parent && index.TryGetValue(b.parent, out int p) ? p : -1;
                    var q = rootRotation * b.rotation;
                    text.Append($"bone\t{i}\t{b.name}\t{parent}\t{V(space.MultiplyPoint3x4(b.position))}\t{F(q.x)} {F(q.y)} {F(q.z)} {F(q.w)}\n");
                }
                for (int i = 0; i < bones.Count; i++) text.Append($"bonematrix\t{i}\t{M(space * bones[i].localToWorldMatrix)}\n");

                var baked = new Mesh();
                var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int m = 0; m < skins.Length; m++)
                {
                    var skin = skins[m];
                    var shared = skin.sharedMesh;
                    bool readable = shared && shared.isReadable;
                    text.Append($"mesh\t{m}\t{skin.name}\tenabled\t{skin.enabled && skin.gameObject.activeInHierarchy}\treadable\t{readable}\t" +
                        $"vertices\t{(shared ? shared.vertexCount : 0)}\tbones\t{skin.bones?.Length ?? 0}\trootbone\t{(skin.rootBone ? skin.rootBone.name : "-")}\n");
                    if (skin.bones != null)
                        for (int s = 0; s < skin.bones.Length; s++)
                            text.Append($"meshbone\t{m}\t{s}\t{(skin.bones[s] && index.TryGetValue(skin.bones[s], out int bi) ? bi : -1)}\n");
                    if (!shared) continue;
                    BoneWeight[] weights = null;
                    if (readable)
                        try { weights = shared.boneWeights; } catch { weights = null; }
                    text.Append($"matrix\t{m}\t{M(space * skin.transform.localToWorldMatrix)}\n");
                    try
                    {
                        var binds = shared.bindposes;
                        for (int s = 0; s < binds.Length; s++) text.Append($"bind\t{m}\t{s}\t{M(binds[s])}\n");
                    }
                    catch { }
                    if (readable)
                        try { foreach (var lv in shared.vertices) text.Append($"local\t{m}\t{V(lv)}\n"); } catch { }
                    foreach (bool useScale in new[] { false, true })
                        try
                        {
                            skin.BakeMesh(baked, useScale);
                            foreach (var rv in baked.vertices) text.Append($"raw\t{m}\t{(useScale ? 1 : 0)}\t{V(rv)}\n");
                        }
                        catch { }
                    try
                    {
                        var tris = baked.triangles;
                        for (int t = 0; t + 2 < tris.Length; t += 3) text.Append($"tri\t{m}\t{tris[t]} {tris[t + 1]} {tris[t + 2]}\n");
                    }
                    catch (System.Exception e) { text.Append($"note\tmesh {m} triangles unavailable: {e.GetBaseException().Message}\n"); }
                    var world = Surface(skin, baked, out string how, out float score);
                    if (world == null)
                    {
                        text.Append($"note\tmesh {m} could not be baked\n");
                        continue;
                    }
                    var bounds = skin.bounds;
                    text.Append($"bake\t{m}\t{how}\t{F(score)}\n");
                    text.Append($"bounds\t{m}\t{V(space.MultiplyPoint3x4(bounds.center))}\t{V(bounds.size)}\n");
                    for (int i = 0; i < world.Length; i++)
                    {
                        text.Append($"v\t{m}\t{V(space.MultiplyPoint3x4(world[i]))}");
                        if (weights != null && i < weights.Length)
                        {
                            var w = weights[i];
                            text.Append($"\t{w.boneIndex0} {F(w.weight0)} {w.boneIndex1} {F(w.weight1)}");
                        }
                        text.Append('\n');
                    }
                }
                Object.Destroy(baked);

                string dir = Path.Combine(Paths.BepInExRootPath, "PotatoFarmer-necks");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, name + ".txt");
                File.WriteAllText(path, text.ToString());
                Plugin.Log.LogInfo($"Hitching post: neck data for {name} written to {path}.");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Hitching post: neck data for {name} failed: {e.GetBaseException().Message}");
            }
        }
    }
}
