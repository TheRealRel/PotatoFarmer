// PotatoFarmer - farming and animal pieces with their own models, written from scratch. First piece: the feeding trough.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace PotatoFarmer
{
    // Every piece here needs to be on the server and every player: no process filter, so dedicated servers load it
    // and know the pieces (a server deletes pieces it doesn't know).
    [BepInPlugin(Id, "PotatoFarmer", Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Id = "potato.farmer", Version = "1.3.1";
        // Saved in worlds: never rename.
        public const string TroughPiece = "piece_potato_trough";
        public const string TroughName = "$piece_potato_trough";
        private const string TroughDescription = "$piece_potato_trough_description";
        public const string RoostPiece = "piece_potato_roost";
        public const string RoostName = "$piece_potato_roost";
        private const string RoostDescription = "$piece_potato_roost_description";
        public const string CaskPiece = "piece_potato_cask";
        public const string CaskName = "$piece_potato_cask";
        private const string CaskDescription = "$piece_potato_cask_description";
        public const string LickPiece = "piece_potato_lick";
        public const string LickName = "$piece_potato_lick";
        private const string LickDescription = "$piece_potato_lick_description";
        public const string PostPiece = "piece_potato_post";
        public const string PostName = "$piece_potato_post";
        private const string PostDescription = "$piece_potato_post_description";

        // One section, so Configuration Manager stays short; each setting starts with the piece it belongs to.
        // No apostrophes or = \ " [ ] in names: BepInEx rejects them.
        private const string Section = "1 - Pieces";

        internal static ManualLogSource Log;
        internal static ConfigEntry<int> TroughCapacity, RoostCapacity, RoostFlock, CaskBatches, PostAnimals;
        internal static ConfigEntry<bool> TroughEnabled, TroughShowFood, RoostEnabled, CaskEnabled, CaskMix, LickEnabled, PostEnabled;
        internal static ConfigEntry<float> TroughReach, RoostReach, LickReach, LickFedTime, PostRope;
        private static Piece troughPiece, roostPiece, caskPiece, lickPiece, postPiece;

        private void Awake()
        {
            Log = Logger;
            Assets.Dir = Path.Combine(Path.GetDirectoryName(Info.Location) ?? "", "assets");
            var before = ConfigMove.Read(Config.ConfigFilePath);
            var serverSet = new ConfigurationManagerAttributes { IsAdminOnly = true };
            TroughEnabled = Config.Bind(Section, "Trough", true,
                new ConfigDescription("Show the feeding trough on the hammer. Troughs already built stay and keep working. Set by the server.", null, serverSet));
            TroughCapacity = Config.Bind(Section, "Trough capacity", 100,
                new ConfigDescription("How many food items one trough holds. Set by the server.", new AcceptableValueRange<int>(1, 500), serverSet));
            TroughReach = Config.Bind(Section, "Trough reach", 25f,
                new ConfigDescription("How far away (metres) hungry animals notice the trough and walk over to eat. 25 covers a big farm pen; animals on their own only look about 10 m for food. Set by the server.",
                    new AcceptableValueRange<float>(5f, 60f), serverSet));
            TroughShowFood = Config.Bind(Section, "Trough food in the bowl", true, "Show what is in the trough as food models in the bowl (only on your screen).");
            TroughEnabled.SettingChanged += (_, __) => { if (troughPiece) troughPiece.m_enabled = TroughEnabled.Value; };
            RoostEnabled = Config.Bind(Section, "Roost", true,
                new ConfigDescription("Show the roost on the hammer. Roosts already built stay and keep working. Set by the server.", null, serverSet));
            RoostCapacity = Config.Bind(Section, "Roost capacity", 60,
                new ConfigDescription("How many eggs one roost holds. Set by the server.", new AcceptableValueRange<int>(1, 500), serverSet));
            RoostReach = Config.Bind(Section, "Roost reach", 10f,
                new ConfigDescription("How far away (metres) the roost gathers eggs. Hens only breed with others within 10 m, so 10 covers a coop. Set by the server.",
                    new AcceptableValueRange<float>(2f, 30f), serverSet));
            RoostFlock = Config.Bind(Section, "Roost flock size", 6,
                new ConfigDescription("With a fire near the roost, it hatches its eggs one at a time while there are fewer hens (and chicks) than this within its reach. 0 = never hatch. Set by the server.",
                    new AcceptableValueRange<int>(0, 30), serverSet));
            RoostEnabled.SettingChanged += (_, __) => { if (roostPiece) roostPiece.m_enabled = RoostEnabled.Value; };
            CaskEnabled = Config.Bind(Section, "Cask", true,
                new ConfigDescription("Show the cask on the hammer. Casks already built stay and keep working. Set by the server.", null, serverSet));
            CaskBatches = Config.Bind(Section, "Cask batches", 4,
                new ConfigDescription("How many mead bases one cask brews at once, each like a fermenter of its own. Set by the server.", new AcceptableValueRange<int>(1, 20), serverSet));
            CaskMix = Config.Bind(Section, "Cask mixes mead bases", false,
                new ConfigDescription("Let one cask brew different mead bases at once. Off: a cask brews one kind at a time, until it's emptied. Set by the server.", null, serverSet));
            CaskEnabled.SettingChanged += (_, __) => { if (caskPiece) caskPiece.m_enabled = CaskEnabled.Value; };
            LickEnabled = Config.Bind(Section, "Lick", true,
                new ConfigDescription("Show the salt lick on the hammer. Licks already built stay and keep working. Set by the server.", null, serverSet));
            LickFedTime = Config.Bind(Section, "Lick fed time", 2f,
                new ConfigDescription("Tamed animals near a lick stay fed this many times as long between meals (on top of any other mod that changes it). 1 = no change. Set by the server.",
                    new AcceptableValueRange<float>(1f, 5f), serverSet));
            LickReach = Config.Bind(Section, "Lick reach", 10f,
                new ConfigDescription("How far away (metres) the lick works. 10 covers a pen. Set by the server.", new AcceptableValueRange<float>(2f, 30f), serverSet));
            LickEnabled.SettingChanged += (_, __) => { if (lickPiece) lickPiece.m_enabled = LickEnabled.Value; };
            PostEnabled = Config.Bind(Section, "Post", true,
                new ConfigDescription("Show the hitching post on the hammer. Posts already built stay and keep working. Set by the server.", null, serverSet));
            PostAnimals = Config.Bind(Section, "Post animals", 4,
                new ConfigDescription("How many tamed animals one post holds. Set by the server.", new AcceptableValueRange<int>(1, 10), serverSet));
            PostRope = Config.Bind(Section, "Post rope length", 4f,
                new ConfigDescription("How far (metres) a tied animal can wander from beside the post. Set by the server.", new AcceptableValueRange<float>(2f, 10f), serverSet));
            PostEnabled.SettingChanged += (_, __) => { if (postPiece) postPiece.m_enabled = PostEnabled.Value; };
            // 1.3.0 and before had a section per piece ("1 - Trough" / "Enabled"...).
            ConfigMove.Apply(Config, before, new Dictionary<string, ConfigEntryBase>
            {
                { "1 - Trough|Enabled", TroughEnabled },
                { "1 - Trough|Capacity", TroughCapacity },
                { "1 - Trough|Reach", TroughReach },
                { "1 - Trough|Show food in the bowl", TroughShowFood },
                { "2 - Roost|Enabled", RoostEnabled },
                { "2 - Roost|Capacity", RoostCapacity },
                { "2 - Roost|Reach", RoostReach },
                { "2 - Roost|Flock size", RoostFlock },
                { "3 - Cask|Enabled", CaskEnabled },
                { "3 - Cask|Batches", CaskBatches },
                { "3 - Cask|Mix mead bases", CaskMix },
                { "4 - Lick|Enabled", LickEnabled },
                { "4 - Lick|Fed time", LickFedTime },
                { "4 - Lick|Reach", LickReach },
                { "5 - Post|Enabled", PostEnabled },
                { "5 - Post|Animals per post", PostAnimals },
                { "5 - Post|Rope length", PostRope },
            });
            new Harmony(Id).PatchAll(typeof(Plugin).Assembly);
            PrefabManager.OnVanillaPrefabsAvailable += AddPieces;
        }

        private static void AddPieces()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddPieces;
            LocalizationManager.Instance.GetLocalization().AddTranslation("English", new Dictionary<string, string>
            {
                { TroughName.Substring(1), "Gullinbursti's Trough" },
                { TroughDescription.Substring(1), "A feeding trough blessed by Freyr's golden boar. Fill it with food and hungry animals nearby come to eat: taming, breeding and feeding all count." },
                { RoostName.Substring(1), "Gullinkambi's Roost" },
                { RoostDescription.Substring(1), "A nesting box watched over by Gullinkambi, the golden-combed rooster of Valhalla. Gathers the eggs your hens lay nearby, so they keep laying." },
                { CaskName.Substring(1), "Heiðrún's Cask" },
                { CaskDescription.Substring(1), "A great mead cask blessed by Heiðrún, the goat whose udder runs with mead in Valhalla. Brews several mead bases at once, each like a fermenter of its own. Needs a roof." },
                { LickName.Substring(1), "Auðhumla's Lick" },
                { PostName.Substring(1), "Sleipnir's Post" },
                { PostDescription.Substring(1), "A hitching post carved for Sleipnir, Odin's eight-legged horse. Bring a tamed animal that follows you and tie it here, and it stays put instead of wandering off." },
                { LickDescription.Substring(1), "A block of salt on carved stone, for Auðhumla, the first cow, who licked the first god out of the salt ice. Tamed animals nearby stay fed longer between meals." },
            });
            try { AddTrough(); }
            catch (Exception e) { Log.LogError("Couldn't add the feeding trough: " + e); }
            try { AddRoost(); }
            catch (Exception e) { Log.LogError("Couldn't add the roost: " + e); }
            try { AddCask(); }
            catch (Exception e) { Log.LogError("Couldn't add the cask: " + e); }
            try { AddLick(); }
            catch (Exception e) { Log.LogError("Couldn't add the salt lick: " + e); }
            try { AddPost(); }
            catch (Exception e) { Log.LogError("Couldn't add the hitching post: " + e); }
        }

        private static void AddTrough()
        {
            var piece = new CustomPiece(TroughPiece, "piece_table", new PieceConfig
            {
                Name = TroughName,
                Description = TroughDescription,
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = "forge",
                Requirements = new[] { new RequirementConfig("Wood", 10, 0, true), new RequirementConfig("Bronze", 2, 0, true) },
            });
            if (!piece.PiecePrefab)
            {
                Log.LogWarning("The wooden table to build the trough from wasn't found; no trough this session.");
                return;
            }
            var mesh = Assets.LoadMesh("trough", Trough.Size);
            if (!mesh) return;
            var look = new PieceLook("trough", 1f, 0.6f);
            var prefab = piece.PiecePrefab;
            Rebuild(prefab, mesh, look.Material);
            // Simple boxes rather than the detailed mesh: the floor, the two long walls and the boar-head end posts.
            AddBox(prefab, "Floor", new Vector3(0f, 0.11f, 0f), new Vector3(1.52f, 0.215f, 0.52f), Trough.Size);
            AddBox(prefab, "Side", new Vector3(0f, 0.28f, 0.25f), new Vector3(1.52f, 0.14f, 0.06f), Trough.Size);
            AddBox(prefab, "Side", new Vector3(0f, 0.28f, -0.25f), new Vector3(1.52f, 0.14f, 0.06f), Trough.Size);
            AddBox(prefab, "End", new Vector3(0.88f, 0.25f, 0f), new Vector3(0.24f, 0.5f, 0.42f), Trough.Size);
            AddBox(prefab, "End", new Vector3(-0.88f, 0.25f, 0f), new Vector3(0.24f, 0.5f, 0.42f), Trough.Size);
            var wear = prefab.GetComponent<WearNTear>();
            wear.m_materialType = WearNTear.MaterialType.Wood;
            prefab.AddComponent<Trough>();
            troughPiece = piece.Piece;
            troughPiece.m_enabled = TroughEnabled.Value;
            if (!GUIManager.IsHeadless()) troughPiece.m_icon = RenderManager.Instance.Render(prefab, IconView(25f, 30f));
            PieceManager.Instance.AddPiece(piece);
            Log.LogInfo("Feeding trough added to the hammer.");
        }

        private static void AddRoost()
        {
            var piece = new CustomPiece(RoostPiece, "piece_table", new PieceConfig
            {
                Name = RoostName,
                Description = RoostDescription,
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = "forge",
                Requirements = new[] { new RequirementConfig("FineWood", 10, 0, true), new RequirementConfig("BlackMetal", 2, 0, true) },
            });
            if (!piece.PiecePrefab)
            {
                Log.LogWarning("The wooden table to build the roost from wasn't found; no roost this session.");
                return;
            }
            var mesh = Assets.LoadMesh("roost", Roost.Size);
            if (!mesh) return;
            // The black metal chest's material: its metal gets the game's green black-metal sheen. Its blotchy value
            // noise is turned off, which mottled the wood.
            var look = new PieceLook("roost", 1f, -1f, "BlackMetalChest_mat", valueNoise: 0f);
            var prefab = piece.PiecePrefab;
            Rebuild(prefab, mesh, look.Material);
            // The base under the cubbies, the lid, the back wall, the two side walls and the rooster-head end posts.
            AddBox(prefab, "Base", new Vector3(0f, 0.12f, 0f), new Vector3(1.5f, 0.245f, 0.5f), Roost.Size);
            AddBox(prefab, "Lid", new Vector3(0f, 0.52f, 0f), new Vector3(1.6f, 0.19f, 0.64f), Roost.Size);
            AddBox(prefab, "Back", new Vector3(0f, 0.335f, -0.19f), new Vector3(1.5f, 0.19f, 0.09f), Roost.Size);
            AddBox(prefab, "Side", new Vector3(0.715f, 0.335f, 0f), new Vector3(0.07f, 0.19f, 0.45f), Roost.Size);
            AddBox(prefab, "Side", new Vector3(-0.715f, 0.335f, 0f), new Vector3(0.07f, 0.19f, 0.45f), Roost.Size);
            AddBox(prefab, "End", new Vector3(0.85f, 0.42f, 0f), new Vector3(0.2f, 0.84f, 0.22f), Roost.Size);
            AddBox(prefab, "End", new Vector3(-0.85f, 0.42f, 0f), new Vector3(0.2f, 0.84f, 0.22f), Roost.Size);
            var wear = prefab.GetComponent<WearNTear>();
            wear.m_materialType = WearNTear.MaterialType.Wood;
            prefab.AddComponent<Roost>();
            roostPiece = piece.Piece;
            roostPiece.m_enabled = RoostEnabled.Value;
            if (!GUIManager.IsHeadless()) roostPiece.m_icon = RenderManager.Instance.Render(prefab, IconView(25f, 25f));
            PieceManager.Instance.AddPiece(piece);
            Log.LogInfo("Roost added to the hammer.");
        }

        private static void AddCask()
        {
            var piece = new CustomPiece(CaskPiece, "piece_table", new PieceConfig
            {
                Name = CaskName,
                Description = CaskDescription,
                PieceTable = "Hammer",
                Category = "Crafting",
                CraftingStation = "forge",
                Requirements = new[] { new RequirementConfig("FineWood", 30, 0, true), new RequirementConfig("BlackMetal", 6, 0, true), new RequirementConfig("Tar", 4, 0, true), new RequirementConfig("IronNails", 20, 0, true) },
            });
            if (!piece.PiecePrefab)
            {
                Log.LogWarning("The wooden table to build the cask from wasn't found; no cask this session.");
                return;
            }
            var mesh = Assets.LoadMesh("cask", Cask.Size);
            if (!mesh) return;
            // Black metal hoops and fine wood, drawn like the roost with the black metal chest's material.
            var look = new PieceLook("cask", 1f, -1f, "BlackMetalChest_mat", valueNoise: 0f);
            var prefab = piece.PiecePrefab;
            var visual = Rebuild(prefab, mesh, look.Material);
            // The hoops are a model of their own in plain black metal, so their edges follow the model rather than the
            // texture's pixels, and the metal's green sheen never touches the wood.
            var hoopMesh = Assets.LoadMesh("caskhoops", Cask.Size);
            if (hoopMesh)
            {
                var hoops = new GameObject("Hoops");
                hoops.layer = prefab.layer;
                hoops.transform.SetParent(visual.transform, false);
                hoops.AddComponent<MeshFilter>().sharedMesh = hoopMesh;
                hoops.AddComponent<MeshRenderer>().sharedMaterial = new PieceLook("caskhoops", 1f, -1f, "BlackMetalChest_mat", valueNoise: 0f).Material;
            }
            // The barrel lying on its side, and the cradle under it.
            AddBox(prefab, "Barrel", new Vector3(0.055f, 1.19f, 0f), new Vector3(1.71f, 1.78f, 1.81f), Cask.Size);
            AddBox(prefab, "Cradle", new Vector3(0.064f, 0.2f, 0f), new Vector3(1.87f, 0.4f, 1.64f), Cask.Size);
            var wear = prefab.GetComponent<WearNTear>();
            wear.m_materialType = WearNTear.MaterialType.Wood;
            prefab.AddComponent<Cask>();
            caskPiece = piece.Piece;
            caskPiece.m_enabled = CaskEnabled.Value;
            if (!GUIManager.IsHeadless()) caskPiece.m_icon = RenderManager.Instance.Render(prefab, IconView(55f, 15f));
            PieceManager.Instance.AddPiece(piece);
            Log.LogInfo("Cask added to the hammer.");
        }

        // The hammer icon's view: Jötunn's camera looks at the model's front (+Z, the side the player faces when placing
        // it) from the front; turn it by yaw degrees round the model (towards the model's -X end) and look down by pitch.
        // Rotation order matters: the yaw first, then the tilt, so the camera always ends up above the model.
        private static Quaternion IconView(float yaw, float pitch) => Quaternion.Euler(pitch, 0f, 0f) * Quaternion.Euler(0f, yaw, 0f);

        private static void AddLick()
        {
            var piece = new CustomPiece(LickPiece, "piece_table", new PieceConfig
            {
                Name = LickName,
                Description = LickDescription,
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = "forge",
                Requirements = new[] { new RequirementConfig("Stone", 10, 0, true), new RequirementConfig("Flint", 4, 0, true), new RequirementConfig("Tin", 2, 0, true) },
            });
            if (!piece.PiecePrefab)
            {
                Log.LogWarning("The wooden table to build the salt lick from wasn't found; no salt lick this session.");
                return;
            }
            var mesh = Assets.LoadMesh("lick", Lick.Size);
            if (!mesh) return;
            // Matte stone and salt, no shine.
            var look = new PieceLook("lick", 1f, 0f);
            var prefab = piece.PiecePrefab;
            Rebuild(prefab, mesh, look.Material);
            // The stone base with the salt block, and the carved backboard.
            AddBox(prefab, "Base", new Vector3(0f, 0.225f, 0f), new Vector3(1f, 0.45f, 0.78f), Lick.Size);
            AddBox(prefab, "Back", new Vector3(0.01f, 0.79f, -0.31f), new Vector3(0.72f, 0.7f, 0.17f), Lick.Size);
            var wear = prefab.GetComponent<WearNTear>();
            wear.m_materialType = WearNTear.MaterialType.Stone;
            prefab.AddComponent<Lick>();
            lickPiece = piece.Piece;
            lickPiece.m_enabled = LickEnabled.Value;
            if (!GUIManager.IsHeadless()) lickPiece.m_icon = RenderManager.Instance.Render(prefab, IconView(25f, 15f));
            PieceManager.Instance.AddPiece(piece);
            Log.LogInfo("Salt lick added to the hammer.");
        }

        private static void AddPost()
        {
            var piece = new CustomPiece(PostPiece, "piece_table", new PieceConfig
            {
                Name = PostName,
                Description = PostDescription,
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = "forge",
                Requirements = new[] { new RequirementConfig("RoundLog", 4, 0, true), new RequirementConfig("Bronze", 1, 0, true) },
            });
            if (!piece.PiecePrefab)
            {
                Log.LogWarning("The wooden table to build the hitching post from wasn't found; no hitching post this session.");
                return;
            }
            var mesh = Assets.LoadMesh("post", Post.Size);
            if (!mesh) return;
            // Core wood with bronze rings, like the trough.
            var look = new PieceLook("post", 1f, 0.6f);
            var prefab = piece.PiecePrefab;
            Rebuild(prefab, mesh, look.Material);
            // The pole, and the rings sticking out at the top.
            AddBox(prefab, "Pole", new Vector3(0f, 1f, 0f), new Vector3(0.32f, 2f, 0.32f), Post.Size);
            AddBox(prefab, "Rings", new Vector3(0f, 1.5f, 0f), new Vector3(0.72f, 0.4f, 0.1f), Post.Size);
            var wear = prefab.GetComponent<WearNTear>();
            wear.m_materialType = WearNTear.MaterialType.Wood;
            prefab.AddComponent<Post>();
            postPiece = piece.Piece;
            postPiece.m_enabled = PostEnabled.Value;
            if (!GUIManager.IsHeadless()) postPiece.m_icon = RenderManager.Instance.Render(prefab, IconView(20f, 10f));
            PieceManager.Instance.AddPiece(piece);
            Log.LogInfo("Hitching post added to the hammer.");
        }

        // A vanilla piece supplies the basics (building, wear, hit and break effects); its looks and colliders are
        // replaced with the custom model.
        private static GameObject Rebuild(GameObject prefab, Mesh mesh, Material material)
        {
            foreach (var child in prefab.transform.Cast<Transform>().ToList()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach (var component in prefab.GetComponents<Component>())
            {
                if (component is Transform || component is ZNetView || component is Piece || component is WearNTear) continue;
                Log.LogDebug("Removing the base piece's " + component.GetType().Name);
                UnityEngine.Object.DestroyImmediate(component);
            }
            var visual = new GameObject("Model");
            visual.layer = prefab.layer;
            visual.transform.SetParent(prefab.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;
            var piece = prefab.GetComponent<Piece>();
            piece.m_comfort = 0;
            piece.m_comfortGroup = Piece.ComfortGroup.None;
            piece.m_primaryTarget = false;
            var wear = prefab.GetComponent<WearNTear>();
            wear.m_new = wear.m_worn = wear.m_broken = visual;
            wear.m_fragmentRoots = null;
            wear.m_autoCreateFragments = false;
            return visual;
        }

        private static void AddBox(GameObject prefab, string name, Vector3 center, Vector3 size, float scale)
        {
            center *= scale;
            size *= scale;
            var part = new GameObject(name);
            part.layer = prefab.layer;
            part.transform.SetParent(prefab.transform, false);
            var box = part.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }
    }
}
