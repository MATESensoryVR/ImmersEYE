#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Non-destructive optimization for repeated ceramic plate meshes under
/// BlockC_Food Items. Keeps imported USDZ and original Mesh assets untouched.
/// Place this file under Assets/Editor/.
/// </summary>
public static class ImmersiveEyePlateOptimizer
{
    private const string Folder = "Assets/ImmersiveEyeOptimized";
    private const string MeshPath = Folder + "/LowPolyPlate.asset";
    private const int Segments = 72;

    [MenuItem("Tools/ImmersiveEye/Replace Block C Plates With Low-Poly Mesh")]
    private static void OptimizePlates()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null || root.name != "BlockC_Food Items")
        {
            EditorUtility.DisplayDialog("Select Block C Food Items",
                "Select the BlockC_Food Items GameObject in the Hierarchy first. No changes were made.", "OK");
            return;
        }

        MeshFilter[] allFilters = root.GetComponentsInChildren<MeshFilter>(true);
        var candidates = new List<MeshFilter>();
        foreach (MeshFilter filter in allFilters)
        {
            if (filter == null || filter.sharedMesh == null) continue;
            // Only components on plate-named objects; never touch any food mesh.
            if (!filter.gameObject.name.StartsWith("Plate", StringComparison.OrdinalIgnoreCase)) continue;
            if (filter.sharedMesh.triangles.Length / 3 < 10000) continue;
            candidates.Add(filter);
        }

        if (candidates.Count == 0)
        {
            EditorUtility.DisplayDialog("No heavy plates found",
                "No high-poly plate MeshFilters were found below the selected object. No changes were made.", "OK");
            return;
        }

        bool proceed = EditorUtility.DisplayDialog("Optimize Block C plates?",
            $"Found {candidates.Count} heavy plate mesh(es). This replaces only their MeshFilter references " +
            "with light, plate-shaped meshes. Original USDZ files, transforms, materials, food meshes and AOIs stay unchanged. " +
            "You can undo this before closing Unity. Save a scene backup first.",
            "Replace Plates", "Cancel");
        if (!proceed) return;

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "ImmersiveEyeOptimized");

        // Group by source mesh bounds. This keeps original local positions,
        // sizes, and all existing Transform values intact even if assets differ.
        var replacementByKey = new Dictionary<string, Mesh>();
        int changed = 0;
        long oldTriangles = 0;
        long newTriangles = 0;
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Optimize Block C Plate Meshes");

        foreach (MeshFilter filter in candidates)
        {
            Mesh original = filter.sharedMesh;
            Bounds bounds = original.bounds;
            string key = GetBoundsKey(bounds);

            if (!replacementByKey.TryGetValue(key, out Mesh lowPoly))
            {
                lowPoly = CreatePlateMesh(bounds, Segments);
                string path = replacementByKey.Count == 0 ? MeshPath :
                    $"{Folder}/LowPolyPlate_{replacementByKey.Count + 1}.asset";
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                AssetDatabase.CreateAsset(lowPoly, path);
                replacementByKey.Add(key, lowPoly);
            }

            oldTriangles += original.triangles.Length / 3;
            newTriangles += lowPoly.triangles.Length / 3;

            Undo.RecordObject(filter, "Replace high-poly plate mesh");
            filter.sharedMesh = lowPoly;
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            EditorUtility.SetDirty(filter);
            changed++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"IMMERSIVEEYE_PLATES_OPTIMIZED: replaced={changed}, " +
                  $"oldTriangles={oldTriangles:N0}, newTriangles={newTriangles:N0}. " +
                  "Original USDZ assets and food meshes were not changed. " +
                  "Check the Scene view before saving the scene.", root);
        EditorUtility.DisplayDialog("Block C plate meshes replaced",
            $"Updated {changed} plate MeshFilter(s).\n" +
            $"Plate triangles: {oldTriangles:N0} -> {newTriangles:N0}.\n\n" +
            "Check how the plates look in Scene view. If unsatisfactory, use Edit > Undo immediately. " +
            "The original Plate.usdz file was not modified.", "OK");
    }

    [MenuItem("Tools/ImmersiveEye/Replace Block C Plates With Low-Poly Mesh", true)]
    private static bool CanOptimize() => !EditorApplication.isPlayingOrWillChangePlaymode;

    private static string GetBoundsKey(Bounds b) =>
        $"{b.center.x:F4}_{b.center.y:F4}_{b.center.z:F4}_{b.size.x:F4}_{b.size.y:F4}_{b.size.z:F4}";

    private static Mesh CreatePlateMesh(Bounds b, int segments)
    {
        // Radial cross-section: shallow food well, curved lip, underside/foot.
        // Sizes and pivot are mapped to the original imported mesh bounds.
        float[] radii = { 0f, 0.35f, 0.71f, 0.84f, 0.94f, 1f, 0.97f, 0.83f, 0.44f, 0.28f, 0f };
        float[] heights = { 0.54f, 0.54f, 0.57f, 0.68f, 0.94f, 1f, 0.50f, 0.20f, 0.13f, 0f, 0.05f };
        int rings = radii.Length;
        int stride = segments + 1;
        Vector3[] vertices = new Vector3[rings * stride];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[(rings - 1) * segments * 6];
        float radiusX = Mathf.Max(b.extents.x, 0.0001f);
        float radiusZ = Mathf.Max(b.extents.z, 0.0001f);
        float height = Mathf.Max(b.size.y, 0.001f);

        for (int ring = 0; ring < rings; ring++)
        {
            float r = radii[ring];
            float y = b.min.y + heights[ring] * height;
            for (int side = 0; side <= segments; side++)
            {
                float angle = side * Mathf.PI * 2f / segments;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);
                int index = ring * stride + side;
                vertices[index] = new Vector3(
                    b.center.x + cosine * radiusX * r,
                    y,
                    b.center.z + sine * radiusZ * r);
                uv[index] = new Vector2(0.5f + cosine * r * 0.5f,
                                         0.5f + sine * r * 0.5f);
            }
        }

        int t = 0;
        for (int ring = 0; ring < rings - 1; ring++)
        {
            for (int side = 0; side < segments; side++)
            {
                int a = ring * stride + side;
                int b0 = a + 1;
                int c = (ring + 1) * stride + side;
                int d = c + 1;
                // Counter-clockwise viewed from above on the upper surface.
                triangles[t++] = a;
                triangles[t++] = b0;
                triangles[t++] = c;
                triangles[t++] = b0;
                triangles[t++] = d;
                triangles[t++] = c;
            }
        }
        Mesh mesh = new Mesh { name = "ImmersiveEye_LowPolyPlate" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
#endif
