// Læraðr's Rack: a jerky drying rack with nine hooks. Each hook takes one raw meat and one honey; they hang there
// while they dry, then become two jerky (like the game's cauldron recipe: 1 meat + 1 honey -> 2 jerky), which hang on
// the hook until taken. No fire, no burning.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PotatoFarmer
{
    public class Rack : MonoBehaviour, Hoverable, Interactable
    {
        private const string HooksKey = "PotatoRack.Hooks";
        internal const int Hooks = 9;
        // The model file is 1.9 m long; drawn twice that (3.8 m long, 2.2 m tall, hooks 1.65 m up), so the game's big
        // meats (serpent, Bone Maw, volture) hang at full size like on the game's iron spit.
        internal const float Size = 2f;
        // The nine hook tips in the model file (measured from rack.mesh: the hooks' metal, lowest points), three on
        // each crossbar. The crossbars run along x.
        private static readonly float[] HookX = { -0.307f, 0.100f, 0.448f }, HookZ = { -0.47f, 0f, 0.475f };
        private const float HookY = 0.825f, SpaceAlong = 0.41f, SpaceAcross = 0.47f, HoneyScale = 0.55f;

        private static readonly Dictionary<string, Bounds> modelBounds = new Dictionary<string, Bounds>();
        private static Dictionary<string, KeyValuePair<string, int>> jerkyFor;
        private static ObjectDB jerkyForDb;
        private static CookingStation spit;

        private ZNetView view;
        private float nextCheck;
        private readonly string[] shown = new string[Hooks];
        private readonly GameObject[] hookModels = new GameObject[Hooks];

        private bool Valid => view && view.IsValid();

        private struct Hook
        {
            public string Meat;
            public long Start;
            public bool Empty => string.IsNullOrEmpty(Meat);
        }

        private void Awake()
        {
            view = GetComponent<ZNetView>();
            if (!view || view.GetZDO() == null) return;
            view.Register<string>("PotatoRack_Add", RPC_Add);
            view.Register("PotatoRack_Take", RPC_Take);
            view.Register<string, int>("PotatoRack_Give", RPC_Give);
            var wear = GetComponent<WearNTear>();
            if (wear) wear.m_onDestroyed += OnDestroyed;
        }

        // ------------------------------------------------------------------ which meat makes which jerky

        // Raw meat prefab -> (jerky prefab, how many). The game's (and other mods') jerky recipes are read from the game:
        // 1 meat + 1 honey -> N jerky. Our own jerkies come after, for meats that have none.
        internal static Dictionary<string, KeyValuePair<string, int>> JerkyFor()
        {
            var db = ObjectDB.instance;
            if (jerkyFor != null && jerkyForDb == db) return jerkyFor;
            jerkyFor = new Dictionary<string, KeyValuePair<string, int>>();
            jerkyForDb = db;
            if (!db) return jerkyFor;
            foreach (var recipe in db.m_recipes)
            {
                if (!recipe || !recipe.m_item || !recipe.m_enabled || recipe.m_resources == null || recipe.m_resources.Length != 2) continue;
                if (recipe.m_item.gameObject.name.IndexOf("Jerky", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var honey = recipe.m_resources.FirstOrDefault(r => r.m_resItem && r.m_resItem.gameObject.name == "Honey");
                var meat = recipe.m_resources.FirstOrDefault(r => r.m_resItem && r.m_resItem.gameObject.name != "Honey");
                if (honey == null || meat == null || honey.m_amount != 1 || meat.m_amount != 1) continue;
                string raw = meat.m_resItem.gameObject.name;
                if (!jerkyFor.ContainsKey(raw)) jerkyFor[raw] = new KeyValuePair<string, int>(recipe.m_item.gameObject.name, Math.Max(1, recipe.m_amount));
            }
            foreach (var kind in Jerky.ByMeat.Values)
                if (!jerkyFor.ContainsKey(kind.Meat) && db.GetItemPrefab(kind.Meat) && db.GetItemPrefab(kind.Prefab))
                    jerkyFor[kind.Meat] = new KeyValuePair<string, int>(kind.Prefab, 2);
            return jerkyFor;
        }

        internal static void LogPairs()
        {
            jerkyFor = null;
            var pairs = JerkyFor();
            Plugin.Log.LogInfo($"Læraðr's Rack dries {pairs.Count} meats: " +
                string.Join(", ", pairs.Select(p => $"{p.Key} -> {p.Value.Key} x{p.Value.Value}").ToArray()));
        }

        private static CookingStation Spit()
        {
            if (spit) return spit;
            var prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab("piece_cookingstation") : null;
            spit = prefab ? prefab.GetComponent<CookingStation>() : null;
            return spit;
        }

        // ------------------------------------------------------------------ hooks: "meat@start ticks" x 9, ";"

        private Hook[] Read()
        {
            var hooks = new Hook[Hooks];
            if (!Valid) return hooks;
            var parts = view.GetZDO().GetString(HooksKey, "").Split(';');
            for (int i = 0; i < Hooks && i < parts.Length; i++)
            {
                int at = parts[i].LastIndexOf('@');
                if (at <= 0 || !long.TryParse(parts[i].Substring(at + 1), out long start)) continue;
                hooks[i] = new Hook { Meat = parts[i].Substring(0, at), Start = start };
            }
            return hooks;
        }

        private void Write(Hook[] hooks) =>
            view.GetZDO().Set(HooksKey, string.Join(";", hooks.Select(h => h.Empty ? "" : h.Meat + "@" + h.Start).ToArray()));

        private static double Age(Hook hook) => (ZNet.instance.GetTime() - new DateTime(hook.Start)).TotalSeconds;

        private static float Duration() => Plugin.RackTime.Value * 60f;

        private static bool Ready(Hook hook) => !hook.Empty && Age(hook) >= Duration();

        // ------------------------------------------------------------------ hover and use

        public float GetHoverOffset() => 0f;

        public string GetHoverName() => Localization.instance.Localize(Plugin.RackName);

        public string GetHoverText()
        {
            if (!Valid) return "";
            if (!PrivateArea.CheckAccess(transform.position, 0f, false)) return Localization.instance.Localize(Plugin.RackName + "\n$piece_noaccess");
            var hooks = Read();
            int used = hooks.Count(h => !h.Empty);
            var text = new System.Text.StringBuilder(Plugin.RackName + $" ({used}/{Hooks})");
            if (used == 0) text.Append("\n$piece_container_empty");
            // One line per meat: how many, and when the next is done.
            foreach (var group in hooks.Where(h => !h.Empty).GroupBy(h => h.Meat))
            {
                int ready = group.Count(Ready), drying = group.Count() - ready;
                string name = ItemName(group.Key);
                if (ready > 0 && JerkyFor().TryGetValue(group.Key, out var jerky))
                    text.Append($"\n{ItemName(jerky.Key)} x{ready * jerky.Value}: <color=yellow>ready</color>");
                if (drying > 0)
                {
                    double left = group.Where(h => !Ready(h)).Min(h => Duration() - Age(h));
                    text.Append($"\n{name} x{drying}: {Mathf.Max(1, Mathf.CeilToInt((float)left / 60f))} min left");
                }
            }
            if (hooks.Any(Ready)) text.Append("\n[<color=yellow><b>$KEY_Use</b></color>] Take the jerky");
            else if (used < Hooks) text.Append("\n[<color=yellow><b>$KEY_Use</b></color>] Hang raw meat and honey");
            if (used < Hooks) text.Append("\n[<color=yellow><b>1-8</b></color>] Hang meat from your hotbar");
            return Localization.instance.Localize(text.ToString());
        }

        // Use takes every jerky that's done; with none, it hangs the first meat from your inventory that dries here.
        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !Valid) return false;
            if (!PrivateArea.CheckAccess(transform.position, 0f, true)) return true;
            if (Read().Any(Ready))
            {
                view.InvokeRPC("PotatoRack_Take");
                return true;
            }
            var pairs = JerkyFor();
            var meat = user.GetInventory().GetAllItems().FirstOrDefault(i => i.m_dropPrefab && pairs.ContainsKey(i.m_dropPrefab.name));
            if (meat == null)
            {
                user.Message(MessageHud.MessageType.Center, "No raw meat that dries into jerky");
                return true;
            }
            Add(user, meat, 1);
            return true;
        }

        // From the hotbar: as many of that meat as there are free hooks and honey for.
        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            if (!Valid || item == null) return false;
            if (!PrivateArea.CheckAccess(transform.position, 0f, true)) return true;
            if (!item.m_dropPrefab || !JerkyFor().ContainsKey(item.m_dropPrefab.name))
            {
                user.Message(MessageHud.MessageType.Center, item.m_dropPrefab && item.m_dropPrefab.name == "Honey" ? "Hang a raw meat; the honey goes with it" : "That doesn't dry into jerky");
                return true;
            }
            Add(user, item, item.m_stack);
            return true;
        }

        private void Add(Humanoid user, ItemDrop.ItemData meat, int wanted)
        {
            int free = Read().Count(h => h.Empty);
            if (free == 0)
            {
                user.Message(MessageHud.MessageType.Center, "Every hook is taken");
                return;
            }
            var inventory = user.GetInventory();
            var honeyPrefab = ObjectDB.instance.GetItemPrefab("Honey");
            string honeyName = honeyPrefab ? honeyPrefab.GetComponent<ItemDrop>().m_itemData.m_shared.m_name : "$item_honey";
            int honey = inventory.CountItems(honeyName);
            if (honey == 0)
            {
                user.Message(MessageHud.MessageType.Center, "Each meat needs a honey with it");
                return;
            }
            int count = Math.Min(Math.Min(free, wanted), Math.Min(meat.m_stack, honey));
            string prefab = meat.m_dropPrefab.name, name = meat.m_shared.m_name;
            if (!inventory.RemoveItem(meat, count)) return;
            inventory.RemoveItem(honeyName, count);
            for (int i = 0; i < count; i++) view.InvokeRPC("PotatoRack_Add", prefab);
            user.Message(MessageHud.MessageType.Center, $"$msg_added {name} x{count}");
        }

        private static string ItemName(string prefab)
        {
            var item = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            var drop = item ? item.GetComponent<ItemDrop>() : null;
            return drop ? drop.m_itemData.m_shared.m_name : prefab;
        }

        // ------------------------------------------------------------------ changes, made by the rack's owner

        private void RPC_Add(long sender, string meat)
        {
            if (!Valid || !view.IsOwner()) return;
            var hooks = Read();
            int free = Array.FindIndex(hooks, h => h.Empty);
            if (free < 0 || !JerkyFor().ContainsKey(meat))
            {
                // Taken by someone else meanwhile: the meat and its honey go back.
                view.InvokeRPC(sender, "PotatoRack_Give", meat, 1);
                view.InvokeRPC(sender, "PotatoRack_Give", "Honey", 1);
                return;
            }
            hooks[free] = new Hook { Meat = meat, Start = ZNet.instance.GetTime().Ticks };
            Write(hooks);
            Spit()?.m_addEffect.Create(HookPoint(free), transform.rotation);
        }

        private void RPC_Take(long sender)
        {
            if (!Valid || !view.IsOwner()) return;
            var hooks = Read();
            var pairs = JerkyFor();
            var give = new Dictionary<string, int>();
            for (int i = 0; i < Hooks; i++)
            {
                if (!Ready(hooks[i])) continue;
                if (pairs.TryGetValue(hooks[i].Meat, out var jerky)) Count(give, jerky.Key, jerky.Value);
                else
                {
                    // Its jerky is gone (a mod removed): the meat and honey come back instead.
                    Count(give, hooks[i].Meat, 1);
                    Count(give, "Honey", 1);
                }
                hooks[i] = default;
            }
            if (give.Count == 0) return;
            Write(hooks);
            foreach (var entry in give) view.InvokeRPC(sender, "PotatoRack_Give", entry.Key, entry.Value);
        }

        private static void Count(Dictionary<string, int> counts, string prefab, int add) =>
            counts[prefab] = (counts.TryGetValue(prefab, out int n) ? n : 0) + add;

        // On the player who asked: into their inventory, or at their feet when it's full.
        private void RPC_Give(long sender, string prefab, int count)
        {
            var player = Player.m_localPlayer;
            var item = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (!item || count <= 0) return;
            var inventory = player ? player.GetInventory() : null;
            if (inventory != null && inventory.CanAddItem(item, count) && inventory.AddItem(item, count))
            {
                var name = item.GetComponent<ItemDrop>().m_itemData.m_shared.m_name;
                player.Message(MessageHud.MessageType.TopLeft, "$msg_added " + name, count, item.GetComponent<ItemDrop>().m_itemData.GetIcon());
                return;
            }
            var at = player ? player.transform.position + Vector3.up : transform.position + Vector3.up;
            for (int i = 0; i < count; i++) ItemDrop.OnCreateNew(Instantiate(item, at, Quaternion.identity));
        }

        // Broken or taken down: done hooks drop their jerky, the others their meat and honey.
        private void OnDestroyed()
        {
            if (!Valid || !view.IsOwner()) return;
            var hooks = Read();
            var pairs = JerkyFor();
            Write(new Hook[Hooks]);
            for (int i = 0; i < Hooks; i++)
            {
                if (hooks[i].Empty) continue;
                var drops = new List<KeyValuePair<string, int>>();
                if (Ready(hooks[i]) && pairs.TryGetValue(hooks[i].Meat, out var jerky)) drops.Add(jerky);
                else
                {
                    drops.Add(new KeyValuePair<string, int>(hooks[i].Meat, 1));
                    drops.Add(new KeyValuePair<string, int>("Honey", 1));
                }
                foreach (var drop in drops)
                {
                    var prefab = ObjectDB.instance.GetItemPrefab(drop.Key);
                    if (!prefab) continue;
                    for (int n = 0; n < drop.Value; n++)
                        ItemDrop.OnCreateNew(Instantiate(prefab, HookPoint(i) + UnityEngine.Random.insideUnitSphere * 0.3f, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f)));
                }
            }
        }

        // ------------------------------------------------------------------ what hangs on the hooks (looks only)

        private Vector3 HookLocal(int hook) => new Vector3(HookX[hook % 3], HookY + 0.01f, HookZ[hook / 3]) * Size;

        private Vector3 HookPoint(int hook) => transform.TransformPoint(HookLocal(hook));

        private void Update()
        {
            if (Time.time < nextCheck || !Valid || Jotunn.Managers.GUIManager.IsHeadless()) return;
            nextCheck = Time.time + 1f;
            var hooks = Read();
            for (int i = 0; i < Hooks; i++)
            {
                string state = hooks[i].Empty ? "" : hooks[i].Meat + (Ready(hooks[i]) ? ":done" : ":raw");
                if (state == shown[i]) continue;
                shown[i] = state;
                ShowHook(i, hooks[i]);
            }
        }

        private void OnDestroy()
        {
            foreach (var model in hookModels) if (model) Destroy(model);
        }

        private void ShowHook(int index, Hook hook)
        {
            if (hookModels[index]) Destroy(hookModels[index]);
            hookModels[index] = null;
            if (hook.Empty || !ObjectDB.instance) return;
            // Built while switched off so the copies' own scripts never start, then switched on once stripped.
            var holder = new GameObject("Hook" + index);
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            hookModels[index] = holder;
            var at = HookLocal(index);
            float along = SpaceAlong * Size * 1.3f, across = SpaceAcross * Size, down = HookY * Size - 0.05f;
            if (!Ready(hook))
            {
                var meat = Copy(hook.Meat, holder.transform, out var meatBounds);
                if (!meat) return;
                var fit = Hanging.Place(meatBounds, at, along, across, down, true, Hanging.SignFor(hook.Meat));
                Apply(meat, fit, 1f, at);
                // The honey hangs on the same hook, smaller, flat against the meat's outer side.
                var honey = Copy("Honey", holder.transform, out var honeyBounds);
                if (honey)
                {
                    var honeyFit = Hanging.Place(honeyBounds, at, along, across, down, true, Hanging.SignFor("Honey"));
                    int outward = at.z >= 0f ? 1 : -1;
                    float lowZ = at.z + (honeyFit.Low.z - at.z) * HoneyScale, highZ = at.z + (honeyFit.High.z - at.z) * HoneyScale;
                    float face = outward > 0 ? fit.High.z : fit.Low.z;
                    float shift = face - (outward > 0 ? lowZ : highZ) - outward * 0.01f * Size;
                    Apply(honey, honeyFit, HoneyScale, at);
                    honey.transform.localPosition += new Vector3(0f, 0f, shift);
                }
            }
            else if (JerkyFor().TryGetValue(hook.Meat, out var jerky))
            {
                // Two jerkies side by side on the hook, barely touching.
                for (int k = 0; k < 2; k++)
                {
                    var copy = Copy(jerky.Key, holder.transform, out var bounds);
                    if (!copy) break;
                    var fit = Hanging.Place(bounds, at, along / 2f, across, down, false, Hanging.SignFor(jerky.Key));
                    Apply(copy, fit, 1f, at);
                    float width = fit.High.x - fit.Low.x;
                    copy.transform.localPosition += new Vector3((k == 0 ? -0.5f : 0.5f) * width * 0.92f, 0f, (k - 0.5f) * 0.01f * Size);
                }
            }
            holder.SetActive(true);
        }

        // Puts a hung copy in place; extra shrinks it about the hook point (the honey).
        private static void Apply(GameObject copy, Hanging.Fit fit, float extra, Vector3 hook)
        {
            copy.transform.localRotation = fit.Rotation;
            copy.transform.localScale = copy.transform.localScale * fit.Scale * extra;
            copy.transform.localPosition = hook + (fit.Position - hook) * extra;
        }

        // A copy of an item's own model (its "attach", or "model" for items without one such as honey), stripped of
        // scripts, physics, lights, sounds and particles, casting no shadow (like the game's cooking spits). bounds: its
        // box in its own space times its own scale.
        private static GameObject Copy(string prefab, Transform parent, out Bounds bounds)
        {
            bounds = default;
            var item = ObjectDB.instance.GetItemPrefab(prefab);
            if (!item) return null;
            var source = item.transform.Find("attach") ?? item.transform.Find("model");
            if (!source) return null;
            var copy = Instantiate(source.gameObject, parent, false);
            copy.name = prefab;
            Trough.Strip(copy);
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!modelBounds.TryGetValue(prefab, out bounds))
            {
                var scale = copy.transform.localScale;
                copy.transform.localScale = Vector3.one;
                if (!Trough.LocalBounds(copy, out bounds)) bounds = new Bounds(Vector3.zero, Vector3.one * 0.3f);
                copy.transform.localScale = scale;
                bounds = new Bounds(Vector3.Scale(bounds.center, scale), Vector3.Scale(bounds.size, scale));
                modelBounds[prefab] = bounds;
            }
            copy.transform.localPosition = Vector3.zero;
            return copy;
        }
    }
}
