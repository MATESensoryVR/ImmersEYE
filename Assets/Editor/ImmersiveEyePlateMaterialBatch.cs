#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Applies one clean ceramic-white URP material to every *optimized* plate
/// under the selected BlockC_Food Items scene object.
/// Does not touch food meshes, food materials, AOIs, transforms, or USDZ assets.
/// Install under Assets/Editor/. Use Tools > ImmersiveEye > Fix All Block C Plate Materials.
/// </summary>
public static class ImmersiveEyePlateMaterialBatch
{
    private const string MaterialDirectory = "Assets/ImmersiveEyeOptimized";
    private const string MaterialPath = MaterialDirectory + "/ImmersiveEye_PlateWhite.mat";
    private const string LowPolyMeshPrefix = "ImmersiveEye_LowPolyPlate";

    [MenuItem("Tools/ImmersiveEye/Fix All Block C Plate Materials")]
    private static void Apply()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null || root.name != "BlockC_Food Items" || !root.scene.IsValid())
        {
            EditorUtility.DisplayDialog("Select the Block C foods",
                "Select the scene object BlockC_Food Items in the Hierarchy first. No changes were made.", "OK");
            return;
        }

        var targets = new List<MeshRenderer>();
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null || filter.sharedMesh == null) continue;
            // Only plates modified by the prior low-poly plate optimizer.
            if (!filter.sharedMesh.name.StartsWith(LowPolyMeshPrefix, StringComparison.Ordinal)) continue;
            if (!filter.gameObject.name.StartsWith("Plate", StringComparison.OrdinalIgnoreCase)) continue;
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer != null) targets.Add(renderer);
        }

        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("No optimized plates found",
                "No optimized plate meshes were found under BlockC_Food Items. " +
                "This tool intentionally leaves original plates and food objects unchanged.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Fix all Block C plates?",
            $"Found {targets.Count} optimized plate(s). Apply one plain ceramic-white material to all of them?\n\n" +
            "Food meshes, AOIs, transforms, original prefabs/USDZ assets and Block D will not change. " +
            "Scene overrides can be undone using Edit > Undo.",
            "Apply to All", "Cancel")) return;

        if (!AssetDatabase.IsValidFolder(MaterialDirectory))
            AssetDatabase.CreateFolder("Assets", "ImmersiveEyeOptimized");

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            EditorUtility.DisplayDialog("URP Lit shader unavailable",
                "Could not locate 'Universal Render Pipeline/Lit'. " +
                "No materials have been changed. Check that URP is installed.", "OK");
            return;
        }

        Material ceramic = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (ceramic == null)
        {
            ceramic = new Material(shader) { name = "ImmersiveEye_PlateWhite" };
            AssetDatabase.CreateAsset(ceramic, MaterialPath);
        }
        Undo.RecordObject(ceramic, "Set clean ceramic plate material");
        ceramic.shader = shader;
        if (ceramic.HasProperty("_BaseColor"))
            ceramic.SetColor("_BaseColor", new Color(0.97f, 0.965f, 0.95f, 1f));
        if (ceramic.HasProperty("_BaseMap"))
            ceramic.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (ceramic.HasProperty("_Metallic")) ceramic.SetFloat("_Metallic", 0f);
        if (ceramic.HasProperty("_Smoothness")) ceramic.SetFloat("_Smoothness", 0.45f);
        EditorUtility.SetDirty(ceramic);

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Apply white material to all Block C plates");
        foreach (MeshRenderer renderer in targets)
        {
            Undo.RecordObject(renderer, "Change optimized plate material");
            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                materials = new Material[1];
            materials[0] = ceramic;
            renderer.sharedMaterials = materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
        }
        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"IMMERSIVEEYE_PLATES_MATERIAL_FIXED: plates={targets.Count}, " +
                  $"material={MaterialPath}, food meshes unchanged.", root);
        EditorUtility.DisplayDialog("All optimized Block C plates updated",
            $"Applied the ceramic-white material to {targets.Count} plate(s).\n\n" +
            "Check the table in Scene view before saving. The original USDZ files are untouched.", "OK");
    }

    [MenuItem("Tools/ImmersiveEye/Fix All Block C Plate Materials", true)]
    private static bool Validate() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
#endif
