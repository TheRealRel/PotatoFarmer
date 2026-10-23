// Auðhumla's Lick: a salt lick. Tamed animals within its reach stay fed longer between meals, so a trough's food lasts
// longer and they stay ready to breed. Nothing is stored: the game's own "is it hungry" check is stretched while the
// animal is near a lick, so other mods that change how long animals stay fed (like LetMeTameYou) still count.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PotatoFarmer
{
    public class Lick : MonoBehaviour, Hoverable
    {
        public const float Size = 1f;
        private static readonly List<Lick> all = new List<Lick>();
        private ZNetView view;

        private void Awake()
        {
            view = GetComponent<ZNetView>();
            // The placement ghost has no network object; it shouldn't feed anything.
            if (view && view.GetZDO() != null) all.Add(this);
        }

        private void OnDestroy() => all.Remove(this);

        internal static bool Near(Vector3 position)
        {
            float reach = Plugin.LickReach.Value;
            float reach2 = reach * reach;
            for (int i = 0; i < all.Count; i++)
                if (all[i] && (all[i].transform.position - position).sqrMagnitude <= reach2) return true;
            return false;
        }

        public float GetHoverOffset() => 0f;

        public string GetHoverName() => Localization.instance.Localize(Plugin.LickName);

        public string GetHoverText()
        {
            float reach = Plugin.LickReach.Value;
            int tamed = 0;
            foreach (var character in Character.GetAllCharacters())
                if (character && character.IsTamed() && (character.transform.position - transform.position).sqrMagnitude <= reach * reach) tamed++;
            float times = Plugin.LickFedTime.Value;
            string text = $"{Plugin.LickName}\nTamed animals within {reach:0} m stay fed {times:0.#}x as long\n{tamed} tamed nearby";
            return Localization.instance.Localize(text);
        }

        [HarmonyPatch(typeof(Tameable), nameof(Tameable.IsHungry))]
        private static class StayFed
        {
            private static readonly AccessTools.FieldRef<Tameable, ZNetView> View = AccessTools.FieldRefAccess<Tameable, ZNetView>("m_nview");
            private static readonly AccessTools.FieldRef<Tameable, Character> Body = AccessTools.FieldRefAccess<Tameable, Character>("m_character");

            // Only when the game says hungry, for a tamed animal near a lick: hungry again only after the longer time.
            private static void Postfix(Tameable __instance, ref bool __result)
            {
                if (!__result) return;
                double left = FedLeft(__instance);
                if (left > 0) __result = false;
            }

            // Seconds this animal stays fed thanks to a lick: -1 if no lick is near, it was never fed, or it's past
            // even the longer time.
            internal static double FedLeft(Tameable tame)
            {
                if (all.Count == 0 || Plugin.LickFedTime.Value <= 1f) return -1;
                var body = Body(tame);
                if (!body || !body.IsTamed() || !Near(body.transform.position)) return -1;
                var zdo = View(tame)?.GetZDO();
                if (zdo == null || !ZNet.instance) return -1;
                long last = zdo.GetLong(ZDOVars.s_tameLastFeeding, 0L);
                if (last <= 0L) return -1; // Never fed: a lick doesn't stand in for food.
                double since = (ZNet.instance.GetTime() - new System.DateTime(last)).TotalSeconds;
                double left = tame.m_fedDuration * Plugin.LickFedTime.Value - since;
                return left > 0 ? left : -1;
            }
        }

        // A tamed animal near a lick says how long it stays fed, so the lick's effect can be seen.
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.GetHoverText))]
        private static class FedHover
        {
            private static void Postfix(Tameable __instance, ref string __result)
            {
                if (string.IsNullOrEmpty(__result)) return;
                double left = StayFed.FedLeft(__instance);
                if (left <= 0) return;
                var time = System.TimeSpan.FromSeconds(left);
                string text = time.TotalHours >= 1 ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes}:{time.Seconds:00}";
                __result += Localization.instance.Localize($"\nFed for {text} more ({Plugin.LickName})");
            }
        }
    }
}
