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
        public const string Id = "potato.farmer", Version = "1.0.0";
        // Saved in worlds: never rename.
        public const string TroughPiece = "piece_potato_trough";
        public const string TroughName = "$piece_potato_trough";
        private const string TroughDescription = "$piece_potato_trough_description";
        private const string TroughSection = "1 - Trough"; // No apostrophes: BepInEx rejects them in section names.

        internal static ManualLogSource Log;
        internal static ConfigEntry<int> TroughCapacity;
        internal static ConfigEntry<bool> TroughEnabled, TroughShowFood;
        internal static ConfigEntry<float> TroughReach;
        private static Piece troughPiece;

        private void Awake()
        {
            Log = Logger;
            Assets.Dir = Path.Combine(Path.GetDirectoryName(Info.Location) ?? "", "assets");
            var serverSet = new ConfigurationManagerAttributes { IsAdminOnly = true };
            TroughEnabled = Config.Bind(TroughSection, "Enabled", true,
                new ConfigDescription("Show the feeding trough on the hammer. Troughs already built stay and keep working. Set by the server.", null, serverSet));
            TroughCapacity = Config.Bind(TroughSection, "Capacity", 40,
                new ConfigDescription("How many food items one trough holds. Set by the server.", new AcceptableValueRange<int>(1, 500), serverSet));
            TroughReach = Config.Bind(TroughSection, "Reach", 25f,
                new ConfigDescription("How far away (metres) hungry animals notice the trough and walk over to eat. 25 covers a big farm pen; animals on their own only look about 10 m for food. Set by the server.",
                    new AcceptableValueRange<float>(5f, 60f), serverSet));
            TroughShowFood = Config.Bind(TroughSection, "Show food in the bowl", true, "Show what is in the trough as food models in the bowl (only on your screen).");
            TroughEnabled.SettingChanged += (_, __) => { if (troughPiece) troughPiece.m_enabled = TroughEnabled.Value; };
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
            });
            try { AddTrough(); }
            catch (Exception e) { Log.LogError("Couldn't add the feeding trough: " + e); }
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
            AddBox(prefab, "Floor", new Vector3(0f, 0.11f, 0f), new Vector3(1.52f, 0.215f, 0.52f));
            AddBox(prefab, "Side", new Vector3(0f, 0.28f, 0.25f), new Vector3(1.52f, 0.14f, 0.06f));
            AddBox(prefab, "Side", new Vector3(0f, 0.28f, -0.25f), new Vector3(1.52f, 0.14f, 0.06f));
            AddBox(prefab, "End", new Vector3(0.88f, 0.25f, 0f), new Vector3(0.24f, 0.5f, 0.42f));
            AddBox(prefab, "End", new Vector3(-0.88f, 0.25f, 0f), new Vector3(0.24f, 0.5f, 0.42f));
            var wear = prefab.GetComponent<WearNTear>();
            wear.m_materialType = WearNTear.MaterialType.Wood;
            prefab.AddComponent<Trough>();
            troughPiece = piece.Piece;
            troughPiece.m_enabled = TroughEnabled.Value;
            if (!GUIManager.IsHeadless()) troughPiece.m_icon = RenderManager.Instance.Render(prefab, Quaternion.Euler(20f, 200f, 0f));
            PieceManager.Instance.AddPiece(piece);
            Log.LogInfo("Feeding trough added to the hammer.");
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

        private static void AddBox(GameObject prefab, string name, Vector3 center, Vector3 size)
        {
            center *= Trough.Size;
            size *= Trough.Size;
            var part = new GameObject(name);
            part.layer = prefab.layer;
            part.transform.SetParent(prefab.transform, false);
            var box = part.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }
    }
}
