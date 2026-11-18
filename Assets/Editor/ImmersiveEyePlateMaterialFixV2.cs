// Editor-only tool. Save this file at Assets/Editor/ImmersiveEyePlateMaterialFixV2.cs.
// Changes only the scene instances underneath the selected BlockC_Food Items.
// Does not modify source USDZ models or prefab assets.
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ImmersiveEyePlateMaterialFixV2
{
    private const string MaterialFolder = "Assets/ImmersiveEyeGenerated/Materials";
    private const string MaterialPath = MaterialFolder + "/ImmersiveEye_PlateWhite.mat";

    [MenuItem("Tools/ImmersiveEye/FORCE White Material on Selected Block C Plates")]
    private static void FixScenePlates()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null || root.name != "BlockC_Food Items" ||
            EditorUtility.IsPersistent(root) || !root.scene.IsValid())
        {
            EditorUtility.DisplayDialog("Select Block C Food Items",
                "In the Hierarchy (not the Project window), select the scene object 'BlockC_Food Items', then run this command again.",
                "OK");
            return;
        }

        MeshRenderer[] allRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
        int candidates = 0;
        foreach (MeshRenderer renderer in allRenderers)
            if (renderer != null && IsPlateName(renderer.gameObject.name))
                candidates++;

        if (candidates == 0)
        {
            Debug.LogError("IMMERSIVEEYE_PLATE_MATERIAL: No plate MeshRenderers found under " + root.name);
            EditorUtility.DisplayDialog("No plates found",
                "No plate MeshRenderers were found beneath your selection. Nothing changed.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Fix Block C plate materials",
            "Found " + candidates + " plate MeshRenderers (including inactive children).\n\n" +
            "Apply a white ceramic URP material to their SCENE instances? Food meshes and source prefab/USDZ assets will not change.",
            "Apply to all", "Cancel"))
            return;

        Material whiteMaterial = GetOrCreateWhiteMaterial();
        if (whiteMaterial == null) return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("ImmersiveEye - Fix all Block C plate materials");

        int changed = 0;
        int alreadyCorrect = 0;
        int failed = 0;
        foreach (MeshRenderer renderer in allRenderers)
        {
            if (renderer == null || !IsPlateName(renderer.gameObject.name))
                continue;

            Material[] assigned = renderer.sharedMaterials;
            if (assigned == null || assigned.Length == 0)
            {
                Debug.LogWarning("IMMERSIVEEYE_PLATE_MATERIAL: Plate has no material slot: " + GetPath(renderer.transform));
                failed++;
                continue;
            }
            if (assigned[0] == whiteMaterial)
            {
                alreadyCorrect++;
                continue;
            }

            // Setting a SCENE-instance renderer, not the prefab source.
            Undo.RecordObject(renderer, "Set white ceramic plate material");
            assigned[0] = whiteMaterial;
            renderer.sharedMaterials = assigned;

            // Essential for nested prefab-instance overrides to survive scene reloads.
            if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);

            if (renderer.sharedMaterials.Length > 0 &&
                renderer.sharedMaterials[0] == whiteMaterial)
            {
                changed++;
            }
            else
            {
                failed++;
                Debug.LogError("IMMERSIVEEYE_PLATE_MATERIAL: Assignment did not stick on " + GetPath(renderer.transform));
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        if (changed > 0)
            EditorSceneManager.MarkSceneDirty(root.scene);

        AssetDatabase.SaveAssets();
        string report = "IMMERSIVEEYE_PLATE_MATERIAL_RESULT: " +
                        "found=" + candidates + ", changed=" + changed +
                        ", alreadyWhite=" + alreadyCorrect + ", failed=" + failed +
                        ", material=" + MaterialPath +
                        ". Inspect White Rice > Plate (1) > Mesh Renderer > Element 0, then save scene (Cmd+S).";
        Debug.Log(report);
        EditorUtility.DisplayDialog("Block C plates checked",
            "Plate renderers found: " + candidates + "\n" +
            "Materials changed: " + changed + "\n" +
            "Already correct: " + alreadyCorrect + "\n" +
            "Failed: " + failed + "\n\n" +
            "Verify White Rice > Plate (1) > Mesh Renderer > Element 0.\n" +
            "If it looks right, save the scene with Cmd+S.", "OK");
    }

    [MenuItem("Tools/ImmersiveEye/FORCE White Material on Selected Block C Plates", true)]
    private static bool ValidateFixScenePlates()
    {
        return !Application.isPlaying;
    }

    private static bool IsPlateName(string name)
    {
        return string.Equals(name, "Plate", StringComparison.Ordinal) ||
               name.StartsWith("Plate (", StringComparison.Ordinal);
    }

    private static Material GetOrCreateWhiteMaterial()
    {
        if (!AssetDatabase.IsValidFolder("Assets/ImmersiveEyeGenerated"))
            AssetDatabase.CreateFolder("Assets", "ImmersiveEyeGenerated");
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/ImmersiveEyeGenerated", "Materials");

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("IMMERSIVEEYE_PLATE_MATERIAL: URP/Lit shader not found. No materials changed.");
                EditorUtility.DisplayDialog("URP shader missing", "Universal Render Pipeline/Lit shader not found. No plates were changed.", "OK");
                return null;
            }
            mat = new Material(shader) { name = "ImmersiveEye_PlateWhite" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
        }

        // Keep the plate genuinely white; do not reuse the baked USDZ atlas.
        Undo.RecordObject(mat, "Set ceramic plate appearance");
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.97f, 0.97f, 0.95f, 1f));
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.45f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
