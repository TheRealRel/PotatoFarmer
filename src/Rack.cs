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
        // The nine hooks in the model file, three on each crossbar (the crossbars run along x). Each hook rings its bar
        // and ends in two curls, one bigger than the other, pointing across the bar: the meat hangs from the bigger
        // curl, the honeycomb from the smaller. Each curl's lowest inside point, measured from rack.mesh's hook metal
        // (front row then middle then back, each left to right).
        private static readonly Vector3[] BigCurl =
        {
            new Vector3(-0.304f, 0.828f, -0.486f), new Vector3(0.110f, 0.838f, -0.509f), new Vector3(0.441f, 0.827f, -0.495f),
            new Vector3(-0.307f, 0.833f, -0.036f), new Vector3(0.085f, 0.821f, 0.017f), new Vector3(0.458f, 0.830f, 0.020f),
            new Vector3(-0.296f, 0.824f, 0.494f), new Vector3(0.096f, 0.825f, 0.493f), new Vector3(0.440f, 0.826f, 0.495f),
        };
        private static readonly Vector3[] SmallCurl =
        {
            new Vector3(-0.315f, 0.832f, -0.433f), new Vector3(0.098f, 0.830f, -0.440f), new Vector3(0.443f, 0.831f, -0.440f),
            new Vector3(-0.300f, 0.836f, 0.027f), new Vector3(0.101f, 0.827f, -0.031f), new Vector3(0.459f, 0.838f, -0.041f),
            new Vector3(-0.317f, 0.837f, 0.426f), new Vector3(0.098f, 0.831f, 0.440f), new Vector3(0.438f, 0.835f, 0.434f),
        };
        // Where the jerkies go through each hook (test c of 1.4.1, the user: "Some may barely be touching the hook").
        // The curl points above are the underside of the hook's solid foot, about 4 cm (model) below the floor of
        // the U between the stem and each horn, so a jerky's top 3.5 cm above them stayed inside the metal and the
        // jerky looked stuck to the hook's underside. These are the U floors, measured in slices through each curl
        // (tools/hang/valley2.py): the middle of the floor's lowest stretch, at the curl's x. A jerky's top goes
        // JerkyLift above its floor, so the hook visibly goes through it and the top shows inside the U.
        private static readonly Vector3[] BigFloor =
        {
            new Vector3(-0.3038f, 0.8704f, -0.4888f), new Vector3(0.1103f, 0.8708f, -0.4929f), new Vector3(0.4407f, 0.8706f, -0.4950f),
            new Vector3(-0.3068f, 0.8689f, -0.0437f), new Vector3(0.0851f, 0.8651f, 0.0427f), new Vector3(0.4580f, 0.8745f, 0.0459f),
            new Vector3(-0.2965f, 0.8664f, 0.4906f), new Vector3(0.0955f, 0.8698f, 0.4978f), new Vector3(0.4396f, 0.8704f, 0.4954f),
        };
        private static readonly Vector3[] SmallFloor =
        {
            new Vector3(-0.3147f, 0.8813f, -0.4148f), new Vector3(0.0975f, 0.8812f, -0.4209f), new Vector3(0.4429f, 0.8810f, -0.4150f),
            new Vector3(-0.3004f, 0.8789f, 0.0313f), new Vector3(0.1013f, 0.8670f, -0.0383f), new Vector3(0.4585f, 0.8741f, -0.0471f),
            new Vector3(-0.3166f, 0.8812f, 0.4146f), new Vector3(0.0983f, 0.8828f, 0.4218f), new Vector3(0.4379f, 0.8751f, 0.4174f),
        };
        private const float JerkyLift = 0.03f;
        // Pierce: an item's top sits this far above its curl's lowest point (model units), so the curl goes into it
        // rather than the item touching it at one point. Along the bar a hanging item may overlap its neighbours a
        // little (5 %); across it stays in its row. The honeycomb hangs 30 cm. Jerkies are drawn 12 % bigger.
        private const float HookY = 0.825f, Pierce = 0.035f, SpaceAlong = 0.41f, SpaceAcross = 0.47f, Overlap = 1.05f;
        private const float HoneyHeight = 0.30f, JerkyScale = 1.12f;
        private static readonly Dictionary<string, Vector3[]> modelPoints = new Dictionary<string, Vector3[]>();
        // Where the honeycomb and the jerkies hang from: the middle of their highest 3 cm, in their own space times their
        // own scale, for the game's spit way [0] and longest side down [1], so the curl goes into the item's real top
        // instead of the top of its box. Measured offline (the game's meshes can't be read while it runs); our own
        // jerky meshes are measured at run time.
        private const float TopBand = 0.03f;
        private static readonly Dictionary<string, Vector3[]> Anchors = new Dictionary<string, Vector3[]>
        {
            { "Honey", new[] { new Vector3(0.065f, -0.005f, 0.294f), new Vector3(0.077f, 0.020f, -0.300f) } },
            { "BoarJerky", new[] { new Vector3(-0.049f, 0.035f, 0.197f), new Vector3(-0.049f, 0.035f, 0.197f) } },
            { "WolfJerky", new[] { new Vector3(-0.053f, 0.041f, 0.268f), new Vector3(0.029f, 0.009f, -0.244f) } },
            { "BearJerky_TW", new[] { new Vector3(-0.049f, 0.035f, 0.197f), new Vector3(-0.049f, 0.035f, 0.197f) } },
            { "FoxJerky_TW", new[] { new Vector3(0.007f, 0.013f, 0.115f), new Vector3(0.007f, 0.013f, 0.115f) } },
        };

        // How each known raw meat hangs: its turn, its size, and the point in it (own space times own scale) that goes
        // on the bigger curl's lowest point. Worked out offline from each meat's mesh (tools/hang/hangplan3.py,
        // anchor2.py, export_hangs.py) because the game's meshes can't be read while it runs. Tried for every meat:
        // the game's spit way, the same flipped over, and longest side down, each also a quarter turn about the
        // vertical; sized to its share of the rack. For each way the hook point is the highest spot where, 6 cm below
        // the meat's top, the meat is solid there and 3 cm all round it, so the curl sits inside the meat itself and
        // not in a bone, a thin rim or a prong. Picked, in order: nothing (a bone, a fin) rising more than 2 cm above
        // that spot; the meat's weight within 4 cm of straight under the hook, as a hung piece would settle; the
        // game's way; the biggest. Then each meat is raised (test f, after the user's screenshots: the meat still read
        // as hanging under the hook) until the curl's lower two thirds, up to 8 cm above its bottom, is hidden inside or
        // behind the meat seen from eye level in front of the row and from low at the rack's end, on all nine hooks
        // (90 %; narrow pieces thinner than the 16 cm curl get the height that hides the most). Only the curl's tips,
        // the shank and the ring show above it. Checked with renders from the user's own camera angles.
        // Other mods' meats not listed here hang the older way (Hanging.Place, centred under the curl).
        private struct Hang
        {
            public readonly Quaternion Rotation;
            public readonly float Scale;
            public readonly Vector3 Point;
            public Hang(Quaternion rotation, float scale, Vector3 point) { Rotation = rotation; Scale = scale; Point = point; }
        }

        private static readonly Dictionary<string, Hang> Hangs = new Dictionary<string, Hang>
        {
            { "RawMeat", new Hang(new Quaternion(0.5f, -0.5f, 0.5f, 0.5f), 1.000f, new Vector3(0.0203f, 0.0023f, -0.2379f)) },   // flip0
            { "WolfMeat", new Hang(new Quaternion(0.7071068f, 0f, 0f, 0.7071068f), 1.000f, new Vector3(0.1235f, 0.0397f, -0.2912f)) },   // flip90
            { "BjornMeat", new Hang(new Quaternion(0.7071068f, 0f, 0f, 0.7071068f), 1.000f, new Vector3(-0.0074f, -0.0447f, -0.2148f)) },   // flip90
            { "FoxMeat_TW", new Hang(new Quaternion(0.5f, -0.5f, 0.5f, 0.5f), 1.000f, new Vector3(0.0112f, 0.0121f, -0.1899f)) },   // flip0
            { "DeerMeat", new Hang(new Quaternion(0f, 0.7071068f, 0.7071068f, 0f), 1.000f, new Vector3(0.0005f, 0.0153f, -0.0234f)) },   // game90
            { "rae_HorseMeat", new Hang(new Quaternion(0f, 0.7071068f, 0.7071068f, 0f), 1.000f, new Vector3(0.0005f, 0.0153f, -0.0234f)) },   // game90
            { "AsksvinMeat", new Hang(new Quaternion(-0.5f, 0.5f, 0.5f, 0.5f), 1.000f, new Vector3(-0.0016f, 0.0963f, 0.1535f)) },   // game0
            { "HareMeat", new Hang(new Quaternion(-0.5f, 0.5f, -0.5f, 0.5f), 1.000f, new Vector3(-0.4668f, 0.0160f, -0.1023f)) },   // long90
            { "SharkMeat_TW", new Hang(new Quaternion(-0.5f, 0.5f, 0.5f, 0.5f), 0.723f, new Vector3(0.1043f, -0.0016f, 0.0170f)) },   // game0
            { "ChickenMeat", new Hang(new Quaternion(0.7071068f, 0f, 0f, 0.7071068f), 1.000f, new Vector3(-0.0138f, 0.0162f, -0.0349f)) },   // flip90
            { "VoltureMeat", new Hang(new Quaternion(0f, 0.7071068f, 0.7071068f, 0f), 1.000f, new Vector3(-0.1024f, 0.1349f, 0.2350f)) },   // game90
            { "NeckTail", new Hang(new Quaternion(0.7071068f, 0f, 0f, 0.7071068f), 1.000f, new Vector3(0.0029f, 0.0270f, -0.2797f)) },   // flip90
            { "LoxMeat", new Hang(new Quaternion(0.7071068f, 0f, 0f, 0.7071068f), 1.000f, new Vector3(0.0121f, 0.0238f, -0.4296f)) },   // flip90
            { "BearSteak_TW", new Hang(new Quaternion(0f, 0.7071068f, 0.7071068f, 0f), 1.000f, new Vector3(-0.0331f, 0.0317f, 0.1135f)) },   // game90
            { "MooseMeat", new Hang(new Quaternion(-0.5f, -0.5f, 0.5f, 0.5f), 0.608f, new Vector3(1.4427f, -0.0275f, -0.0892f)) },   // long90
            { "BugMeat", new Hang(new Quaternion(-0.5f, -0.5f, 0.5f, 0.5f), 1.000f, new Vector3(0.5916f, 0.1597f, -0.0943f)) },   // long90
            { "SerpentMeat", new Hang(new Quaternion(0.5f, -0.5f, 0.5f, 0.5f), 0.751f, new Vector3(-0.0055f, 0.0228f, -0.4764f)) },   // flip0
            { "BoneMawSerpentMeat", new Hang(new Quaternion(0.5f, -0.5f, 0.5f, 0.5f), 0.981f, new Vector3(0.0025f, -0.0279f, -0.5543f)) },   // flip0
        };

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
        private static readonly KeyValuePair<string, string>[] GamePairs =
        {
            new KeyValuePair<string, string>("RawMeat", "BoarJerky"), new KeyValuePair<string, string>("WolfMeat", "WolfJerky"),
        };

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
            // The game's own pair, when no recipe above gave it: a mod that changes or removes the cauldron's Boar or Wolf
            // Jerky recipe (more meat or honey, another ingredient, switched off) left the rack refusing boar and wolf
            // meat while our own jerkies worked (GitHub report, 2026-11-07). The rack keeps the game's 1 + 1 -> 2.
            foreach (var pair in GamePairs)
                if (!jerkyFor.ContainsKey(pair.Key) && db.GetItemPrefab(pair.Key) && db.GetItemPrefab(pair.Value))
                    jerkyFor[pair.Key] = new KeyValuePair<string, int>(pair.Value, 2);
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

        private static Vector3 Curl(Vector3[] curls, int hook) => (curls[hook] + new Vector3(0f, Pierce, 0f)) * Size;

        private static Vector3 Floor(Vector3[] floors, int hook) => (floors[hook] + new Vector3(0f, JerkyLift, 0f)) * Size;

        private Vector3 HookLocal(int hook) => Curl(BigCurl, hook);

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
            Vector3 big = Curl(BigCurl, index), small = Curl(SmallCurl, index);
            float along = SpaceAlong * Size * Overlap, across = SpaceAcross * Size, down = (HookY + Pierce) * Size - 0.05f;
            if (!Ready(hook))
            {
                // The meat on the bigger curl, the honeycomb on the smaller one.
                var honey = Copy("Honey", holder.transform, out var honeyBounds);
                if (honey && HoneyRest.Poses.TryGetValue(hook.Meat, out var poses) && poses.Length == Hooks)
                {
                    // Where it comes to rest against this meat on this hook (see HoneyRest).
                    honey.transform.localRotation = poses[index].Rotation;
                    honey.transform.localScale = honey.transform.localScale * poses[index].Scale;
                    honey.transform.localPosition = poses[index].Position;
                }
                else if (honey)
                {
                    var honeyFit = Anchor("Honey", honey, Hanging.Place(honeyBounds, small, along, across, down, true, Hanging.SignFor("Honey")), small);
                    Apply(honey, honeyFit, HoneyHeight / Mathf.Max(honeyFit.High.y - honeyFit.Low.y, 0.001f), small);
                }
                var meat = Copy(hook.Meat, holder.transform, out var meatBounds);
                if (meat && Hangs.TryGetValue(hook.Meat, out var hang))
                {
                    // Its hang point on the curl's lowest point (the curl then sits 6 cm inside the meat).
                    meat.transform.localRotation = hang.Rotation;
                    meat.transform.localScale = meat.transform.localScale * hang.Scale;
                    meat.transform.localPosition = BigCurl[index] * Size - hang.Scale * (hang.Rotation * hang.Point);
                }
                else if (meat) Apply(meat, Anchor(hook.Meat, meat, Hanging.Place(meatBounds, big, along, across, down, true, Hanging.SignFor(hook.Meat)), big), 1f, big);
            }
            else if (JerkyFor().TryGetValue(hook.Meat, out var jerky))
            {
                // One jerky on each curl. Our crossed-strip jerkies are turned in their own face so the strips read as
                // an X, hung by the top corner.
                bool turned = Jerky.Kinds.Any(k => k.Prefab == jerky.Key && k.Shape == "deer");
                for (int k = 0; k < 2; k++)
                {
                    var at = Floor(k == 0 ? BigFloor : SmallFloor, index);
                    var copy = Copy(jerky.Key, holder.transform, out var bounds);
                    if (!copy) break;
                    var fit = Anchor(jerky.Key, copy, Hanging.Place(bounds, at, along / 2f, across, down, false, Hanging.SignFor(jerky.Key)), at);
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
                        copy.transform.localPosition = start + at - top + new Vector3((k - 0.5f) * 0.04f * Size, 0f, 0f);
                    }
                    else
                    {
                        Apply(copy, fit, JerkyScale, at);
                        copy.transform.localPosition += new Vector3((k - 0.5f) * 2f * width * 0.12f, 0f, 0f);
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

        // Moves a fit so the item's real top (see Anchors) sits on the hook point (honey, jerkies, other mods' meats).
        private static Hanging.Fit Anchor(string prefab, GameObject copy, Hanging.Fit fit, Vector3 at)
        {
            Vector3 top;
            if (Anchors.TryGetValue(prefab, out var anchors)) top = fit.Rotation * anchors[fit.Game ? 0 : 1];
            else if (Points(prefab, copy) is Vector3[] points)
            {
                float highest = float.NegativeInfinity;
                foreach (var point in points) highest = Mathf.Max(highest, (fit.Rotation * point).y);
                var sum = Vector3.zero;
                int count = 0;
                foreach (var point in points)
                {
                    var turned = fit.Rotation * point;
                    if (turned.y < highest - TopBand) continue;
                    sum += turned;
                    count++;
                }
                top = sum / Mathf.Max(count, 1);
                top.y = highest;
            }
            else return fit;
            var position = at - fit.Scale * top;
            var shift = position - fit.Position;
            fit.Position = position;
            fit.Low += shift;
            fit.High += shift;
            return fit;
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
            Looks.Restore(source.gameObject, copy);
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
