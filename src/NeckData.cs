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

        private static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        private static string V(Vector3 v) => F(v.x) + " " + F(v.y) + " " + F(v.z);

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
                    try { skin.BakeMesh(baked, true); }
                    catch (System.Exception e)
                    {
                        text.Append($"note\tmesh {m} could not be baked: {e.GetBaseException().Message}\n");
                        continue;
                    }
                    var place = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                    var vertices = baked.vertices;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        text.Append($"v\t{m}\t{V(space.MultiplyPoint3x4(place.MultiplyPoint3x4(vertices[i])))}");
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
