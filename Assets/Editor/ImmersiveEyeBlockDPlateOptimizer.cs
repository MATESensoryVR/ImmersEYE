#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only utility to replace the 17 heavy Block D plate meshes with
/// the already-approved lightweight Block C plate mesh and white material.
/// This changes MeshFilter.sharedMesh and MeshRenderer.sharedMaterial on
/// individual scene objects, NOT their Transforms, XR components, or prefabs.
/// </summary>
public static class ImmersiveEyeBlockDPlateOptimizer
{
    private const string SourceGroupName = "BlockC_Food Items";
    private const string TargetGroupName = "BlockD_Food Items";
    private const string KnownMaterialPath =
        "Assets/ImmersiveEyeGenerated/Materials/ImmersiveEye_PlateWhite.mat";
    private const string GeneratedMeshPath =
        "Assets/ImmersiveEyeGenerated/Meshes/ImmersiveEye_LowPolyPlate_FromBlockC.asset";

    [MenuItem("Tools/ImmersiveEye/Optimize Block D Plates Using Block C")]
    private static void OptimizeBlockDPlates()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("ImmersiveEye", "Open the VRET scene first.", "OK");
            return;
        }

        Transform sourceGroup = FindInScene(scene, SourceGroupName);
        Transform targetGroup = FindInScene(scene, TargetGroupName);
        if (sourceGroup == null || targetGroup == null)
        {
            EditorUtility.DisplayDialog("ImmersiveEye",
                "Could not find BlockC_Food Items or BlockD_Food Items in the active scene. No changes made.", "OK");
            return;
        }

        // Block C's main Plate is the same USDZ plate already converted to low-poly.
        Transform sourcePlate = sourceGroup.Find("Plate");
        if (sourcePlate == null)
        {
            // Alternative: its optimized plate child beneath White Rice.
            Transform rice = sourceGroup.Find("White Rice");
            if (rice != null) sourcePlate = rice.Find("Plate (1)");
        }

        MeshFilter sourceFilter = sourcePlate != null ? sourcePlate.GetComponent<MeshFilter>() : null;
        if (sourceFilter == null || sourceFilter.sharedMesh == null)
        {
            EditorUtility.DisplayDialog("ImmersiveEye",
                "No optimized plate mesh found in Block C. No changes made.", "OK");
            return;
        }

        Mesh sourceMesh = sourceFilter.sharedMesh;
        long newTris = TriangleCount(sourceMesh);
        if (newTris < 1 || newTris > 10000)
        {
            EditorUtility.DisplayDialog("ImmersiveEye",
                "The Block C source plate appears unoptimized (" + newTris +
                " triangles). No Block D changes were made.", "OK");
            return;
        }

        Material white = AssetDatabase.LoadAssetAtPath<Material>(KnownMaterialPath);
        if (white == null)
        {
            MeshRenderer sourceRenderer = sourcePlate.GetComponent<MeshRenderer>();
            if (sourceRenderer != null && sourceRenderer.sharedMaterial != null &&
                sourceRenderer.sharedMaterial.name.IndexOf("PlateWhite", StringComparison.OrdinalIgnoreCase) >= 0)
                white = sourceRenderer.sharedMaterial;
        }
        if (white == null)
        {
            EditorUtility.DisplayDialog("ImmersiveEye",
                "Could not find the previously approved white plate material. No changes made.", "OK");
            return;
        }

        Transform[] plateObjects = targetGroup.Cast<Transform>()
            .Where(t => t.name == "Plate" || t.name.StartsWith("Plate_", StringComparison.Ordinal))
            .ToArray();
        if (plateObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("ImmersiveEye", "No Block D plates found. No changes made.", "OK");
            return;
        }

        foreach (Transform plate in plateObjects)
        {
            if (plate.GetComponent<MeshFilter>() == null || plate.GetComponent<MeshRenderer>() == null)
            {
                EditorUtility.DisplayDialog("ImmersiveEye",
                    "Plate '" + plate.name + "' lacks a Mesh Filter or Mesh Renderer. No changes made.", "OK");
                return;
            }
        }

        if (!EditorUtility.DisplayDialog("Optimize Block D plates",
                "Replace the meshes and materials on " + plateObjects.Length +
                " Block D plates using the existing Block C optimized white plate?\n\n" +
                "Original USDZ files, GameObjects, colliders, plate slots, food meshes, and interaction scripts will not be changed.",
                "Optimize", "Cancel")) return;

        Mesh persistentMesh = sourceMesh;
        if (!AssetDatabase.Contains(sourceMesh))
        {
            EnsureFolder("Assets/ImmersiveEyeGenerated");
            EnsureFolder("Assets/ImmersiveEyeGenerated/Meshes");
            persistentMesh = AssetDatabase.LoadAssetAtPath<Mesh>(GeneratedMeshPath);
            if (persistentMesh == null)
            {
                persistentMesh = UnityEngine.Object.Instantiate(sourceMesh);
                persistentMesh.name = "ImmersiveEye_LowPolyPlate_FromBlockC";
                persistentMesh.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(persistentMesh, GeneratedMeshPath);
            }
        }

        int changed = 0, alreadyOptimized = 0;
        long before = 0, after = 0;
        Undo.SetCurrentGroupName("ImmersiveEye Optimize Block D Plates");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (Transform plate in plateObjects)
        {
            MeshFilter filter = plate.GetComponent<MeshFilter>();
            MeshRenderer renderer = plate.GetComponent<MeshRenderer>();
            before += TriangleCount(filter.sharedMesh);
            bool meshDifferent = filter.sharedMesh != persistentMesh;
            bool materialDifferent = renderer.sharedMaterial != white;
            if (meshDifferent || materialDifferent)
            {
                Undo.RecordObject(filter, "Optimize Block D plate mesh");
                Undo.RecordObject(renderer, "Apply Block D white plate material");
                filter.sharedMesh = persistentMesh;
                renderer.sharedMaterial = white;
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(filter);
                EditorUtility.SetDirty(renderer);
                changed++;
            }
            else alreadyOptimized++;
            after += TriangleCount(filter.sharedMesh);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("IMMERSIVEEYE_BLOCK_D_PLATES_OPTIMIZED" +
            "|found=" + plateObjects.Length +
            "|changed=" + changed +
            "|alreadyOptimized=" + alreadyOptimized +
            "|oldTriangles=" + before +
            "|newTriangles=" + after +
            "|reusedBlockCPlateTriangles=" + newTris +
            "|originalAssetsUnchanged=true" +
            "|sceneNeedsSave=true");
        EditorUtility.DisplayDialog("ImmersiveEye Block D", "Updated " + changed +
            " of " + plateObjects.Length + " plates.\n\nTriangles: " +
            before.ToString("N0") + " → " + after.ToString("N0") +
            "\n\nInspect Block D in the Scene view, then save the scene (Cmd+S).", "OK");
    }

    private static Transform FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == objectName) return t;
        return null;
    }

    private static long TriangleCount(Mesh mesh)
    {
        if (mesh == null) return 0;
        long total = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            if (mesh.GetTopology(i) == MeshTopology.Triangles)
                total += (long)mesh.GetIndexCount(i) / 3;
        }
        return total;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int cut = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, cut), path.Substring(cut + 1));
    }
}
#endif
