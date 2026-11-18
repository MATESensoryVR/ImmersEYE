// Read-only Editor diagnostic for the food textures in ImmersiveEye Block C and D.
// Install at Assets/Editor/ImmersiveEyeFoodTextureAudit.cs.
// No assets, import settings, materials, scenes or experiment objects are modified.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ImmersiveEyeFoodTextureAudit
{
    private static readonly string[] FoodNames = {
        "White Rice", "Tofu", "StirFry Vege", "Roast Vege", "Potato", "Pasta",
        "Olives", "Nuts", "Fruits", "Fresh Salad", "Fish", "Chicken",
        "Cheese", "Bread", "Beef", "Avocado"
    };

    private sealed class Entry
    {
        public Texture2D texture;
        public readonly HashSet<string> foods = new HashSet<string>();
        public readonly HashSet<string> materials = new HashSet<string>();
    }

    [MenuItem("Tools/ImmersiveEye/Audit Food Textures - Blocks C and D")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject blockC = FindSceneObject(scene, "BlockC_Food Items");
        GameObject blockD = FindSceneObject(scene, "BlockD_Food Items");
        if (blockC == null || blockD == null)
        {
            Debug.LogError("IMMERSIVEEYE_TEXTURE_AUDIT_ERROR: Missing BlockC_Food Items or BlockD_Food Items. Nothing changed.");
            return;
        }

        var entries = new Dictionary<Texture2D, Entry>();
        int foodCount = 0, missingFoodOrRenderer = 0, materialsChecked = 0;
        foreach (var pair in new[] {
            new KeyValuePair<string, GameObject>("C", blockC),
            new KeyValuePair<string, GameObject>("D", blockD) })
        {
            foreach (string foodName in FoodNames)
            {
                Transform food = pair.Value.transform.Find(foodName);
                if (food == null) { missingFoodOrRenderer++; continue; }
                // Food root renderer only: intentionally EXCLUDES nested and separate plates/labels.
                Renderer renderer = food.GetComponent<Renderer>();
                if (renderer == null) { missingFoodOrRenderer++; continue; }
                foodCount++;
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat == null) continue;
                    materialsChecked++;
                    foreach (string property in mat.GetTexturePropertyNames())
                    {
                        Texture2D texture = mat.GetTexture(property) as Texture2D;
                        if (texture == null) continue;
                        Entry entry;
                        if (!entries.TryGetValue(texture, out entry))
                        {
                            entry = new Entry { texture = texture };
                            entries.Add(texture, entry);
                        }
                        entry.foods.Add(pair.Key + "/" + foodName);
                        entry.materials.Add(mat.name);
                    }
                }
            }
        }

        var sorted = entries.Values.OrderByDescending(e => (long)e.texture.width * e.texture.height).ToList();
        int fourK = sorted.Count(e => Mathf.Max(e.texture.width, e.texture.height) >= 4096);
        int twoK = sorted.Count(e => Mathf.Max(e.texture.width, e.texture.height) >= 2048);
        var sb = new StringBuilder();
        sb.AppendLine("=== ImmersiveEye Food Texture Audit (read only) ===");
        sb.AppendLine("Examined FOOD root materials in Blocks C and D, including disabled objects; plates and labels excluded.");
        sb.AppendLine("Android texture settings are import configuration, not measured headset memory or FPS.");
        sb.AppendLine(string.Format("IMMERSIVEEYE_TEXTURE_AUDIT_RESULT|foodObjects={0}|missingFoodOrRenderer={1}|materialSlotsChecked={2}|uniqueTextures={3}|textures2KPlus={4}|textures4KPlus={5}|sceneOrAssetsChanged=false",
            foodCount, missingFoodOrRenderer, materialsChecked, sorted.Count, twoK, fourK));
        sb.AppendLine();
        foreach (Entry entry in sorted)
        {
            Texture2D t = entry.texture;
            string path = AssetDatabase.GetAssetPath(t);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            string android = "unavailable";
            string mipmaps = t.mipmapCount > 1 ? "yes" : "no";
            if (importer != null)
            {
                var settings = importer.GetPlatformTextureSettings("Android");
                android = settings.overridden
                    ? "override max=" + settings.maxTextureSize + ", format=" + settings.format
                    : "default max=" + importer.maxTextureSize + ", compression=" + importer.textureCompression;
                mipmaps = importer.mipmapEnabled ? "yes" : "no";
            }
            sb.AppendLine(string.Format("{0} | {1}x{2} | mipmaps={3} | Android: {4} | usedBy={5} | asset={6}",
                t.name, t.width, t.height, mipmaps, android, entry.foods.Count,
                string.IsNullOrEmpty(path) ? "(embedded/runtime texture)" : path));
        }
        sb.AppendLine();
        sb.AppendLine("No modifications were made. Do not change texture sizes until inspecting this report.");
        string report = sb.ToString();
        Debug.Log(report);
        EditorGUIUtility.systemCopyBuffer = report;
        Debug.Log("ImmersiveEye texture audit report copied to clipboard.");
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == name) return candidate.gameObject;
            }
        }
        return null;
    }
}
