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
        public const string Id = "potato.farmer", Version = "1.2.0";
        // Saved in worlds: never rename.
        public const string TroughPiece = "piece_potato_trough";
        public const string TroughName = "$piece_potato_trough";
        private const string TroughDescription = "$piece_potato_trough_description";
        private const string TroughSection = "1 - Trough"; // No apostrophes: BepInEx rejects them in section names.
        public const string RoostPiece = "piece_potato_roost";
        public const string RoostName = "$piece_potato_roost";
        private const string RoostDescription = "$piece_potato_roost_description";
        private const string RoostSection = "2 - Roost";
        public const string CaskPiece = "piece_potato_cask";
        public const string CaskName = "$piece_potato_cask";
        private const string CaskDescription = "$piece_potato_cask_description";
        private const string CaskSection = "3 - Cask";

        internal static ManualLogSource Log;
        internal static ConfigEntry<int> TroughCapacity, RoostCapacity, RoostFlock, CaskBatches;
        internal static ConfigEntry<bool> TroughEnabled, TroughShowFood, RoostEnabled, CaskEnabled, CaskMix;
        internal static ConfigEntry<float> TroughReach, RoostReach;
        private static Piece troughPiece, roostPiece, caskPiece;

        private void Awake()
        {
            Log = Logger;
            Assets.Dir = Path.Combine(Path.GetDirectoryName(Info.Location) ?? "", "assets");
            var serverSet = new ConfigurationManagerAttributes { IsAdminOnly = true };
            TroughEnabled = Config.Bind(TroughSection, "Enabled", true,
                new ConfigDescription("Show the feeding trough on the hammer. Troughs already built stay and keep working. Set by the server.", null, serverSet));
            TroughCapacity = Config.Bind(TroughSection, "Capacity", 100,
                new ConfigDescription("How many food items one trough holds. Set by the server.", new AcceptableValueRange<int>(1, 500), serverSet));
            TroughReach = Config.Bind(TroughSection, "Reach", 25f,
                new ConfigDescription("How far away (metres) hungry animals notice the trough and walk over to eat. 25 covers a big farm pen; animals on their own only look about 10 m for food. Set by the server.",
                    new AcceptableValueRange<float>(5f, 60f), serverSet));
            TroughShowFood = Config.Bind(TroughSection, "Show food in the bowl", true, "Show what is in the trough as food models in the bowl (only on your screen).");
            TroughEnabled.SettingChanged += (_, __) => { if (troughPiece) troughPiece.m_enabled = TroughEnabled.Value; };
            RoostEnabled = Config.Bind(RoostSection, "Enabled", true,
                new ConfigDescription("Show the roost on the hammer. Roosts already built stay and keep working. Set by the server.", null, serverSet));
            RoostCapacity = Config.Bind(RoostSection, "Capacity", 60,
                new ConfigDescription("How many eggs one roost holds. Set by the server.", new AcceptableValueRange<int>(1, 500), serverSet));
            RoostReach = Config.Bind(RoostSection, "Reach", 10f,
                new ConfigDescription("How far away (metres) the roost gathers eggs. Hens only breed with others within 10 m, so 10 covers a coop. Set by the server.",
                    new AcceptableValueRange<float>(2f, 30f), serverSet));
            RoostFlock = Config.Bind(RoostSection, "Flock size", 6,
                new ConfigDescription("With a fire near the roost, it hatches its eggs one at a time while there are fewer hens (and chicks) than this within its reach. 0 = never hatch. Set by the server.",
                    new AcceptableValueRange<int>(0, 30), serverSet));
            RoostEnabled.SettingChanged += (_, __) => { if (roostPiece) roostPiece.m_enabled = RoostEnabled.Value; };
            CaskEnabled = Config.Bind(CaskSection, "Enabled", true,
                new ConfigDescription("Show the cask on the hammer. Casks already built stay and keep working. Set by the server.", null, serverSet));
            CaskBatches = Config.Bind(CaskSection, "Batches", 4,
                new ConfigDescription("How many mead bases one cask brews at once, each like a fermenter of its own. Set by the server.", new AcceptableValueRange<int>(1, 20), serverSet));
            CaskMix = Config.Bind(CaskSection, "Mix mead bases", false,
                new ConfigDescription("Let one cask brew different mead bases at once. Off: a cask brews one kind at a time, until it's emptied. Set by the server.", null, serverSet));
            CaskEnabled.SettingChanged += (_, __) => { if (caskPiece) caskPiece.m_enabled = CaskEnabled.Value; };
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
            });
            try { AddTrough(); }
            catch (Exception e) { Log.LogError("Couldn't add the feeding trough: " + e); }
            try { AddRoost(); }
            catch (Exception e) { Log.LogError("Couldn't add the roost: " + e); }
            try { AddCask(); }
            catch (Exception e) { Log.LogError("Couldn't add the cask: " + e); }
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
            if (!GUIManager.IsHeadless()) troughPiece.m_icon = RenderManager.Instance.Render(prefab, Quaternion.Euler(20f, 200f, 0f));
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
            if (!GUIManager.IsHeadless()) roostPiece.m_icon = RenderManager.Instance.Render(prefab, Quaternion.Euler(20f, 200f, 0f));
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
            if (!GUIManager.IsHeadless()) caskPiece.m_icon = RenderManager.Instance.Render(prefab, Quaternion.Euler(20f, 230f, 0f));
            PieceManager.Instance.AddPiece(piece);
            Log.LogInfo("Cask added to the hammer.");
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
