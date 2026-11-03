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
        // Pierce: an item's top sits this far above the hook's lowest point (model units), so the hook goes into it
        // rather than the item touching it at one point. Along the bar a hanging item may overlap its neighbours a
        // little (5 %); across it stays in its row. The honeycomb hangs 30 cm and the meat's top tucks behind the lower
        // half of it. Jerkies are drawn 12 % bigger than the item.
        private const float HookY = 0.825f, Pierce = 0.035f, SpaceAlong = 0.41f, SpaceAcross = 0.47f, Overlap = 1.05f;
        private const float HoneyHeight = 0.30f, HoneyTuck = 0.5f, JerkyScale = 1.12f;
        private static readonly Dictionary<string, Vector3[]> modelPoints = new Dictionary<string, Vector3[]>();

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

        private Vector3 HookLocal(int hook) => new Vector3(HookX[hook % 3], HookY + Pierce, HookZ[hook / 3]) * Size;

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
            float along = SpaceAlong * Size * Overlap, across = SpaceAcross * Size, down = (HookY + Pierce) * Size - 0.05f;
            if (!Ready(hook))
            {
                // The honeycomb goes on the hook first; the meat hangs from the hook right under it, its top tucked
                // behind the comb's lower half, so both hang from the hook and nothing floats.
                float drop = 0f;
                var honey = Copy("Honey", holder.transform, out var honeyBounds);
                if (honey)
                {
                    var honeyFit = Hanging.Place(honeyBounds, at, along, across, down, true, Hanging.SignFor("Honey"));
                    float shrink = HoneyHeight / Mathf.Max(honeyFit.High.y - honeyFit.Low.y, 0.001f);
                    Apply(honey, honeyFit, shrink, at);
                    drop = HoneyHeight * HoneyTuck;
                }
                var meat = Copy(hook.Meat, holder.transform, out var meatBounds);
                if (meat)
                {
                    var below = at - new Vector3(0f, drop, 0f);
                    Apply(meat, Hanging.Place(meatBounds, below, along, across, down - drop, true, Hanging.SignFor(hook.Meat)), 1f, below);
                }
            }
            else if (JerkyFor().TryGetValue(hook.Meat, out var jerky))
            {
                // Two jerkies on the one hook, a little apart and one behind the other, their tops on the hook. Our
                // crossed-strip jerkies are turned in their own face so the strips read as an X, hung by the top corner.
                bool turned = Jerky.Kinds.Any(k => k.Prefab == jerky.Key && k.Shape == "deer");
                for (int k = 0; k < 2; k++)
                {
                    var copy = Copy(jerky.Key, holder.transform, out var bounds);
                    if (!copy) break;
                    var fit = Hanging.Place(bounds, at, along / 2f, across, down, false, Hanging.SignFor(jerky.Key));
                    float width = fit.High.x - fit.Low.x;
                    if (turned && Points(jerky.Key, copy) is Vector3[] points)
                    {
                        var turn = Quaternion.Euler(0f, 0f, k == 0 ? 45f : 35f);
                        var rotation = turn * fit.Rotation;
                        float scale = fit.Scale * JerkyScale;
                        var start = turn * (fit.Position * JerkyScale);
                        var top = new Vector3(0f, float.NegativeInfinity, 0f);
                        foreach (var point in points)
                        {
                            var placed = start + rotation * (point * scale);
                            if (placed.y > top.y) top = placed;
                        }
                        copy.transform.localRotation = rotation;
                        copy.transform.localScale = copy.transform.localScale * scale;
                        copy.transform.localPosition = start + at - top + new Vector3(k - 0.5f, 0f, k - 0.5f) * 0.04f * Size;
                    }
                    else
                    {
                        Apply(copy, fit, JerkyScale, at);
                        copy.transform.localPosition += new Vector3((k == 0 ? -1f : 1f) * width * 0.12f, -k * 0.02f * Size, (k - 0.5f) * 0.04f * Size);
                    }
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

        // The model's vertices in its own space times its own scale (for our own meshes, which can be read; the game's
        // can't while it runs). Null when unreadable.
        private static Vector3[] Points(string prefab, GameObject copy)
        {
            if (modelPoints.TryGetValue(prefab, out var cached)) return cached;
            var list = new List<Vector3>();
            var scale = copy.transform.localScale;
            var toCopy = copy.transform.worldToLocalMatrix;
            foreach (var filter in copy.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!IsShown(filter.transform, copy.transform)) continue;
                var mesh = filter.sharedMesh;
                if (!mesh || !mesh.isReadable) { list = null; break; }
                var matrix = toCopy * filter.transform.localToWorldMatrix;
                foreach (var vertex in mesh.vertices) list.Add(Vector3.Scale(matrix.MultiplyPoint3x4(vertex), scale));
            }
            var points = list != null && list.Count > 0 ? list.ToArray() : null;
            modelPoints[prefab] = points;
            return points;
        }

        private static bool IsShown(Transform part, Transform root)
        {
            for (; part && part != root; part = part.parent)
                if (!part.gameObject.activeSelf) return false;
            return true;
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
