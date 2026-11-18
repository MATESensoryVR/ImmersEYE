// ImmersiveEyeMetallicTextureBatch.cs
// Unity 6.x Editor-only utility. Place in Assets/Editor.
// Optimizes only the 16 embedded 4K metallic-roughness masks used by the
// 32 food renderers in Block C and D. Never edits source .glb files.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ImmersiveEyeMetallicTextureBatch
{
    private const int NewSize = 1024;
    private const string OutputFolder = "Assets/ImmersiveEyeGenerated/FoodSurfaceMaps1024";

    private static readonly HashSet<string> FoodNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "White Rice", "Tofu", "StirFry Vege", "Roast Vege", "Potato", "Pasta",
        "Olives", "Nuts", "Fruits", "Fresh Salad", "Fish", "Chicken",
        "Cheese", "Bread", "Beef", "Avocado"
    };

    private sealed class FoodJob
    {
        public MeshRenderer renderer;
        public Material originalMaterial;
        public string textureProperty;
        public Texture2D originalTexture;
        public string foodName;
    }

    [MenuItem("Tools/ImmersiveEye/Optimize 4K Food Surface Textures - C and D")]
    public static void Optimize()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("IMMERSIVEEYE_SURFACE_TEXTURE_ERROR: Save your Unity scene first.");
            return;
        }

        var jobs = new List<FoodJob>();
        int groupsFound = 0;
        foreach (string groupName in new[] { "BlockC_Food Items", "BlockD_Food Items" })
        {
            Transform foodGroup = FindInScene(scene, groupName);
            if (foodGroup == null)
            {
                Debug.LogError("IMMERSIVEEYE_SURFACE_TEXTURE_ERROR: Missing scene group: " + groupName);
                return;
            }
            groupsFound++;
            foreach (Transform child in foodGroup)
            {
                if (!FoodNames.Contains(child.name)) continue;
                MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    Debug.LogError("IMMERSIVEEYE_SURFACE_TEXTURE_ERROR: Missing root MeshRenderer on " + child.name);
                    return;
                }
                Material[] mats = renderer.sharedMaterials;
                if (mats.Length != 1 || mats[0] == null)
                {
                    Debug.LogError("IMMERSIVEEYE_SURFACE_TEXTURE_ERROR: Expected one root material on " + child.name + ", found " + mats.Length);
                    return;
                }

                Material mat = mats[0];
                string foundProperty = null;
                Texture2D foundTexture = null;
                foreach (string property in mat.GetTexturePropertyNames())
                {
                    Texture2D tex = mat.GetTexture(property) as Texture2D;
                    if (tex == null) continue;
                    if (tex.width < 4096 || tex.height < 4096) continue;
                    if (tex.name.IndexOf("metallic_roughness", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (foundTexture != null)
                    {
                        Debug.LogError("IMMERSIVEEYE_SURFACE_TEXTURE_ERROR: Ambiguous metallic textures on " + child.name);
                        return;
                    }
                    foundProperty = property;
                    foundTexture = tex;
                }
                if (foundTexture == null)
                {
                    Debug.LogError("IMMERSIVEEYE_SURFACE_TEXTURE_ERROR: 4K metallic-roughness map not found on " + groupName + "/" + child.name + ". No changes made.");
                    return;
                }
                jobs.Add(new FoodJob { renderer = renderer, originalMaterial = mat,
                    textureProperty = foundProperty, originalTexture = foundTexture, foodName = child.name });
            }
        }

        var uniqueTextures = new HashSet<Texture2D>();
        foreach (FoodJob job in jobs) uniqueTextures.Add(job.originalTexture);
        // Fail safely rather than modifying unexpected models/materials.
        if (groupsFound != 2 || jobs.Count != 32 || uniqueTextures.Count != 16)
        {
            Debug.LogError("IMMERSIVEEYE_SURFACE_TEXTURE_ERROR: Expected 32 foods / 16 unique 4K masks; found foods=" + jobs.Count + ", uniqueMasks=" + uniqueTextures.Count + ". No changes made.");
            return;
        }

        if (!EditorUtility.DisplayDialog("ImmersiveEye: optimize surface maps",
            "Found 32 food renderers sharing 16 unique 4K metallic-roughness maps.\n\n" +
            "Create 1024px mipmapped, Android ASTC-compressed copies, and use material copies in Blocks C and D?\n\n" +
            "The original GLB files and material assets will not be edited. Save a backup of the scene before continuing.",
            "Optimize 16 maps", "Cancel")) return;

        EnsureFolder("Assets/ImmersiveEyeGenerated");
        EnsureFolder(OutputFolder);
        EnsureFolder(OutputFolder + "/Textures");
        EnsureFolder(OutputFolder + "/Materials");

        var textureCopies = new Dictionary<Texture2D, Texture2D>();
        var materialCopies = new Dictionary<Material, Material>();
        int changed = 0;
        try
        {
            // Complete source texture generation before changing any scene renderer.
            foreach (FoodJob job in jobs)
            {
                if (textureCopies.ContainsKey(job.originalTexture)) continue;
                string basename = SafeName(job.foodName) + "_metallic_roughness_1024";
                string path = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/Textures/" + basename + ".png");
                byte[] png = DownsampleMaskToPng(job.originalTexture);
                string absolutePath = Path.Combine(Application.dataPath, path.Substring("Assets/".Length));
                File.WriteAllBytes(absolutePath, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new Exception("No TextureImporter for " + path);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false; // Metallic/roughness data is NOT a color image.
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = job.originalTexture.wrapMode;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Android", overridden = true, maxTextureSize = NewSize,
                    format = TextureImporterFormat.ASTC_6x6, compressionQuality = 75
                });
                importer.SaveAndReimport();
                Texture2D optimized = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (optimized == null) throw new Exception("Failed to load generated texture: " + path);
                textureCopies.Add(job.originalTexture, optimized);
            }

            foreach (FoodJob job in jobs)
            {
                if (!materialCopies.TryGetValue(job.originalMaterial, out Material matCopy))
                {
                    matCopy = new Material(job.originalMaterial);
                    matCopy.name = "IE_" + SafeName(job.foodName) + "_Surface1024";
                    matCopy.SetTexture(job.textureProperty, textureCopies[job.originalTexture]);
                    string path = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/Materials/" + matCopy.name + ".mat");
                    AssetDatabase.CreateAsset(matCopy, path);
                    materialCopies.Add(job.originalMaterial, matCopy);
                }
                Undo.RecordObject(job.renderer, "Optimize ImmersiveEye 4K Surface Maps");
                job.renderer.sharedMaterial = matCopy;
                EditorUtility.SetDirty(job.renderer);
                changed++;
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("IMMERSIVEEYE_SURFACE_TEXTURE_OPT_RESULT|foodRenderersChanged=" + changed +
                "|uniqueMasksOptimized=" + textureCopies.Count +
                "|oldSize=4096|newSize=1024|mipmaps=true|androidFormat=ASTC_6x6" +
                "|colourAndNormalTexturesUntouched=true|sourceGLBsUnchanged=true|saveScene=true");
        }
        catch (Exception ex)
        {
            Debug.LogError("IMMERSIVEEYE_SURFACE_TEXTURE_ERROR: " + ex.Message +
                "\nCheck scene before saving. Original GLBs have not been changed.");
        }
    }

    private static Transform FindInScene(Scene scene, string wanted)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == wanted) return t;
        }
        return null;
    }

    private static byte[] DownsampleMaskToPng(Texture2D original)
    {
        RenderTexture rt = RenderTexture.GetTemporary(NewSize, NewSize, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture previous = RenderTexture.active;
        Texture2D readable = null;
        try
        {
            Graphics.Blit(original, rt);
            RenderTexture.active = rt;
            readable = new Texture2D(NewSize, NewSize, TextureFormat.RGBA32, false, true);
            readable.ReadPixels(new Rect(0, 0, NewSize, NewSize), 0, 0, false);
            readable.Apply(false, false);
            byte[] bytes = readable.EncodeToPNG();
            if (bytes == null || bytes.Length == 0) throw new Exception("Empty PNG generated for " + original.name);
            return bytes;
        }
        finally
        {
            RenderTexture.active = previous;
            if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private static string SafeName(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = folder.Substring(0, folder.LastIndexOf('/'));
        string name = folder.Substring(folder.LastIndexOf('/') + 1);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
