#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMeshSimplifier;

/// <summary>
/// Non-destructive, editor-only batch simplification for the sixteen Block C/D food meshes.
/// The generated Mesh assets replace only MeshFilter.sharedMesh references in scene instances.
/// Food GameObjects, transforms, colliders, materials, AOIs and XR interaction components
/// are not modified. Requires com.whinarn.unitymeshsimplifier (UnityMeshSimplifier).
/// </summary>
public static class ImmersiveEyeFoodMeshOptimizer
{
    private const string OutputRoot = "Assets/ImmersiveEyeGenerated";
    private const string OutputFolder = OutputRoot + "/OptimizedFoodMeshes";

    private static readonly string[] FoodNames =
    {
        "White Rice", "Tofu", "StirFry Vege", "Roast Vege",
        "Potato", "Pasta", "Olives", "Nuts", "Fruits",
        "Fresh Salad", "Fish", "Chicken", "Cheese", "Bread",
        "Beef", "Avocado"
    };

    [MenuItem("Tools/ImmersiveEye/Optimize Food Meshes - Blocks C and D")]
    public static void OpenWindow()
    {
        ImmersiveEyeMeshOptimizerWindow.ShowWindow();
    }

    public static void Optimize(float quality)
    {
        if (!EditorUtility.DisplayDialog(
            "ImmersiveEye: simplify food meshes",
            "This generates NEW mesh assets and reassigns only the 16 food MeshFilters " +
            "in Block C and Block D. Source meshes, food materials, colliders, AOIs, " +
            "XR Interactables, transforms and plate slots are preserved.\n\n" +
            "Save a backup of the scene first. Continue?",
            "Optimize", "Cancel"))
            return;

        Scene scene = SceneManager.GetActiveScene();
        GameObject c = FindSceneObject(scene, "BlockC_Food Items");
        GameObject d = FindSceneObject(scene, "BlockD_Food Items");
        if (c == null || d == null)
        {
            EditorUtility.DisplayDialog("Missing food group",
                "Could not find both BlockC_Food Items and BlockD_Food Items in the active scene. No changes made.",
                "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(OutputRoot))
            AssetDatabase.CreateFolder("Assets", "ImmersiveEyeGenerated");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder(OutputRoot, "OptimizedFoodMeshes");

        int attempted = 0;
        int changed = 0;
        int skipped = 0;
        long originalTriangles = 0;
        long newTriangles = 0;
        var errors = new List<string>();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("ImmersiveEye Optimize Food Meshes");

        try
        {
            foreach (var pair in new[] { new KeyValuePair<string, GameObject>("BlockC", c),
                                           new KeyValuePair<string, GameObject>("BlockD", d) })
            {
                foreach (string foodName in FoodNames)
                {
                    attempted++;
                    string title = pair.Key + " / " + foodName;
                    EditorUtility.DisplayProgressBar("ImmersiveEye food optimization",
                        title, (float)(attempted - 1) / (FoodNames.Length * 2));

                    Transform food = pair.Value.transform.Find(foodName);
                    if (food == null)
                    {
                        errors.Add(title + ": food GameObject not found");
                        skipped++;
                        continue;
                    }
                    MeshFilter filter = food.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null)
                    {
                        errors.Add(title + ": food MeshFilter missing or empty");
                        skipped++;
                        continue;
                    }

                    Mesh original = filter.sharedMesh;
                    if (original.name.StartsWith("ImmersiveEye_FoodLOD_", StringComparison.Ordinal))
                    {
                        skipped++;
                        continue;
                    }

                    int oldCount = TriangleCount(original);
                    try
                    {
                        // Initialize transfers UVs, normals and submesh/material topology.
                        var simplifier = new MeshSimplifier();
                        simplifier.Initialize(original);
                        // Preserve UV seams/foldovers to reduce texture artifacts.
                        var options = simplifier.SimplificationOptions;
                        options.PreserveUVSeamEdges = true;
                        options.PreserveUVFoldoverEdges = true;
                        options.PreserveBorderEdges = true;
                        simplifier.SimplificationOptions = options;
                        simplifier.SimplifyMesh(quality);

                        Mesh result = simplifier.ToMesh();
                        result.name = "ImmersiveEye_FoodLOD_" + pair.Key + "_" + SafeName(foodName);
                        result.RecalculateBounds();
                        int afterCount = TriangleCount(result);
                        if (afterCount <= 0 || afterCount >= oldCount)
                        {
                            UnityEngine.Object.DestroyImmediate(result);
                            errors.Add(title + ": mesh not reduced (" + oldCount + " -> " + afterCount + ")");
                            skipped++;
                            continue;
                        }
                        if (result.subMeshCount != original.subMeshCount)
                        {
                            UnityEngine.Object.DestroyImmediate(result);
                            errors.Add(title + ": submesh mismatch, skipped for material safety");
                            skipped++;
                            continue;
                        }

                        string path = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/" + result.name + ".asset");
                        AssetDatabase.CreateAsset(result, path);
                        Undo.RecordObject(filter, "Simplify " + title);
                        filter.sharedMesh = result;
                        EditorUtility.SetDirty(filter);
                        if (PrefabUtility.IsPartOfPrefabInstance(filter))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                        originalTriangles += oldCount;
                        newTriangles += afterCount;
                        changed++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add(title + ": " + ex.Message);
                        skipped++;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            if (changed > 0)
                EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
        }

        string summary = "IMMERSIVEEYE_FOOD_MESH_RESULT|attempted=" + attempted +
                         "|changed=" + changed + "|skipped=" + skipped +
                         "|oldTriangles=" + originalTriangles + "|newTriangles=" + newTriangles +
                         "|quality=" + quality.ToString("F2") +
                         "|originalMeshesUnchanged=true|saveScene=true";
        Debug.Log(summary);
        foreach (string error in errors) Debug.LogWarning("IMMERSIVEEYE_FOOD_MESH_SKIP: " + error);
        EditorUtility.DisplayDialog("Food simplification finished",
            summary + (errors.Count > 0 ? "\n\nCheck Console warnings." : "\n\nInspect all foods before saving."),
            "OK");
    }

    private static int TriangleCount(Mesh mesh)
    {
        int tris = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
            if (mesh.GetTopology(i) == MeshTopology.Triangles)
                tris += (int)mesh.GetIndexCount(i) / 3;
        return tris;
    }

    private static string SafeName(string text)
    {
        return text.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
        }
        return null;
    }
}

public class ImmersiveEyeMeshOptimizerWindow : EditorWindow
{
    private float quality = 0.25f;

    public static void ShowWindow()
    {
        var win = GetWindow<ImmersiveEyeMeshOptimizerWindow>(true,
            "ImmersiveEye food optimization", true);
        win.minSize = new Vector2(390, 170);
        win.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Optimize all 16 foods in Blocks C and D", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Runs on food meshes only, not the optimized plates. Preserves each GameObject, material, collider, AOI, XR interactable and transform. The originals remain untouched.\n\n" +
            "Save a scene backup first. Lower quality means fewer triangles and more visual change.",
            MessageType.Info);
        quality = EditorGUILayout.Slider("Mesh quality", quality, 0.10f, 0.70f);
        if (GUILayout.Button("Optimize food meshes (both blocks)", GUILayout.Height(35)))
            ImmersiveEyeFoodMeshOptimizer.Optimize(quality);
    }
}
#endif
