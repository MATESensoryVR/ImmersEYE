// Editor-only diagnostic. Put this file inside Assets/Editor/.
// Does not change scene objects, materials, meshes, or experiment data.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ImmersiveEyeFoodAudit
{
    private sealed class FoodStats
    {
        public string name;
        public long triangles;
        public int meshRenderers;
        public int skinnedRenderers;
        public int materialSlots;
        public int meshFilters;
        public int animators;
        public int particleSystems;
        public int lights;
        public int videoPlayers;
    }

    [MenuItem("Tools/ImmersiveEye/Audit Selected Food Hierarchy")]
    private static void AuditSelected()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("Food audit: Select BlockC_Food Items or BlockD_Food Items in the Hierarchy first.");
            return;
        }

        var rows = new List<FoodStats>();
        foreach (Transform directChild in selected.transform)
            rows.Add(Analyze(directChild.gameObject));

        if (rows.Count == 0)
            rows.Add(Analyze(selected));

        rows = rows.OrderByDescending(row => row.triangles).ToList();
        var report = new StringBuilder();
        report.AppendLine("=== ImmersiveEye Food Model Audit (Editor only) ===");
        report.AppendLine("Selected group: " + selected.name);
        report.AppendLine("Numbers are mesh triangle estimates and material slots, NOT measured FPS or draw calls.");
        report.AppendLine("Includes disabled child objects; no scene changes were made.");
        report.AppendLine();

        long totalTris = 0;
        int totalMeshRenderers = 0, totalSkinned = 0, totalMaterialSlots = 0;
        foreach (FoodStats row in rows)
        {
            totalTris += row.triangles;
            totalMeshRenderers += row.meshRenderers;
            totalSkinned += row.skinnedRenderers;
            totalMaterialSlots += row.materialSlots;
            report.AppendLine(string.Format(
                "{0} | triangles={1:N0} | mesh renderers={2} | skinned renderers={3} | material slots={4} | animators={5} | particles={6} | lights={7} | video players={8}",
                row.name, row.triangles, row.meshRenderers, row.skinnedRenderers,
                row.materialSlots, row.animators, row.particleSystems, row.lights, row.videoPlayers));
        }
        report.AppendLine();
        report.AppendLine(string.Format(
            "TOTAL | triangles={0:N0} | mesh renderers={1} | skinned renderers={2} | material slots={3}",
            totalTris, totalMeshRenderers, totalSkinned, totalMaterialSlots));

        Debug.Log(report.ToString(), selected);
        EditorGUIUtility.systemCopyBuffer = report.ToString();
        Debug.Log("Food audit: Report copied to clipboard. Paste it into the chat.");
    }

    private static FoodStats Analyze(GameObject root)
    {
        var stats = new FoodStats { name = root.name };
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null || filter.sharedMesh == null) continue;
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled) continue;
            stats.meshFilters++;
            stats.meshRenderers++;
            stats.materialSlots += renderer.sharedMaterials.Length;
            stats.triangles += CountTriangles(filter.sharedMesh);
        }
        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer == null || !renderer.enabled || renderer.sharedMesh == null) continue;
            stats.skinnedRenderers++;
            stats.materialSlots += renderer.sharedMaterials.Length;
            stats.triangles += CountTriangles(renderer.sharedMesh);
        }
        stats.animators = root.GetComponentsInChildren<Animator>(true).Length;
        stats.particleSystems = root.GetComponentsInChildren<ParticleSystem>(true).Length;
        stats.lights = root.GetComponentsInChildren<Light>(true).Length;
        stats.videoPlayers = root.GetComponentsInChildren<UnityEngine.Video.VideoPlayer>(true).Length;
        return stats;
    }

    private static long CountTriangles(Mesh mesh)
    {
        long count = 0;
        for (int submesh = 0; submesh < mesh.subMeshCount; ++submesh)
        {
            if (mesh.GetTopology(submesh) == MeshTopology.Triangles)
                count += (long)mesh.GetIndexCount(submesh) / 3L;
        }
        return count;
    }
}
