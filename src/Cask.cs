// Heiðrún's Cask: a big mead cask that brews several batches at once, like that many fermenters in one.
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace PotatoFarmer
{
    // Works like the game's fermenter, with its recipes, brewing time, yield, effects and need for a roof, but holds
    // several mead bases at once, each brewing on its own clock. The recipes come from the game's fermenter, so modded
    // meads work too, and Potato Tweaks' fermenter speed and output settings count here as well.
    public class Cask : MonoBehaviour, Hoverable, Interactable
    {
        private const string BatchesKey = "PotatoCask.Batches";
        private const float TapDelay = 1.5f, CoverEvery = 10f;
        // The model file is 2 m long; drawn 1.5 times that (3 m long, 3.1 m tall) so it dwarfs the fermenter, which also
        // brings its 512 px textures to about 50 px/m. Where the tap is and where the roof is checked (model space).
        internal const float Size = 1.5f;
        // Just in front of the cork bung under the goat's chin, where the mead pours out.
        private static readonly Vector3 OutputPoint = new Vector3(-1.05f, 0.58f, 0f), RoofCheck = new Vector3(0f, 2.15f, 0f);

        private static Fermenter fermenter;
        private ZNetView view;
        private float nextCover, nextCheck;
        private bool exposed, hasRoof = true;

        private bool Valid => view && view.IsValid();

        private struct Batch
        {
            public string Prefab;
            public long Start;
        }

        private void Awake()
        {
            view = GetComponent<ZNetView>();
            if (!view || view.GetZDO() == null) return;
            view.Register<string, int>("PotatoCask_Add", RPC_Add);
            view.Register("PotatoCask_Tap", RPC_Tap);
            view.Register<string, int>("PotatoCask_Give", RPC_Give);
            var wear = GetComponent<WearNTear>();
            if (wear) wear.m_onDestroyed += OnDestroyed;
        }

        // ------------------------------------------------------------------ the game's fermenter: recipes and timing

        private static Fermenter Fermenter()
        {
            if (fermenter) return fermenter;
            var prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab("fermenter") : null;
            fermenter = prefab ? prefab.GetComponent<Fermenter>() : null;
            return fermenter;
        }

        private static Fermenter.ItemConversion Recipe(string prefab) =>
            Fermenter()?.m_conversion?.FirstOrDefault(c => c != null && c.m_from && c.m_from.gameObject.name == prefab);

        private static float TweaksSetting(string key)
        {
            try
            {
                // Potato Tweaks 1.5.17 on: "8 - Stations and building" / "Fermenter speed"; before: "13 - Fermenters" / "Speed".
                if (Chainloader.PluginInfos.TryGetValue("potato.tweaks", out var tweaks) && tweaks.Instance &&
                    (tweaks.Instance.Config.TryGetEntry<float>("8 - Stations and building", "Fermenter " + key.ToLowerInvariant(), out var entry) ||
                     tweaks.Instance.Config.TryGetEntry<float>("13 - Fermenters", key, out entry)))
                    return Mathf.Max(0.1f, entry.Value);
            }
            catch (Exception) { }
            return 1f;
        }

        private static float Duration() => (Fermenter() ? Fermenter().m_fermentationDuration : 2400f) / TweaksSetting("Speed");

        private static int Yield(Fermenter.ItemConversion recipe) => Math.Max(1, Mathf.RoundToInt(recipe.m_producedItems * TweaksSetting("Output")));

        // ------------------------------------------------------------------ what's inside: "prefab@start ticks;..."

        private List<Batch> Read()
        {
            var list = new List<Batch>();
            if (!Valid) return list;
            foreach (string part in view.GetZDO().GetString(BatchesKey, "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int at = part.LastIndexOf('@');
                if (at <= 0 || !long.TryParse(part.Substring(at + 1), out long start)) continue;
                list.Add(new Batch { Prefab = part.Substring(0, at), Start = start });
            }
            return list;
        }

        private void Write(List<Batch> batches) =>
            view.GetZDO().Set(BatchesKey, string.Join(";", batches.Select(b => b.Prefab + "@" + b.Start).ToArray()));

        // With mixing off, the one kind of mead base a cask that isn't empty takes; null when anything goes.
        private static string Brewing(List<Batch> batches) => Plugin.CaskMix.Value || batches.Count == 0 ? null : batches[0].Prefab;

        private static double Age(Batch batch) => (ZNet.instance.GetTime() - new DateTime(batch.Start)).TotalSeconds;

        private static bool Ready(Batch batch) => Age(batch) >= Duration();

        // ------------------------------------------------------------------ roof, checked like the fermenter's

        private void Update()
        {
            if (!Valid || Time.time < nextCheck) return;
            nextCheck = Time.time + 2f;
            UpdateCover(false);
        }

        private void UpdateCover(bool now)
        {
            if (!now && Time.time < nextCover) return;
            nextCover = Time.time + CoverEvery;
            Cover.GetCoverForPoint(transform.TransformPoint(RoofCheck * Size), out float cover, out bool underRoof);
            exposed = cover < 0.7f;
            hasRoof = underRoof;
            // Out in the open, brewing starts over, as in the game's fermenter.
            if ((exposed || !hasRoof) && view.IsOwner())
            {
                var batches = Read();
                bool changed = false;
                long now2 = ZNet.instance.GetTime().Ticks;
                for (int i = 0; i < batches.Count; i++)
                {
                    if (Ready(batches[i])) continue;
                    batches[i] = new Batch { Prefab = batches[i].Prefab, Start = now2 };
                    changed = true;
                }
                if (changed) Write(batches);
            }
        }

        // ------------------------------------------------------------------ hover and use

        public float GetHoverOffset() => 0f;

        public string GetHoverName() => Localization.instance.Localize(Plugin.CaskName);

        public string GetHoverText()
        {
            if (!Valid) return "";
            if (!PrivateArea.CheckAccess(transform.position, 0f, false)) return Localization.instance.Localize(Plugin.CaskName + "\n$piece_noaccess");
            var batches = Read();
            var text = new System.Text.StringBuilder(Plugin.CaskName + $" ({batches.Count}/{Plugin.CaskBatches.Value})");
            if (batches.Count == 0) text.Append("\n$piece_container_empty");
            float duration = Duration();
            foreach (var batch in batches)
            {
                var recipe = Recipe(batch.Prefab);
                string name = recipe != null && recipe.m_from ? recipe.m_from.m_itemData.m_shared.m_name : batch.Prefab;
                if (Ready(batch)) text.Append($"\n{name}: <color=yellow>$piece_fermenter_ready</color>");
                else if (!hasRoof) text.Append($"\n{name}: $piece_fermenter_needroof");
                else if (exposed) text.Append($"\n{name}: $piece_fermenter_exposed");
                else text.Append($"\n{name}: {Mathf.CeilToInt((float)(duration - Age(batch)) / 60f)} min left");
            }
            if (batches.Any(Ready)) text.Append("\n[<color=yellow><b>$KEY_Use</b></color>] $piece_fermenter_tap");
            else if (batches.Count < Plugin.CaskBatches.Value) text.Append("\n[<color=yellow><b>$KEY_Use</b></color>] $piece_fermenter_add");
            if (batches.Count < Plugin.CaskBatches.Value) text.Append("\n[<color=yellow><b>1-8</b></color>] Add mead bases from your hotbar");
            return Localization.instance.Localize(text.ToString());
        }

        // Use taps every ready batch; with none ready, it adds one mead base from your inventory, like the fermenter.
        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !Valid) return false;
            if (!PrivateArea.CheckAccess(transform.position, 0f, true)) return true;
            if (Read().Any(Ready))
            {
                view.InvokeRPC("PotatoCask_Tap");
                return true;
            }
            string brewing = Brewing(Read());
            var item = Fermenter()?.m_conversion?.Where(c => c != null && c.m_from && (brewing == null || c.m_from.gameObject.name == brewing))
                .Select(c => user.GetInventory().GetItem(c.m_from.m_itemData.m_shared.m_name)).FirstOrDefault(i => i != null);
            if (item == null)
            {
                user.Message(MessageHud.MessageType.Center, "$msg_noprocessableitems");
                return true;
            }
            Add(user, item, 1);
            return true;
        }

        // From the hotbar: as many of that stack as there's room for.
        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            if (!Valid || item == null) return false;
            if (!PrivateArea.CheckAccess(transform.position, 0f, true)) return true;
            if (!item.m_dropPrefab || Recipe(item.m_dropPrefab.name) == null)
            {
                user.Message(MessageHud.MessageType.Center, "That doesn't brew");
                return true;
            }
            Add(user, item, item.m_stack);
            return true;
        }

        private void Add(Humanoid user, ItemDrop.ItemData item, int wanted)
        {
            UpdateCover(true);
            if (!hasRoof)
            {
                user.Message(MessageHud.MessageType.Center, "$piece_fermenter_needroof");
                return;
            }
            if (exposed)
            {
                user.Message(MessageHud.MessageType.Center, "$piece_fermenter_exposed");
                return;
            }
            var batches = Read();
            string brewing = Brewing(batches);
            if (brewing != null && brewing != item.m_dropPrefab.name)
            {
                user.Message(MessageHud.MessageType.Center, "The cask is brewing " + (Recipe(brewing)?.m_from?.m_itemData.m_shared.m_name ?? brewing));
                return;
            }
            int room = Plugin.CaskBatches.Value - batches.Count;
            if (room <= 0)
            {
                user.Message(MessageHud.MessageType.Center, "The cask is full");
                return;
            }
            int count = Math.Min(room, Math.Min(wanted, item.m_stack));
            string prefab = item.m_dropPrefab.name, name = item.m_shared.m_name;
            if (!user.GetInventory().RemoveItem(item, count)) return;
            view.InvokeRPC("PotatoCask_Add", prefab, count);
            user.Message(MessageHud.MessageType.Center, $"$msg_added {name} x{count}");
        }

        // ------------------------------------------------------------------ changes, made by the cask's owner

        private void RPC_Add(long sender, string prefab, int count)
        {
            if (!Valid || !view.IsOwner() || count <= 0 || Recipe(prefab) == null) return;
            var batches = Read();
            string brewing = Brewing(batches);
            int take = brewing != null && brewing != prefab ? 0 : Math.Min(count, Math.Max(0, Plugin.CaskBatches.Value - batches.Count));
            long now = ZNet.instance.GetTime().Ticks;
            for (int i = 0; i < take; i++) batches.Add(new Batch { Prefab = prefab, Start = now });
            if (take > 0)
            {
                Write(batches);
                Fermenter()?.m_addedEffects.Create(transform.position, transform.rotation);
            }
            // Filled up by someone else meanwhile: the rest goes back.
            if (count > take) view.InvokeRPC(sender, "PotatoCask_Give", prefab, count - take);
        }

        private void RPC_Tap(long sender)
        {
            if (!Valid || !view.IsOwner()) return;
            var batches = Read();
            var ready = batches.Where(Ready).ToList();
            if (ready.Count == 0) return;
            Write(batches.Where(b => !Ready(b)).ToList());
            Fermenter()?.m_tapEffects.Create(transform.position, transform.rotation);
            StartCoroutine(Pour(ready.Select(b => b.Prefab).ToList()));
        }

        private System.Collections.IEnumerator Pour(List<string> prefabs)
        {
            yield return new WaitForSeconds(TapDelay);
            var at = transform.TransformPoint(OutputPoint * Size);
            Fermenter()?.m_spawnEffects.Create(at, Quaternion.identity);
            float lift = 0.3f;
            foreach (string prefab in prefabs)
            {
                var recipe = Recipe(prefab);
                if (recipe == null || !recipe.m_to) continue;
                for (int i = 0; i < Yield(recipe); i++)
                {
                    ItemDrop.OnCreateNew(Instantiate(recipe.m_to, at + Vector3.up * lift, Quaternion.identity));
                    lift += 0.05f;
                }
            }
        }

        // On the player who asked: mead bases that didn't fit go back into their inventory, or at their feet.
        private void RPC_Give(long sender, string prefab, int count)
        {
            var player = Player.m_localPlayer;
            var item = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (!item || count <= 0) return;
            var inventory = player ? player.GetInventory() : null;
            if (inventory != null && inventory.CanAddItem(item, count) && inventory.AddItem(item, count)) return;
            var at = player ? player.transform.position + Vector3.up : transform.position + Vector3.up;
            for (int i = 0; i < count; i++) ItemDrop.OnCreateNew(Instantiate(item, at, Quaternion.identity));
        }

        // Broken or taken down: ready batches spill their mead, the rest their mead bases, like the fermenter.
        private void OnDestroyed()
        {
            if (!Valid || !view.IsOwner()) return;
            var batches = Read();
            Write(new List<Batch>());
            foreach (var batch in batches)
            {
                var recipe = Recipe(batch.Prefab);
                if (recipe == null) continue;
                bool ready = Ready(batch);
                var drop = ready ? recipe.m_to : recipe.m_from;
                int count = ready ? Yield(recipe) : 1;
                for (int i = 0; i < count; i++)
                {
                    var at = transform.position + Vector3.up + UnityEngine.Random.insideUnitSphere * 0.4f;
                    ItemDrop.OnCreateNew(Instantiate(drop.gameObject, at, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f)));
                }
            }
        }
    }
}
