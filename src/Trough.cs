// The trough itself: holds food like a smelter holds ore, hands out one bite at a time to hungry animals.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PotatoFarmer
{
    public class Trough : MonoBehaviour, Hoverable, Interactable
    {
        private const string FoodKey = "PotatoTrough.Food";
        private const int MaxBaitInBowl = 4, MaxFoodModels = 12;
        private const float DispenseDelay = 3f;
        // How big the trough is built, relative to the model file (2 m long). Every measurement below is for the model
        // file and is multiplied by this, so the model, its collision and the bowl always line up.
        internal const float Size = 1.1f;
        // The bowl floor, and the box above it where bites and food models sit (trough space).
        internal static readonly Vector3 BowlFloor = new Vector3(0f, 0.215f, 0f) * Size;
        private static readonly Vector3 BowlCenter = new Vector3(0f, 0.3f, 0f) * Size, BowlHalfSize = new Vector3(0.75f, 0.15f, 0.22f) * Size;
        private static int itemMask;

        internal static readonly List<Trough> All = new List<Trough>();

        private ZNetView view;
        private string shown;
        private GameObject foodModels;
        private float nextCheck, lastDispense = -100f;

        private bool Valid => view && view.IsValid();

        private void Awake()
        {
            view = GetComponent<ZNetView>();
            // The build ghost has no network data; it only needs to look like a trough.
            if (!view || view.GetZDO() == null) return;
            All.Add(this);
            view.Register<string, int>("PotatoTrough_Add", RPC_Add);
            view.Register("PotatoTrough_Take", RPC_Take);
            view.Register<string>("PotatoTrough_Give", RPC_Give);
            view.Register<string>("PotatoTrough_Feed", RPC_Feed);
            var wear = GetComponent<WearNTear>();
            if (wear) wear.m_onDestroyed += OnDestroyed;
        }

        private void OnDestroy() => All.Remove(this);

        internal Stock Read() => Stock.Parse(Valid ? view.GetZDO().GetString(FoodKey, "") : "");

        private void Write(Stock stock) => view.GetZDO().Set(FoodKey, stock.Format());

        private void Update()
        {
            if (Time.time < nextCheck || !Valid) return;
            nextCheck = Time.time + 0.5f;
            string now = Plugin.TroughShowFood.Value ? view.GetZDO().GetString(FoodKey, "") : "";
            if (now == shown) return;
            shown = now;
            ShowFood(Stock.Parse(now));
        }

        // ------------------------------------------------------------------ hover and use

        public float GetHoverOffset() => 0f;

        public string GetHoverName() => Localization.instance.Localize(Plugin.TroughName);

        public string GetHoverText()
        {
            if (!Valid) return "";
            if (!PrivateArea.CheckAccess(transform.position, 0f, false)) return Localization.instance.Localize(Plugin.TroughName + "\n$piece_noaccess");
            var stock = Read();
            string contents = stock.Total == 0 ? "Empty" : string.Join(", ", stock.Entries.Select(entry => entry.Value + " " + ItemName(entry.Key)).ToArray());
            string text = Plugin.TroughName + $" ({stock.Total}/{Plugin.TroughCapacity.Value})\n" + contents +
                "\n[<color=yellow><b>1-8</b></color>] Add food from your hotbar";
            if (stock.Total > 0) text += "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Take the food back";
            return Localization.instance.Localize(text);
        }

        // Food goes in from the hotbar (UseItem); Use with the alternate key takes it all back.
        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !alt || !Valid) return false;
            if (!PrivateArea.CheckAccess(transform.position, 0f, true)) return true;
            if (Read().Total > 0) view.InvokeRPC("PotatoTrough_Take");
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            if (!Valid || item == null) return false;
            if (!PrivateArea.CheckAccess(transform.position, 0f, true)) return true;
            if (!item.m_dropPrefab || !Feeding.IsAnimalFood(item))
            {
                user.Message(MessageHud.MessageType.Center, "No animal eats that");
                return true;
            }
            return Add(user, item);
        }

        private bool Add(Humanoid user, ItemDrop.ItemData item)
        {
            int room = Plugin.TroughCapacity.Value - Read().Total;
            if (room <= 0)
            {
                user.Message(MessageHud.MessageType.Center, "The trough is full");
                return true;
            }
            int count = Math.Min(room, item.m_stack);
            string prefab = item.m_dropPrefab.name, name = item.m_shared.m_name;
            if (!user.GetInventory().RemoveItem(item, count)) return true;
            view.InvokeRPC("PotatoTrough_Add", prefab, count);
            user.Message(MessageHud.MessageType.Center, $"$msg_added {name} x{count}");
            return true;
        }

        private static string ItemName(string prefab)
        {
            var item = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            var drop = item ? item.GetComponent<ItemDrop>() : null;
            return drop ? drop.m_itemData.m_shared.m_name : prefab;
        }

        // ------------------------------------------------------------------ changes, made by the trough's owner

        private void RPC_Add(long sender, string prefab, int count)
        {
            if (!Valid || !view.IsOwner() || count <= 0 || !ObjectDB.instance || !ObjectDB.instance.GetItemPrefab(prefab)) return;
            var stock = Read();
            stock.Add(prefab, count);
            Write(stock);
        }

        private void RPC_Take(long sender)
        {
            if (!Valid || !view.IsOwner()) return;
            var stock = Read();
            if (stock.Total == 0) return;
            Write(new Stock());
            view.InvokeRPC(sender, "PotatoTrough_Give", stock.Format());
        }

        // On the player who asked: the food goes into their inventory, and what doesn't fit lands at their feet.
        private void RPC_Give(long sender, string packed)
        {
            var player = Player.m_localPlayer;
            var at = player ? player.transform.position + Vector3.up : transform.position + Vector3.up * 0.6f;
            foreach (var entry in Stock.Parse(packed).Entries)
            {
                var prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(entry.Key) : null;
                var drop = prefab ? prefab.GetComponent<ItemDrop>() : null;
                if (!drop) continue;
                int left = entry.Value, max = Math.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
                while (left > 0)
                {
                    int count = Math.Min(left, max);
                    left -= count;
                    var inventory = player ? player.GetInventory() : null;
                    if (inventory != null && inventory.CanAddItem(prefab, count) && inventory.AddItem(prefab, count)) continue;
                    ItemDrop.DropItem(Item(prefab, count), count, at, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                }
            }
            if (player) player.Message(MessageHud.MessageType.Center, "You take the food back");
        }

        private static ItemDrop.ItemData Item(GameObject prefab, int count)
        {
            var item = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = count;
            return item;
        }

        // Broken or taken down: the food spills out, like a chest's contents.
        private void OnDestroyed()
        {
            if (!Valid || !view.IsOwner()) return;
            var stock = Read();
            Write(new Stock());
            foreach (var entry in stock.Entries)
            {
                var prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(entry.Key) : null;
                var drop = prefab ? prefab.GetComponent<ItemDrop>() : null;
                if (!drop) continue;
                int left = entry.Value, max = Math.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
                while (left > 0)
                {
                    int count = Math.Min(left, max);
                    left -= count;
                    var at = transform.position + Vector3.up * 0.5f + UnityEngine.Random.insideUnitSphere * 0.3f;
                    ItemDrop.DropItem(Item(prefab, count), count, at, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
                }
            }
        }

        // ------------------------------------------------------------------ feeding

        internal string FoodFor(MonsterAI animal) => Read().Entries.Select(entry => entry.Key).FirstOrDefault(prefab => Feeding.CanEat(animal, prefab));

        // Called where the hungry animal is simulated. The trough's owner sets one bite in the bowl; if that's this
        // game, the animal can go for it straight away, otherwise it finds it on its next look around.
        internal ItemDrop Dispense(string prefab)
        {
            if (Time.time - lastDispense < DispenseDelay || BaitInBowl() >= MaxBaitInBowl) return null;
            lastDispense = Time.time;
            if (view.IsOwner()) return DropBait(prefab);
            view.InvokeRPC("PotatoTrough_Feed", prefab);
            return null;
        }

        private void RPC_Feed(long sender, string prefab)
        {
            if (Valid && view.IsOwner() && BaitInBowl() < MaxBaitInBowl) DropBait(prefab);
        }

        private ItemDrop DropBait(string prefab)
        {
            var item = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            if (!item || !item.GetComponent<ItemDrop>()) return null;
            var stock = Read();
            if (!stock.Remove(prefab, 1)) return null;
            Write(stock);
            var at = transform.TransformPoint(BowlFloor + new Vector3(UnityEngine.Random.Range(-0.45f, 0.45f) * Size, 0.12f, UnityEngine.Random.Range(-0.08f, 0.08f) * Size));
            var bite = ItemDrop.DropItem(Item(item, 1), 1, at, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            Feeding.MarkBait(bite);
            return bite;
        }

        private int BaitInBowl()
        {
            if (itemMask == 0) itemMask = LayerMask.GetMask("item");
            var found = new HashSet<ItemDrop>();
            foreach (var collider in Physics.OverlapBox(transform.TransformPoint(BowlCenter), BowlHalfSize, transform.rotation, itemMask))
            {
                var drop = collider.attachedRigidbody ? collider.attachedRigidbody.GetComponent<ItemDrop>() : null;
                if (drop && Feeding.IsBait(drop)) found.Add(drop);
            }
            return found.Count;
        }

        // ------------------------------------------------------------------ food in the bowl (looks only)

        // The inside of the bowl, in trough space: food stays within these walls and sits on the floor (the floor's
        // highest point, so nothing sinks in). Pieces may overlap each other, like a heap of food.
        private const float InnerHalfLength = 0.70f * Size, InnerHalfWidth = 0.19f * Size, FloorTop = 0.222f * Size, MaxFoodHeight = 0.2f * Size;

        private void ShowFood(Stock stock)
        {
            if (foodModels) Destroy(foodModels);
            foodModels = null;
            int total = stock.Total;
            if (total == 0 || Jotunn.Managers.GUIManager.IsHeadless() || !ObjectDB.instance || !Valid) return;
            // Built while switched off so the copies' own scripts never start, then switched on once stripped.
            foodModels = new GameObject("Food");
            foodModels.SetActive(false);
            foodModels.transform.SetParent(transform, false);
            int models = Mathf.Clamp(Mathf.CeilToInt(total / 3f), 1, MaxFoodModels), placed = 0;
            // Seeded by the trough itself, so the food lies the same way every time and for every player.
            var id = view.GetZDO().m_uid;
            var random = new System.Random(unchecked((int)id.ID * 31 + id.UserID.GetHashCode()));
            foreach (var entry in stock.Entries)
            {
                var prefab = ObjectDB.instance.GetItemPrefab(entry.Key);
                if (!prefab) continue;
                int count = Mathf.Max(1, Mathf.RoundToInt(models * (float)entry.Value / total));
                for (int i = 0; i < count && placed < models; i++, placed++)
                {
                    var copy = Instantiate(prefab, foodModels.transform, false);
                    Strip(copy);
                    float along = models == 1 ? 0f : Mathf.Lerp(-0.55f * Size, 0.55f * Size, placed / (float)(models - 1));
                    Place(copy, along + (float)(random.NextDouble() - 0.5) * 0.12f, (float)random.NextDouble(), random);
                }
            }
            foodModels.SetActive(true);
        }

        // Lays a food model flat with its longest side along the trough, shrinks it if it couldn't fit, and moves it
        // as close to the wanted spot as the walls allow, resting on the floor.
        private static void Place(GameObject copy, float wantX, float sideways, System.Random random)
        {
            var scale = copy.transform.localScale;
            if (!LocalBounds(copy, out var bounds))
            {
                copy.transform.localPosition = new Vector3(Mathf.Clamp(wantX, -0.6f * Size, 0.6f * Size), FloorTop + 0.03f, 0f);
                return;
            }
            var size = Vector3.Scale(bounds.size, scale);
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            var order = new[] { 0, 1, 2 }.OrderByDescending(axis => size[axis]).ToArray();
            // Longest side along the trough, middle across it, shortest pointing up.
            var lay = Quaternion.Inverse(Quaternion.LookRotation(axes[order[1]], axes[order[2]]));
            var rotation = Quaternion.Euler(0f, (random.Next(2) * 180f) + (float)(random.NextDouble() - 0.5) * 24f, 0f) * lay;

            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                corners[i] = rotation * Vector3.Scale(corner, scale);
            }
            var low = corners.Aggregate(Vector3.Min);
            var high = corners.Aggregate(Vector3.Max);
            var span = high - low;
            float fit = Mathf.Min(1f, 2f * InnerHalfLength / Mathf.Max(span.x, 0.001f), 2f * InnerHalfWidth / Mathf.Max(span.z, 0.001f), MaxFoodHeight / Mathf.Max(span.y, 0.001f));
            low *= fit;
            high *= fit;
            float minX = -InnerHalfLength - low.x, maxX = InnerHalfLength - high.x;
            float minZ = -InnerHalfWidth - low.z, maxZ = InnerHalfWidth - high.z;
            float x = minX <= maxX ? Mathf.Clamp(wantX, minX, maxX) : (minX + maxX) / 2f;
            float z = minZ <= maxZ ? Mathf.Lerp(minZ, maxZ, sideways) : (minZ + maxZ) / 2f;
            copy.transform.localScale = scale * fit;
            copy.transform.localRotation = rotation;
            copy.transform.localPosition = new Vector3(x, FloorTop - low.y, z);
        }

        private static bool Shown(Transform part, Transform root)
        {
            for (; part && part != root; part = part.parent)
                if (!part.gameObject.activeSelf) return false;
            return true;
        }

        // The model's visible size in its own space, from its meshes (Renderer.bounds is empty while switched off).
        private static bool LocalBounds(GameObject copy, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            var toCopy = copy.transform.worldToLocalMatrix;
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                // Skip hidden parts: some foods (berries, carrots) carry a switched-off extra model that would
                // otherwise make them look far bigger than they are.
                if (!renderer.enabled || !Shown(renderer.transform, copy.transform)) continue;
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh
                    : renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                if (!mesh) continue;
                var matrix = toCopy * renderer.transform.localToWorldMatrix;
                var b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var point = matrix.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return any;
        }

        private static void Strip(GameObject copy)
        {
            foreach (var system in copy.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.playOnAwake = false;
                var emission = system.emission;
                emission.enabled = false;
                var renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer) renderer.enabled = false;
            }
            foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(script);
            foreach (var joint in copy.GetComponentsInChildren<Joint>(true)) DestroyImmediate(joint);
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
            foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(body);
            foreach (var light in copy.GetComponentsInChildren<Light>(true)) DestroyImmediate(light);
            foreach (var audio in copy.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(audio);
            int layer = LayerMask.NameToLayer("piece_nonsolid");
            foreach (var part in copy.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;
        }
    }
}
