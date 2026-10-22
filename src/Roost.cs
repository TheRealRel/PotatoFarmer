// Gullinkambi's Roost: a nesting box that gathers the eggs hens lay nearby, so they keep laying.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PotatoFarmer
{
    // Hens stop laying once there are 10 hens and eggs within 10 m, so eggs left lying around shut a coop down. The
    // roost gathers cold eggs (any egg that hatches, modded ones too) into itself, keeping their star level, and with a
    // fire nearby hatches them one at a time to keep the flock at the server's flock size. Eggs already warming by a
    // fire are left to hatch, and E switches gathering off for when you want eggs to lie.
    public class Roost : MonoBehaviour, Hoverable, Interactable
    {
        private const string EggsKey = "PotatoRoost.Eggs", OffKey = "PotatoRoost.Off", WarmKey = "PotatoRoost.Warming";
        private const float GatherEvery = 3f;
        // The model file is 1.9 m long; drawn 1.3 times that (about 2.5 m long, 1.1 m tall) so it stands over a hen, which
        // also lands its 256 px textures at the Potato look's ~50 px/m. Measurements below are in the model's space.
        internal const float Size = 1.3f;
        // The three cubbies: centre x of each, the straw floor height and the middle of their depth.
        private static readonly float[] CubbyX = { -0.466f, -0.017f, 0.447f };
        private const float CubbyFloor = 0.25f, CubbyZ = 0.05f, MaxShownPerCubby = 4;
        // Where each egg of a cubby's clutch sits, in egg widths: two side by side, then one in front and one behind.
        // How long an egg looks in the cubbies (metres), and whether its model's real size was logged yet.
        private const float EggLength = 0.12f;
        private static bool eggSizeLogged;
        private static readonly Vector3[] Clutch = { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0f, 0.85f), new Vector3(0f, 0f, -0.85f) };
        private static int itemMask;

        private ZNetView view;
        private string shown;
        private GameObject eggModels;
        private float nextCheck, nextGather;

        private bool Valid => view && view.IsValid();
        private bool Gathering => Valid && !view.GetZDO().GetBool(OffKey);

        private void Awake()
        {
            view = GetComponent<ZNetView>();
            if (!view || view.GetZDO() == null) return;
            view.Register("PotatoRoost_Toggle", RPC_Toggle);
            view.Register("PotatoRoost_Take", RPC_Take);
            view.Register<string>("PotatoRoost_Give", RPC_Give);
            var wear = GetComponent<WearNTear>();
            if (wear) wear.m_onDestroyed += OnDestroyed;
            nextGather = Time.time + UnityEngine.Random.Range(0f, GatherEvery);
        }

        private Stock Read() => Stock.Parse(Valid ? view.GetZDO().GetString(EggsKey, "") : "");

        private void Write(Stock stock) => view.GetZDO().Set(EggsKey, stock.Format());

        private void Update()
        {
            if (!Valid) return;
            if (Time.time >= nextGather)
            {
                nextGather = Time.time + GatherEvery;
                if (view.IsOwner())
                {
                    if (Gathering) Gather();
                    Hatch();
                }
            }
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.5f;
            string now = view.GetZDO().GetString(EggsKey, "");
            if (now == shown) return;
            shown = now;
            ShowEggs(Stock.Parse(now));
        }

        // ------------------------------------------------------------------ gathering (the roost's owner)

        private void Gather()
        {
            var stock = Read();
            int room = Plugin.RoostCapacity.Value - stock.Total;
            if (room <= 0) return;
            if (itemMask == 0) itemMask = LayerMask.GetMask("item");
            bool changed = false;
            var seen = new HashSet<ItemDrop>();
            foreach (var collider in Physics.OverlapSphere(transform.position, Plugin.RoostReach.Value, itemMask))
            {
                var egg = collider.attachedRigidbody ? collider.attachedRigidbody.GetComponent<ItemDrop>() : null;
                if (!egg || !seen.Add(egg) || !egg.GetComponent<EggGrow>()) continue;
                var eggView = egg.GetComponent<ZNetView>();
                if (!eggView || !eggView.IsValid()) continue;
                // Already warming by a fire: someone wants it to hatch.
                if (eggView.GetZDO().GetFloat(ZDOVars.s_growStart) > 0f) continue;
                if (!eggView.IsOwner())
                {
                    egg.RequestOwn();
                    continue;
                }
                int stack = egg.m_itemData.m_stack, count = Math.Min(stack, room);
                stock.Add(Key(egg.gameObject, egg.m_itemData.m_quality), count);
                room -= count;
                changed = true;
                if (count >= stack) eggView.Destroy();
                else egg.SetStack(stack - count);
                if (room <= 0) break;
            }
            if (changed) Write(stock);
        }

        // Stored as "Prefab@stars", so star-level eggs stay star-level.
        private static string Key(GameObject prefab, int quality) => Utils.GetPrefabName(prefab) + "@" + Math.Max(1, quality);

        private static bool Split(string key, out string prefab, out int quality)
        {
            int at = key.LastIndexOf('@');
            prefab = at > 0 ? key.Substring(0, at) : key;
            quality = at > 0 && int.TryParse(key.Substring(at + 1), out int q) ? Math.Max(1, q) : 1;
            return prefab.Length > 0;
        }

        // ------------------------------------------------------------------ hatching (the roost's owner)

        // The lid is the roof: with a fire near the roost, it warms eggs and hatches each after the egg's usual time
        // (30 minutes for a chicken egg), while the flock around it is smaller than the flock size. Eggs are lined up
        // to hatch a few minutes apart, as many as the flock is short, so a full roost hatches a chick every few minutes rather
        // than one per half hour.
        private const float Stagger = 300f;

        private void Hatch()
        {
            var zdo = view.GetZDO();
            var stock = Read();
            var egg = NextEgg(stock, out string key, out int quality);
            bool heat = egg && Plugin.RoostFlock.Value > 0 && EffectArea.IsPointInsideArea(transform.position + Vector3.up * 0.4f, EffectArea.Type.Heat, 0.5f);
            int slots = heat ? Mathf.Min(Plugin.RoostFlock.Value - Flock(egg), Hatchable(stock)) : 0;
            var starts = Starts();
            int before = starts.Count;
            if (slots <= 0)
            {
                if (before > 0) zdo.Set(WarmKey, "");
                return;
            }
            float now = (float)ZNet.instance.GetTimeSeconds();
            // The oldest egg is done: hatch it (one per check; the new chick counts towards the flock on the next).
            if (before > 0 && now - starts[0] >= egg.m_growTime)
            {
                starts.RemoveAt(0);
                zdo.Set(WarmKey, Join(starts));
                if (!stock.Remove(key, 1)) return;
                Write(stock);
                var at = transform.TransformPoint(new Vector3(0f, 0f, 0.9f) * Size);
                var chick = Instantiate(egg.m_grownPrefab, at, transform.rotation).GetComponent<Character>();
                egg.m_hatchEffect.Create(at, transform.rotation);
                if (chick)
                {
                    chick.SetTamed(egg.m_tamed);
                    chick.SetLevel(quality);
                }
                return;
            }
            // Fewer eggs needed now (the flock grew or eggs were taken): stop warming the newest.
            while (starts.Count > slots) starts.RemoveAt(starts.Count - 1);
            // Line up the rest 5 minutes apart straight away, so the hover shows the real wait and skiptime keeps the gap.
            while (starts.Count < slots) starts.Add(starts.Count == 0 ? now : Mathf.Max(now, starts[starts.Count - 1] + Stagger));
            if (starts.Count != before) zdo.Set(WarmKey, Join(starts));
        }

        // When each warming egg started, oldest first.
        private List<float> Starts()
        {
            var list = new List<float>();
            foreach (string part in view.GetZDO().GetString(WarmKey).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (float.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float t)) list.Add(t);
            return list;
        }

        private static string Join(List<float> starts) =>
            string.Join(";", starts.Select(t => t.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

        // How many stored eggs hatch into something.
        private static int Hatchable(Stock stock)
        {
            int count = 0;
            foreach (var entry in stock.Entries)
            {
                if (!Split(entry.Key, out string name, out _)) continue;
                var prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(name) : null;
                var grow = prefab ? prefab.GetComponent<EggGrow>() : null;
                if (grow && grow.m_grownPrefab) count += entry.Value;
            }
            return count;
        }

        // The first stored egg that hatches into something.
        private static EggGrow NextEgg(Stock stock, out string key, out int quality)
        {
            key = null;
            quality = 1;
            foreach (var entry in stock.Entries)
            {
                if (!Split(entry.Key, out string name, out int q)) continue;
                var prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(name) : null;
                var grow = prefab ? prefab.GetComponent<EggGrow>() : null;
                if (!grow || !grow.m_grownPrefab) continue;
                key = entry.Key;
                quality = q;
                return grow;
            }
            return null;
        }

        // Animals within reach that hatch from this egg: the chicks and what they grow into.
        private int Flock(EggGrow egg)
        {
            string young = egg.m_grownPrefab.name;
            var grows = egg.m_grownPrefab.GetComponent<Growup>();
            string adult = grows && grows.m_grownPrefab ? grows.m_grownPrefab.name : null;
            float reach = Plugin.RoostReach.Value;
            int count = 0;
            foreach (var character in Character.GetAllCharacters())
            {
                if (!character || character.IsDead() || Vector3.Distance(character.transform.position, transform.position) > reach) continue;
                string name = Utils.GetPrefabName(character.gameObject);
                if (name == young || name == adult) count++;
            }
            return count;
        }

        private string Warming()
        {
            var starts = Starts();
            if (starts.Count == 0) return null;
            var egg = NextEgg(Read(), out _, out _);
            if (!egg) return null;
            float left = Mathf.Max(0f, egg.m_growTime - (float)(ZNet.instance.GetTimeSeconds() - starts[0]));
            string eggs = starts.Count == 1 ? "an egg" : starts.Count + " eggs";
            return $"Warming {eggs}: next chick in {Mathf.CeilToInt(left / 60f)} min";
        }

        // ------------------------------------------------------------------ hover and use

        public float GetHoverOffset() => 0f;

        public string GetHoverName() => Localization.instance.Localize(Plugin.RoostName);

        public string GetHoverText()
        {
            if (!Valid) return "";
            if (!PrivateArea.CheckAccess(transform.position, 0f, false)) return Localization.instance.Localize(Plugin.RoostName + "\n$piece_noaccess");
            var stock = Read();
            var lines = new List<string>();
            foreach (var entry in stock.Entries)
            {
                if (!Split(entry.Key, out string prefab, out int quality)) continue;
                lines.Add(entry.Value + " " + ItemName(prefab) + (quality > 1 ? " " + new string('★', quality - 1) : ""));
            }
            string contents = lines.Count == 0 ? "No eggs" : string.Join(", ", lines.ToArray());
            string warming = Warming();
            if (warming != null) contents += "\n" + warming;
            string text = Plugin.RoostName + $" ({stock.Total}/{Plugin.RoostCapacity.Value})\n" + contents +
                $"\n[<color=yellow><b>$KEY_Use</b></color>] Gather eggs: {(Gathering ? "On" : "Off")}";
            if (stock.Total > 0) text += "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Take the eggs";
            return Localization.instance.Localize(text);
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !Valid) return false;
            if (!PrivateArea.CheckAccess(transform.position, 0f, true)) return true;
            if (alt)
            {
                if (Read().Total > 0) view.InvokeRPC("PotatoRoost_Take");
            }
            else
            {
                bool was = Gathering;
                view.InvokeRPC("PotatoRoost_Toggle");
                user.Message(MessageHud.MessageType.Center, was ? "The roost stops gathering eggs" : "The roost gathers eggs again");
            }
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        private static string ItemName(string prefab)
        {
            var item = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            var drop = item ? item.GetComponent<ItemDrop>() : null;
            return drop ? drop.m_itemData.m_shared.m_name : prefab;
        }

        // ------------------------------------------------------------------ changes, made by the roost's owner

        private void RPC_Toggle(long sender)
        {
            if (Valid && view.IsOwner()) view.GetZDO().Set(OffKey, Gathering);
        }

        private void RPC_Take(long sender)
        {
            if (!Valid || !view.IsOwner()) return;
            var stock = Read();
            if (stock.Total == 0) return;
            Write(new Stock());
            view.InvokeRPC(sender, "PotatoRoost_Give", stock.Format());
        }

        // On the player who asked: eggs go into their inventory, and what doesn't fit lands at their feet.
        private void RPC_Give(long sender, string packed)
        {
            var player = Player.m_localPlayer;
            var at = player ? player.transform.position + Vector3.up : transform.position + Vector3.up;
            foreach (var entry in Stock.Parse(packed).Entries)
                Hand(entry.Key, entry.Value, player ? player.GetInventory() : null, at);
            if (player) player.Message(MessageHud.MessageType.Center, "You take the eggs");
        }

        private static void Hand(string key, int count, Inventory inventory, Vector3 at)
        {
            if (!Split(key, out string name, out int quality)) return;
            var prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(name) : null;
            var drop = prefab ? prefab.GetComponent<ItemDrop>() : null;
            if (!drop) return;
            int left = count, max = Math.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
            while (left > 0)
            {
                int n = Math.Min(left, max);
                left -= n;
                var item = drop.m_itemData.Clone();
                item.m_dropPrefab = prefab;
                item.m_stack = n;
                item.m_quality = quality;
                if (inventory != null && inventory.CanAddItem(item, n) && inventory.AddItem(item)) continue;
                ItemDrop.DropItem(item, n, at + UnityEngine.Random.insideUnitSphere * 0.3f, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            }
        }

        // Broken or taken down: the eggs spill out.
        private void OnDestroyed()
        {
            if (!Valid || !view.IsOwner()) return;
            var stock = Read();
            Write(new Stock());
            foreach (var entry in stock.Entries) Hand(entry.Key, entry.Value, null, transform.position + Vector3.up * 0.5f);
        }

        // ------------------------------------------------------------------ eggs in the cubbies (looks only)

        private void ShowEggs(Stock stock)
        {
            if (eggModels) Destroy(eggModels);
            eggModels = null;
            if (stock.Total == 0 || Jotunn.Managers.GUIManager.IsHeadless() || !ObjectDB.instance) return;
            eggModels = new GameObject("Eggs");
            eggModels.SetActive(false);
            eggModels.transform.SetParent(transform, false);
            var id = view.GetZDO().m_uid;
            float clutchTurn = 0f;
            var random = new System.Random(unchecked((int)id.ID * 31 + id.UserID.GetHashCode()));
            // One egg shown per egg stored, up to four in each cubby, filling them left to right.
            var keys = stock.Entries.SelectMany(entry => Enumerable.Repeat(entry.Key, entry.Value)).Take(CubbyX.Length * (int)MaxShownPerCubby).ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                if (!Split(keys[i], out string name, out _)) continue;
                var prefab = ObjectDB.instance.GetItemPrefab(name);
                if (!prefab) continue;
                var copy = Instantiate(prefab, eggModels.transform, false);
                Trough.Strip(copy);
                int cubby = i / (int)MaxShownPerCubby, slot = i % (int)MaxShownPerCubby;
                // Lying on its side, a little turned, resting on the straw.
                var rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * Quaternion.Euler(90f, 0f, 0f);
                copy.transform.localRotation = rotation;
                float bottom = 0f, across = EggLength * 0.8f;
                if (Trough.LocalBounds(copy, out var bounds))
                {
                    // Shown at a set size: the egg model's own scale varies (other mods change it), and an egg the
                    // height of the cubby looked wrong.
                    var size = Vector3.Scale(bounds.size, copy.transform.localScale);
                    float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z)), shortest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
                    if (longest > 0.001f)
                    {
                        float fit = EggLength / longest;
                        copy.transform.localScale *= fit;
                        across = (longest + shortest) / 2f * fit;
                        if (!eggSizeLogged)
                        {
                            eggSizeLogged = true;
                            Plugin.Log.LogInfo($"Roost: {name} model is {longest:0.###} m long, shown at {EggLength} m.");
                        }
                    }
                    var scale = copy.transform.localScale;
                    bottom = Enumerable.Range(0, 8).Select(k => (rotation * Vector3.Scale(new Vector3(
                        (k & 1) == 0 ? bounds.min.x : bounds.max.x, (k & 2) == 0 ? bounds.min.y : bounds.max.y, (k & 4) == 0 ? bounds.min.z : bounds.max.z), scale)).y).Min();
                }
                // A clutch: the eggs in each cubby huddle together, touching, the cluster turned its own way.
                if (slot == 0) clutchTurn = (float)random.NextDouble() * 360f;
                var offset = Quaternion.Euler(0f, clutchTurn, 0f) * (Clutch[slot] * across * 0.9f);
                float jitter = across * 0.08f;
                float x = CubbyX[cubby] * Size + offset.x + (float)(random.NextDouble() - 0.5) * jitter;
                float z = CubbyZ * Size + offset.z + (float)(random.NextDouble() - 0.5) * jitter;
                copy.transform.localPosition = new Vector3(x, CubbyFloor * Size - bottom, z);
            }
            eggModels.SetActive(true);
        }
    }
}
