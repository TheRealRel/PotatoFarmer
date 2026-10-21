// How animals find the trough: when a hungry animal finds no food on the ground, it looks for a trough in range
// holding something it eats. The trough sets one bite in its bowl and the animal eats that the normal way.
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PotatoFarmer
{
    internal static class Feeding
    {
        private const string BaitKey = "PotatoTrough.Bait";
        private static readonly Func<MonsterAI, ItemDrop.ItemData, bool> CanConsume =
            AccessTools.MethodDelegate<Func<MonsterAI, ItemDrop.ItemData, bool>>(AccessTools.Method(typeof(MonsterAI), "CanConsume"));
        private static readonly AccessTools.FieldRef<MonsterAI, float> SearchTimer = AccessTools.FieldRefAccess<MonsterAI, float>("m_consumeSearchTimer");
        private static HashSet<string> animalFood;
        private static ZNetScene foodFrom;
        private static bool warned;

        // Every item some tameable or breedable animal eats, from the game's own data (so modded animals count).
        internal static bool IsAnimalFood(ItemDrop.ItemData item)
        {
            var scene = ZNetScene.instance;
            if (!scene) return false;
            if (animalFood == null || foodFrom != scene)
            {
                foodFrom = scene;
                animalFood = new HashSet<string>();
                foreach (var prefab in scene.m_prefabs)
                {
                    var ai = prefab ? prefab.GetComponent<MonsterAI>() : null;
                    if (!ai || ai.m_consumeItems == null || (!prefab.GetComponent<Tameable>() && !prefab.GetComponent<Procreation>())) continue;
                    foreach (var food in ai.m_consumeItems)
                        if (food && food.m_itemData?.m_shared != null) animalFood.Add(food.m_itemData.m_shared.m_name);
                }
            }
            return item?.m_shared != null && animalFood.Contains(item.m_shared.m_name);
        }

        internal static bool CanEat(MonsterAI animal, string prefab)
        {
            var item = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            var drop = item ? item.GetComponent<ItemDrop>() : null;
            return drop && CanConsume(animal, drop.m_itemData);
        }

        internal static void MarkBait(ItemDrop bite)
        {
            if (!bite) return;
            bite.m_autoPickup = false;
            var view = bite.GetComponent<ZNetView>();
            if (view && view.GetZDO() != null) view.GetZDO().Set(BaitKey, true);
        }

        internal static bool IsBait(ItemDrop drop)
        {
            var view = drop.GetComponent<ZNetView>();
            return view && view.GetZDO() != null && view.GetZDO().GetBool(BaitKey);
        }

        // Can the animal walk up to the trough? Like the game's own feeding, a route that only gets close is enough
        // (fences and the trough itself make full routes rare): the route just has to end beside the trough.
        private static readonly List<Vector3> route = new List<Vector3>();

        private static bool Reachable(MonsterAI animal, Trough trough)
        {
            var from = animal.transform.position;
            var at = trough.transform.position;
            float beside = animal.m_consumeRange + 2f * Trough.Size;
            if (Utils.DistanceXZ(from, at) <= beside) return true;
            var paths = Pathfinding.instance;
            if (!paths) return true;
            if (!paths.GetPath(from, at, route, animal.m_pathAgentType, requireFullPath: false, cleanup: false) || route.Count == 0) return false;
            return Utils.DistanceXZ(route[route.Count - 1], at) <= beside;
        }

        // Test builds: write to the log why animals near a trough do or don't eat from it (at most every 10 s each).
        internal const bool Diagnostics = false;
        private static readonly Dictionary<int, float> lastNote = new Dictionary<int, float>();

        internal static void Note(MonsterAI animal, string text)
        {
            if (!Diagnostics || !animal) return;
            int id = animal.GetInstanceID();
            if (lastNote.TryGetValue(id, out float when) && Time.time - when < 10f) return;
            lastNote[id] = Time.time;
            Plugin.Log.LogInfo($"[Trough test] {animal.name.Replace("(Clone)", "")}: {text}");
        }

        private static Trough Nearest(MonsterAI animal, out float distance)
        {
            distance = float.MaxValue;
            Trough nearest = null;
            foreach (var trough in Trough.All)
            {
                if (!trough) continue;
                float d = Vector3.Distance(animal.transform.position, trough.transform.position);
                if (d < distance) { distance = d; nearest = trough; }
            }
            return nearest;
        }

        // Every frame for animals that eat: notes the state of ones within 30 m of a trough.
        [HarmonyPatch(typeof(MonsterAI), "UpdateConsumeItem")]
        private static class Watch
        {
            private static void Prefix(MonsterAI __instance)
            {
                if (!Diagnostics || Trough.All.Count == 0 || __instance.m_consumeItems == null || __instance.m_consumeItems.Count == 0) return;
                var trough = Nearest(__instance, out float distance);
                if (!trough || distance > Plugin.TroughReach.Value + 10f) return;
                var tame = __instance.GetComponent<Tameable>();
                Note(__instance, $"{distance:0.0} m from a trough (reach {Mathf.Max(__instance.m_consumeSearchRange, Plugin.TroughReach.Value):0} m), tamed={__instance.GetComponent<Character>()?.IsTamed()}, hungry={(tame ? tame.IsHungry().ToString() : "n/a")}, alerted={__instance.IsAlerted()}");
            }
        }

        [HarmonyPatch(typeof(MonsterAI), "FindClosestConsumableItem")]
        private static class FromTrough
        {
            private static void Postfix(MonsterAI __instance, float maxRange, ref ItemDrop __result)
            {
                if (Trough.All.Count == 0 || CanConsume == null) return;
                // A trough draws animals from its own reach, which can be further than they'd look for food themselves.
                maxRange = Mathf.Max(maxRange, Plugin.TroughReach.Value);
                if (__result)
                {
                    if (Diagnostics) lastNote.Remove(__instance.GetInstanceID());
                    Note(__instance, $"found food on the ground: {__result.name.Replace("(Clone)", "")}");
                    return;
                }
                try
                {
                    var at = __instance.transform.position;
                    foreach (var trough in Trough.All.Where(trough => trough).OrderBy(trough => Vector3.Distance(at, trough.transform.position)))
                    {
                        float distance = Vector3.Distance(at, trough.transform.position);
                        if (distance > maxRange)
                        {
                            if (Diagnostics) lastNote.Remove(__instance.GetInstanceID());
                            Note(__instance, $"hungry, but the nearest trough is {distance:0.0} m away (reach {maxRange:0} m)");
                            return;
                        }
                        string food = trough.FoodFor(__instance);
                        if (food == null)
                        {
                            if (Diagnostics) lastNote.Remove(__instance.GetInstanceID());
                            Note(__instance, $"hungry, trough {distance:0.0} m away has nothing it eats (it eats: {string.Join(", ", __instance.m_consumeItems.Where(item => item).Select(item => item.name).ToArray())})");
                            continue;
                        }
                        if (!Reachable(__instance, trough))
                        {
                            if (Diagnostics) lastNote.Remove(__instance.GetInstanceID());
                            Note(__instance, $"hungry, trough {distance:0.0} m away has {food}, but its route doesn't get close enough");
                            continue;
                        }
                        var bite = trough.Dispense(food);
                        if (Diagnostics) lastNote.Remove(__instance.GetInstanceID());
                        Note(__instance, bite ? $"hungry, took {food} from the trough {distance:0.0} m away and is walking to it" : $"hungry, asked the trough {distance:0.0} m away for {food} (waiting for a bite, or the bowl is busy)");
                        if (bite) __result = bite;
                        // The bite is on its way from another player's game: look again in a couple of seconds.
                        else SearchTimer(__instance) = Mathf.Max(SearchTimer(__instance), __instance.m_consumeSearchInterval - 2f);
                        return;
                    }
                }
                catch (Exception e)
                {
                    if (warned) return;
                    warned = true;
                    Plugin.Log.LogWarning("Trough feeding failed; animals eat off the ground as usual: " + e);
                }
            }
        }

        // A bite in the bowl is for the animals: nobody's auto-pickup grabs it while walking past.
        [HarmonyPatch(typeof(ItemDrop), "Awake")]
        private static class BaitStaysPut
        {
            private static void Postfix(ItemDrop __instance)
            {
                if (IsBait(__instance)) __instance.m_autoPickup = false;
            }
        }
    }
}
