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
        public const string Id = "potato.farmer", Version = "1.1.3";
        // Saved in worlds: never rename.
        public const string TroughPiece = "piece_potato_trough";
        public const string TroughName = "$piece_potato_trough";
        private const string TroughDescription = "$piece_potato_trough_description";
        private const string TroughSection = "1 - Trough"; // No apostrophes: BepInEx rejects them in section names.
        public const string RoostPiece = "piece_potato_roost";
        public const string RoostName = "$piece_potato_roost";
        private const string RoostDescription = "$piece_potato_roost_description";
        private const string RoostSection = "2 - Roost";

        internal static ManualLogSource Log;
        internal static ConfigEntry<int> TroughCapacity, RoostCapacity, RoostFlock;
        internal static ConfigEntry<bool> TroughEnabled, TroughShowFood, RoostEnabled;
        internal static ConfigEntry<float> TroughReach, RoostReach;
        private static Piece troughPiece, roostPiece;

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
            });
            try { AddTrough(); }
            catch (Exception e) { Log.LogError("Couldn't add the feeding trough: " + e); }
            try { AddRoost(); }
            catch (Exception e) { Log.LogError("Couldn't add the roost: " + e); }
        }

        private static void AddTrough()
        {
            var piece = new CustomPiece(TroughPiece, "piece_table", new PieceConfig
            {
                Name = TroughName,
                Description = TroughDescription,
                PieceTable = "Hammer",
                Category = "Furniture",
                CraftingStation = "piece_workbench",
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
                CraftingStation = "piece_workbench",
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
