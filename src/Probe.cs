// TEST ONLY (1.4.1 test b): logs why equipped weapons and the rack's honeycomb don't show on Tester. Removed before release.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace PotatoFarmer
{
    internal class Probe : MonoBehaviour
    {
        private static readonly string[] Slots = { "m_rightItemInstance", "m_leftItemInstance", "m_rightBackItemInstance", "m_leftBackItemInstance" };
        private static readonly HashSet<string> Logged = new HashSet<string>();
        private float next;
        private string last = "";

        private void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 5f;
            var player = Player.m_localPlayer;
            if (!player || !ObjectDB.instance) return;
            var vis = player.GetComponent<VisEquipment>();
            if (!vis) return;
            var text = new StringBuilder();
            foreach (var slot in Slots)
            {
                var instance = Traverse.Create(vis).Field(slot).GetValue<GameObject>();
                text.Append("\n  ").Append(slot).Append(": ").Append(instance ? Describe(instance) : "none");
            }
            foreach (var item in new[] { Traverse.Create(player).Field("m_rightItem").GetValue<ItemDrop.ItemData>(), Traverse.Create(player).Field("m_leftItem").GetValue<ItemDrop.ItemData>() })
            {
                if (item == null || !item.m_dropPrefab) continue;
                var prefab = ObjectDB.instance.GetItemPrefab(item.m_dropPrefab.name);
                text.Append("\n  prefab ").Append(item.m_dropPrefab.name).Append(" in ObjectDB: ").Append(prefab ? Describe(prefab) : "missing");
            }
            string now = text.ToString();
            if (now == last) return;
            last = now;
            Plugin.Log.LogInfo("PROBE equipped:" + now);
        }

        // The honey asset and the copy the rack made from it (once per meat).
        internal static void Honey(string meat, GameObject copy)
        {
            if (!Logged.Add(meat)) return;
            var prefab = ObjectDB.instance.GetItemPrefab("Honey");
            Plugin.Log.LogInfo("PROBE honey for " + meat + ":\n  asset: " + (prefab ? Describe(prefab) : "missing") + "\n  copy at " + copy.transform.position + " scale " + copy.transform.lossyScale + ": " + Describe(copy));
        }

        private static string Describe(GameObject root)
        {
            var text = new StringBuilder();
            text.Append(root.name).Append(" activeSelf=").Append(root.activeSelf).Append(" inHierarchy=").Append(root.activeInHierarchy)
                .Append(" scene=").Append(root.scene.IsValid() ? root.scene.name : "(asset)");
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var go = renderer.gameObject;
                text.Append("\n    ").Append(Path(go.transform, root.transform)).Append(" [").Append(renderer.GetType().Name)
                    .Append("] enabled=").Append(renderer.enabled).Append(" goActive=").Append(go.activeInHierarchy)
                    .Append(" visible=").Append(renderer.isVisible).Append(" layer=").Append(LayerMask.LayerToName(go.layer))
                    .Append(" mats=").Append(string.Join(",", renderer.sharedMaterials.Select(m => m ? m.name + "/" + (m.shader ? m.shader.name : "-") : "null")))
                    .Append(" comps=").Append(string.Join(",", go.GetComponents<Component>().Where(c => c && !(c is Transform) && !(c is Renderer) && !(c is MeshFilter)).Select(c => c.GetType().Name)));
            }
            var others = root.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c).Select(c => c.GetType().Name).Distinct();
            text.Append("\n    scripts in it: ").Append(string.Join(",", others));
            return text.ToString();
        }

        private static string Path(Transform part, Transform root)
        {
            var names = new List<string>();
            for (; part && part != root; part = part.parent) names.Add(part.name);
            names.Reverse();
            return names.Count == 0 ? "(root)" : string.Join("/", names);
        }
    }
}
