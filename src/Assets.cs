// Models and textures for the custom pieces, loaded from the assets folder beside the DLL (no Unity asset bundle).
// tools/convert.py makes them from a .glb: <name>.mesh, <name>_d.png (color), <name>_n.png (normal), <name>_m.png (metal),
// already at the Potato look's size (about 50 pixels per metre, like Valheim's own pieces).
using System.IO;
using Jotunn.Managers;
using UnityEngine;

namespace PotatoFarmer
{
    internal static class Assets
    {
        internal static string Dir;

        internal static Mesh LoadMesh(string name, float size = 1f)
        {
            string path = Path.Combine(Dir, name + ".mesh");
            if (!File.Exists(path))
            {
                Plugin.Log.LogError("Missing " + path);
                return null;
            }
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (new string(reader.ReadChars(4)) != "PTM1")
                {
                    Plugin.Log.LogError(path + " isn't a model file from convert.py.");
                    return null;
                }
                int vertices = reader.ReadInt32(), indices = reader.ReadInt32();
                var positions = new Vector3[vertices];
                var normals = new Vector3[vertices];
                var uvs = new Vector2[vertices];
                var triangles = new int[indices];
                for (int i = 0; i < vertices; i++) positions[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()) * size;
                for (int i = 0; i < vertices; i++) normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                for (int i = 0; i < vertices; i++) uvs[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                for (int i = 0; i < indices; i++) triangles[i] = reader.ReadInt32();
                var mesh = new Mesh { name = "potato_" + name };
                if (vertices > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = positions;
                mesh.normals = normals;
                mesh.uv = uvs;
                mesh.triangles = triangles;
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }

    // One custom piece's material: a copy of a vanilla piece's material (the game's piece shader, wood with metal) with
    // the piece's own textures, drawn with sharp, unblended pixels like Valheim's own pieces. The template decides how
    // the metal looks: "ironchest" (reinforced chest) for plain iron and bronze; "BlackMetalChest_mat" for black metal,
    // whose material tints the metal green and lightens wood (its own wood texture is nearly black). Its value noise
    // mottles baked textures, so pieces using it turn that off.
    internal sealed class PieceLook
    {
        internal readonly Material Material;

        // Brightness tints the whole model (1 = as painted); shine is how glossy the metal parts are (0 = dull, below 0 =
        // keep the template's own).
        internal PieceLook(string name, float brightness, float shine, string templateName = "ironchest", Color? metalColor = null, float valueNoise = -1f)
        {
            var template = PrefabManager.Cache.GetPrefab<Material>(templateName);
            Material = template ? new Material(template) : new Material(Shader.Find("Custom/Piece") ?? Shader.Find("Standard"));
            if (!template) Plugin.Log.LogWarning($"The material {templateName} wasn't found; using a plain one.");
            Material.name = "potato_" + name;
            if (Material.HasProperty("_Metallic")) Material.SetFloat("_Metallic", 1f);
            if (Material.HasProperty("_BumpScale")) Material.SetFloat("_BumpScale", 1f);
            if (Material.HasProperty("_Color")) Material.SetColor("_Color", new Color(brightness, brightness, brightness, 1f));
            if (shine >= 0f && Material.HasProperty("_MetallicAlphaGloss")) Material.SetFloat("_MetallicAlphaGloss", shine);
            if (shine >= 0f && Material.HasProperty("_GlossMapScale")) Material.SetFloat("_GlossMapScale", shine);
            // The shader's tint for the metal parts only (black metal pieces use a strong green).
            if (metalColor.HasValue && Material.HasProperty("_MetalColor")) Material.SetColor("_MetalColor", metalColor.Value);
            // The shader's light/dark blotches across the surface (0 = none). Baked AI textures already vary, so the
            // template's noise on top makes them look mottled.
            if (valueNoise >= 0f && Material.HasProperty("_ValueNoise")) Material.SetFloat("_ValueNoise", valueNoise);
            if (valueNoise >= 0f && Material.HasProperty("_ValueNoiseVertex")) Material.SetFloat("_ValueNoiseVertex", 0f);
            if (GUIManager.IsHeadless()) return;
            SetTexture("_MainTex", name + "_d.png", false);
            SetTexture("_BumpMap", name + "_n.png", true);
            var metal = SetTexture("_MetallicTex", name + "_m.png", true);
            if (metal && Material.HasProperty("_MetallicGlossMap")) Material.SetTexture("_MetallicGlossMap", metal);
        }

        private Texture2D SetTexture(string property, string file, bool linear)
        {
            if (!Material.HasProperty(property)) return null;
            string path = Path.Combine(Assets.Dir, file);
            if (!File.Exists(path))
            {
                Plugin.Log.LogWarning("Missing " + path);
                return null;
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear)
            {
                name = Path.GetFileNameWithoutExtension(file),
                wrapMode = TextureWrapMode.Repeat,
            };
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), true))
            {
                Plugin.Log.LogWarning("Couldn't read " + path);
                return null;
            }
            texture.filterMode = FilterMode.Point;
            Material.SetTexture(property, texture);
            return texture;
        }
    }
}
