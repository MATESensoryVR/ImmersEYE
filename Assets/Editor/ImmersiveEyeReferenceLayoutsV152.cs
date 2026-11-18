#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Restores the measured Block A / Block B plate layouts from the 25 October 2026
/// saved VRETScene (prior to v15 food spacing). No food instances, AOIs, render
/// objects, event scripts, cameras, video settings or references are replaced.
/// </summary>
public static class ImmersiveEyeReferenceLayoutsV152
{
    private const string Menu = "Tools/ImmersiveEye/v15/3 - Restore reference A/B plate arrangement";
    private struct Item
    {
        public string name;
        public Vector3 localPosition;
        public Item(string n, float x, float y, float z) { name=n;localPosition=new Vector3(x,y,z); }
    }

    [MenuItem(Menu)]
    public static void Restore()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "VRETScene")
        {
            EditorUtility.DisplayDialog("ImmersiveEye", "Open VRETScene before running this layout correction.", "OK");
            return;
        }
        GameObject aGO = Find(scene, "BlockA_Plate");
        GameObject bGO = Find(scene, "PlateA_Group");
        if (aGO == null || bGO == null) { Fail("BlockA_Plate or PlateA_Group was not found. No changes made."); return; }

        Item[] a = {
            new Item("Plate_Base", 0f,1.1f,2f),
            new Item("Protein", -.096f,.97300005f,1.926f),
            new Item("Carbohydrate", .04f,1.22f,1.997f),
            new Item("Vegetables", -.14f,1.199f,1.969f),
            new Item("Fruits", -.468f,1.468f,1.988f),
            new Item("HealthyFat", .189f,1.011f,1.946f),
            new Item("Beverage", .536f,1.5f,2.08f)
        };
        Item[] b = {
            new Item("PlateA",-.85f,.65f,6.5f),
            new Item("PlateB",.9f,.65f,6.5f),
            new Item("PlateC",-.89f,-.67f,6.5f),
            new Item("PlateD",.9f,-.669f,6.5f)
        };
        foreach (Item item in a)
            if (aGO.transform.Find(item.name) == null) { Fail("Missing Block A object: "+item.name); return; }
        foreach (Item item in b)
            if (bGO.transform.Find(item.name) == null) { Fail("Missing Block B object: "+item.name); return; }

        if (!EditorUtility.DisplayDialog("Restore the pictured Block A/B arrangement?",
            "Block A: keep the central mixed-food plate, fruit bowl at upper left, and drink at upper right. " +
            "Block B: keep four complete dishes in a 2 × 2 grid. " +
            "Restore only the 11 listed object positions from your original saved scene. " +
            "No food, AOI or recording components are deleted or reassigned. A backup will be made.",
            "Restore exact positions", "Cancel")) return;
        if (scene.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try
        {
            string absolute = Path.GetFullPath(scene.path);
            string backupDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../ImmersiveEye_v15_2_SceneBackups"));
            Directory.CreateDirectory(backupDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupFile = Path.Combine(backupDir, "VRETScene_before_layout_fix_" + stamp + ".unity");
            File.Copy(absolute, backupFile, false);
            Debug.Log("IE_LAYOUT_V152|BACKUP|" + backupFile);
        }
        catch(Exception e) { Fail("Could not back up the scene. Nothing changed. " + e.Message); return; }

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Restore pictured A/B plate arrangement");
        foreach (Item item in a)
        {
            Transform t = aGO.transform.Find(item.name);
            Undo.RecordObject(t, "Restore Block A item " + item.name);
            t.localPosition = item.localPosition;
            EditorUtility.SetDirty(t);
        }
        foreach (Item item in b)
        {
            Transform t = bGO.transform.Find(item.name);
            Undo.RecordObject(t, "Restore Block B plate " + item.name);
            t.localPosition = item.localPosition;
            EditorUtility.SetDirty(t);
        }
        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("IE_LAYOUT_V152|RESTORED|BLOCK_A_7|BLOCK_B_4");
        EditorUtility.DisplayDialog("Layout restored",
            "Block A's composed plate, upper-left fruit bowl and upper-right drink are restored. " +
            "Block B's four whole plates are restored. Save the scene (Cmd+S) and inspect A and B before Build and Run. " +
            "Do not run the older v15 food spacing menu afterward.", "OK");
    }
    private static GameObject Find(Scene scene, string name)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(o=>o.scene == scene && o.name == name);
    }
    private static void Fail(string msg) { EditorUtility.DisplayDialog("ImmersiveEye layout",msg,"OK"); }
}
#endif
