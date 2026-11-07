// Keeps the item models our pieces show (the rack's meat, honey and jerky, the trough's food, the roost's eggs) looking
// the way the game made them, whatever another mod later does to the items themselves.
//
// Why (1.4.1): with Valheim Performance Optimizations 2.0.0 the rack showed no honeycomb. The game's Honey item is
// also a buildable piece (it can be placed), and VPO, at the end of ZNetScene.Awake, takes over every prefab with a
// Piece: it swaps the materials and switches the prefab's own renderers off, to draw the item itself. Any copy made
// from that prefab afterwards has its renderers off too, and VPO doesn't draw our copies. Read in the game's files
// and in VPO 2.0.0 (see claude/vpo-item-pieces.md).
//
// So, before anything else runs at the start of ZNetScene.Awake (the game's own item prefabs as loaded), each item
// prefab's mesh renderers are noted once: switched on or off, and their materials. A copy gets those back. Nothing
// here knows about any other mod; it only puts back the game's own look.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PotatoFarmer
{
    internal static class Looks
    {
        private struct Look
        {
            public bool Enabled;
            public Material[] Materials;
        }

        private static readonly Dictionary<Renderer, Look> Original = new Dictionary<Renderer, Look>();
        private static readonly List<Renderer> SourceParts = new List<Renderer>(), CopyParts = new List<Renderer>();
        private static bool noted;

        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        private static class NoteItems
        {
            [HarmonyPriority(Priority.First)]
            private static void Prefix(ZNetScene __instance)
            {
                // Once per game: a later world load would note what other mods did since.
                if (noted || !__instance || __instance.m_prefabs == null) return;
                noted = true;
                int count = 0;
                foreach (var prefab in __instance.m_prefabs)
                {
                    if (!prefab || !prefab.GetComponent<ItemDrop>()) continue;
                    prefab.GetComponentsInChildren(true, SourceParts);
                    foreach (var part in SourceParts)
                    {
                        if (!IsMesh(part)) continue;
                        Original[part] = new Look { Enabled = part.enabled, Materials = part.sharedMaterials };
                        count++;
                    }
                }
                SourceParts.Clear();
                Plugin.Log.LogInfo($"Noted the look of {count} item model parts, so the rack, trough and roost show items as the game made them.");
            }
        }

        // copy: a fresh Instantiate of source (the whole item, or one of its children), before Strip.
        internal static void Restore(GameObject source, GameObject copy)
        {
            if (Original.Count == 0 || !source || !copy) return;
            source.GetComponentsInChildren(true, SourceParts);
            copy.GetComponentsInChildren(true, CopyParts);
            if (SourceParts.Count == CopyParts.Count)
            {
                for (int i = 0; i < SourceParts.Count; i++)
                {
                    if (!IsMesh(CopyParts[i]) || !Original.TryGetValue(SourceParts[i], out var look)) continue;
                    var part = CopyParts[i];
                    if (part.enabled != look.Enabled) part.enabled = look.Enabled;
                    if (!SameMaterials(part.sharedMaterials, look.Materials)) part.sharedMaterials = look.Materials;
                }
            }
            SourceParts.Clear();
            CopyParts.Clear();
        }

        // Particle renderers are left alone: Strip switches those off on purpose.
        private static bool IsMesh(Renderer part) => part is MeshRenderer || part is SkinnedMeshRenderer;

        private static bool SameMaterials(Material[] a, Material[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
