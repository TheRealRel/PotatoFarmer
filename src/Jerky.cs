// The jerkies Læraðr's Rack makes for raw meats the game has no jerky for. Each is a copy of the game's Boar Jerky
// with its own model, colour, icon and food values (health = stamina = about three quarters of the cooked meat's
// health, regen as the cooked meat, 30 minutes, like the game's own jerkies).
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Bootstrap;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace PotatoFarmer
{
    internal static class Jerky
    {
        internal sealed class Kind
        {
            // Prefab names are saved in inventories and chests: never rename.
            public string Prefab, Meat, Name, Shape, Look, Mod;
            public float Health, Stamina, Regen;
            public int Level;   // cauldron level for its recipe
            public float Size = 1f;   // drawn size against our 42 cm model (see Sizes below)
        }

        // Cauldron levels (the jerky can also be made at the cauldron, like the game's own: 1 meat + 1 honey -> 2), set
        // from the game's cauldron recipes for the same meat or the same biome (read 2026-11-03 from c4210710): level 1
        // Meadows/Black Forest (Boar Jerky, Deer Stew, Minced Meat Sauce, Pulled Bear; Monstrum's Bear Jerky), 2 Serpent
        // Stew, 3 Wolf Jerky, 4 Plains (Blood Pudding, Fish Wraps), 5 Mistlands and Seeker Aspic, Fiery Svinstew
        // (Asksvin), Monstrum's Bloodgill Fin Soup, 6 Ashlands (Mashed Meat: Asksvin + Volture), 7 Deep North (every
        // moose dish).
        // Shapes: four Meshy models, recoloured per meat (assets/jerkyshape_<shape>.mesh, jerky_<look>_d.png).
        // Sizes: each jerky is sized by the meat it's made from, by the game's own rule. The game's two jerkies keep the
        // same share of their meat: Boar Jerky 10.5 % of Raw meat's volume (0.56 of its length), Wolf Jerky 11.2 %
        // (0.53). Applied to our meats the two measures part ways on odd shapes (length overstates the moose leg and the
        // hare, volume overstates meats whose jerky shape is thin, like the serpent), so the size is the geometric mean
        // of the two (tools/hang/meatsize.py; meat_sizes.json, jerky_size.json): 0.24 m Deer and Horse up to 1.1 m
        // Moose, against the game's Boar Jerky 0.34 m and Wolf Jerky 0.51 m. Icons don't change.
        internal static readonly Kind[] Kinds =
        {
            new Kind { Prefab = "PotatoJerky_NeckTail", Meat = "NeckTail", Name = "Neck tail jerky", Shape = "bloodgill", Look = "necktail", Health = 19, Stamina = 19, Regen = 2, Level = 1, Size = 0.80f },
            new Kind { Prefab = "PotatoJerky_Deer", Meat = "DeerMeat", Name = "Deer jerky", Shape = "deer", Look = "deer", Health = 26, Stamina = 26, Regen = 2, Level = 1, Size = 0.57f },
            new Kind { Prefab = "PotatoJerky_Horse", Meat = "rae_HorseMeat", Name = "Horse jerky", Shape = "deer", Look = "horse", Health = 26, Stamina = 26, Regen = 2, Mod = OdinHorse, Level = 1, Size = 0.57f },
            new Kind { Prefab = "PotatoJerky_Grizzly", Meat = "BearSteak_TW", Name = "Grizzly jerky", Shape = "lox", Look = "grizzly", Health = 29, Stamina = 29, Regen = 3, Mod = Monstrum, Level = 1, Size = 0.87f },
            new Kind { Prefab = "PotatoJerky_Bloodgill", Meat = "SharkMeat_TW", Name = "Bloodgill jerky", Shape = "bloodgill", Look = "bloodgill", Health = 30, Stamina = 55, Regen = 3, Mod = Monstrum, Level = 5, Size = 1.80f },
            new Kind { Prefab = "PotatoJerky_Lox", Meat = "LoxMeat", Name = "Lox jerky", Shape = "lox", Look = "lox", Health = 38, Stamina = 38, Regen = 4, Level = 4, Size = 1.10f },
            new Kind { Prefab = "PotatoJerky_Chicken", Meat = "ChickenMeat", Name = "Chicken jerky", Shape = "bloodgill", Look = "chicken", Health = 45, Stamina = 45, Regen = 5, Level = 5, Size = 0.61f },
            new Kind { Prefab = "PotatoJerky_Hare", Meat = "HareMeat", Name = "Hare jerky", Shape = "deer", Look = "hare", Health = 45, Stamina = 45, Regen = 5, Level = 5, Size = 1.37f },
            new Kind { Prefab = "PotatoJerky_Seeker", Meat = "BugMeat", Name = "Seeker jerky", Shape = "lox", Look = "seeker", Health = 45, Stamina = 45, Regen = 5, Level = 5, Size = 1.49f },
            new Kind { Prefab = "PotatoJerky_Serpent", Meat = "SerpentMeat", Name = "Serpent jerky", Shape = "serpent", Look = "serpent", Health = 53, Stamina = 53, Regen = 3, Level = 2, Size = 2.15f },
            new Kind { Prefab = "PotatoJerky_Asksvin", Meat = "AsksvinMeat", Name = "Asksvin jerky", Shape = "deer", Look = "asksvin", Health = 53, Stamina = 53, Regen = 6, Level = 5, Size = 1.21f },
            new Kind { Prefab = "PotatoJerky_Volture", Meat = "VoltureMeat", Name = "Volture jerky", Shape = "bloodgill", Look = "volture", Health = 53, Stamina = 53, Regen = 6, Level = 6, Size = 1.78f },
            new Kind { Prefab = "PotatoJerky_Moose", Meat = "MooseMeat", Name = "Moose jerky", Shape = "lox", Look = "moose", Health = 60, Stamina = 60, Regen = 7, Level = 7, Size = 2.63f },
            new Kind { Prefab = "PotatoJerky_BoneMaw", Meat = "BoneMawSerpentMeat", Name = "Bone Maw jerky", Shape = "serpent", Look = "bonemaw", Health = 68, Stamina = 68, Regen = 6, Level = 6, Size = 2.18f },
        };

        // Jerkies for these mods' meats only exist when the mod is installed (their plugin GUIDs).
        internal const string Monstrum = "Therzie.Monstrum", OdinHorse = "Raelaziel.OdinHorse";
        private const string Description = "Meat dried with honey, on Læraðr's Rack or in the cauldron. A light snack that lasts.";

        internal static readonly Dictionary<string, Kind> ByMeat = new Dictionary<string, Kind>();

        internal static void AddAll()
        {
            var words = new Dictionary<string, string>();
            int added = 0;
            foreach (var kind in Kinds)
            {
                if (kind.Mod != null && !Chainloader.PluginInfos.ContainsKey(kind.Mod)) continue;
                try
                {
                    if (!Add(kind)) continue;
                    ByMeat[kind.Meat] = kind;
                    AddRecipe(kind);
                    words["item_" + kind.Prefab.ToLowerInvariant()] = kind.Name;
                    words["item_" + kind.Prefab.ToLowerInvariant() + "_description"] = Description;
                    added++;
                }
                catch (Exception e) { Plugin.Log.LogError("Couldn't add " + kind.Name + ": " + e); }
            }
            LocalizationManager.Instance.GetLocalization().AddTranslation("English", words);
            Plugin.Log.LogInfo($"{added} jerkies added for Læraðr's Rack.");
        }

        // The cauldron recipe, like the game's Boar and Wolf Jerky: 1 raw meat + 1 honey -> 2 jerky.
        private static void AddRecipe(Kind kind)
        {
            ItemManager.Instance.AddRecipe(new CustomRecipe(new Jotunn.Configs.RecipeConfig
            {
                Name = "Recipe_" + kind.Prefab,
                Item = kind.Prefab,
                Amount = 2,
                CraftingStation = "piece_cauldron",
                MinStationLevel = kind.Level,
                Requirements = new[] { new Jotunn.Configs.RequirementConfig(kind.Meat, 1), new Jotunn.Configs.RequirementConfig("Honey", 1) },
            }));
        }

        private static bool Add(Kind kind)
        {
            var item = new CustomItem(kind.Prefab, "BoarJerky");
            var prefab = item.ItemPrefab;
            if (!prefab)
            {
                Plugin.Log.LogWarning("The game's Boar Jerky wasn't found; no " + kind.Name + " this session.");
                return false;
            }
            var mesh = Assets.LoadMesh("jerkyshape_" + kind.Shape);
            if (!mesh) return false;
            var shared = item.ItemDrop.m_itemData.m_shared;
            string token = "$item_" + kind.Prefab.ToLowerInvariant();
            shared.m_name = token;
            shared.m_description = token + "_description";
            shared.m_food = kind.Health;
            shared.m_foodStamina = kind.Stamina;
            shared.m_foodEitr = 0f;
            shared.m_foodRegen = kind.Regen;
            shared.m_foodBurnTime = 1800f;
            shared.m_weight = 0.5f;
            shared.m_maxStackSize = 20;
            shared.m_consumeStatusEffect = null;
            if (!GUIManager.IsHeadless())
            {
                var icon = LoadSprite("jerky_" + kind.Look + "_icon.png");
                if (icon) shared.m_icons = new[] { icon };
            }
            // The model: the game's jerky keeps its "attach" (scale 0.3) with the mesh under it at scale 3.33, so one
            // model unit is one metre; our meshes are in metres. Every copy of the mesh (the switched-off one under
            // "equipoffset" too) gets ours, with a box to collide with instead of the old mesh collider.
            var material = Material(kind);
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                filter.sharedMesh = mesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer && material) renderer.sharedMaterial = material;
                foreach (var meshCollider in filter.GetComponents<MeshCollider>()) UnityEngine.Object.DestroyImmediate(meshCollider);
                foreach (var box in filter.GetComponents<BoxCollider>()) UnityEngine.Object.DestroyImmediate(box);
                var collider = filter.gameObject.AddComponent<BoxCollider>();
                collider.center = mesh.bounds.center;
                collider.size = mesh.bounds.size;
            }
            // Its size (on the ground and on the rack; the rack reads it from the copy's scale).
            var attach = prefab.transform.Find("attach");
            if (attach) attach.localScale *= kind.Size;
            else Plugin.Log.LogWarning(kind.Name + ": no attach to size.");
            ItemManager.Instance.AddItem(item);
            return true;
        }

        private static Material Material(Kind kind)
        {
            var source = PrefabManager.Cache.GetPrefab<Material>("BoarJerky_material");
            if (!source) return null;
            var material = new Material(source) { name = "potato_jerky_" + kind.Look };
            if (GUIManager.IsHeadless()) return material;
            var texture = LoadTexture("jerky_" + kind.Look + "_d.png");
            if (texture) material.SetTexture("_MainTex", texture);
            // The game's normal map belongs to its own model's layout; ours is drawn without one.
            material.SetTexture("_BumpMap", null);
            material.DisableKeyword("_NORMALMAP");
            return material;
        }

        internal static Texture2D LoadTexture(string file)
        {
            string path = Path.Combine(Assets.Dir, file);
            if (!File.Exists(path))
            {
                Plugin.Log.LogWarning("Missing " + path);
                return null;
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = Path.GetFileNameWithoutExtension(file), wrapMode = TextureWrapMode.Repeat };
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), true)) return null;
            texture.filterMode = FilterMode.Point;
            return texture;
        }

        private static Sprite LoadSprite(string file)
        {
            string path = Path.Combine(Assets.Dir, file);
            if (!File.Exists(path))
            {
                Plugin.Log.LogWarning("Missing " + path);
                return null;
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = Path.GetFileNameWithoutExtension(file), wrapMode = TextureWrapMode.Clamp };
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), true)) return null;
            texture.filterMode = FilterMode.Bilinear;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
