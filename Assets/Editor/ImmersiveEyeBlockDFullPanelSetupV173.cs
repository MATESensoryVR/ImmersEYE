#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ImmersiveEyeBlockDFullPanelSetupV173
{
    [MenuItem("Tools/ImmersiveEye/v17.3 - Show COMPLETE Block D panel from start")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "VRETScene" || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("ImmersiveEye", "Open VRETScene first.", "OK");
            return;
        }

        GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.scene == scene && go.name == name);

        GameObject ui = Find("UI");
        GameObject panel = Find("BlockDConfirmPanel");
        GameObject env = Find("BlockD_Food Items");
        GameObject intro = Find("BlockDIntroPanel");
        GameObject end = Find("EndPanel");
        GameObject labelGO = Find("SelectionCountText");
        GameObject plate = Find("BlockD_PlateManager");

        if (ui == null || panel == null || env == null || labelGO == null || plate == null)
        {
            EditorUtility.DisplayDialog("ImmersiveEye",
                "Cannot find UI, BlockDConfirmPanel, BlockD_Food Items, SelectionCountText, or BlockD_PlateManager. Nothing changed.", "OK");
            return;
        }
        TMP_Text label = labelGO.GetComponent<TMP_Text>();
        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        MonoBehaviour plateManager = plate.GetComponents<MonoBehaviour>()
            .FirstOrDefault(c => c != null && c.GetType().Name == "BlockDPlateManager");
        if (label == null || labelRect == null || plateManager == null)
        {
            EditorUtility.DisplayDialog("ImmersiveEye", "Missing TMP label or BlockDPlateManager component. Nothing changed.", "OK");
            return;
        }
        SerializedProperty entries = new SerializedObject(plateManager).FindProperty("foods");
        if (entries == null || !entries.isArray || entries.arraySize == 0)
        {
            EditorUtility.DisplayDialog("ImmersiveEye", "Could not read the BlockDPlateManager food references. Nothing changed.", "OK");
            return;
        }
        List<Transform> foods = new List<Transform>();
        for (int i = 0; i < entries.arraySize; i++)
        {
            UnityEngine.Object item = entries.GetArrayElementAtIndex(i).objectReferenceValue;
            Transform t = (item as Component)?.transform ?? (item as GameObject)?.transform;
            if (t == null)
            {
                EditorUtility.DisplayDialog("ImmersiveEye", "Missing food reference at index " + i + ". Nothing changed.", "OK");
                return;
            }
            foods.Add(t);
        }

        if (!EditorUtility.DisplayDialog("ImmersiveEye v17.3",
            "Show the COMPLETE Build your own meal panel from the BEGINNING of Block D (title, Selected: 0/4, instructions, Confirm Selection)?\n\nDoes not change selection rules, AOIs, gaze or video. Scene backup will be created.",
            "Apply and back up", "Cancel")) return;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string backup = Path.Combine(projectRoot, "ImmersiveEye_v17_3_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(backup);
        File.Copy(Path.GetFullPath(scene.path), Path.Combine(backup, Path.GetFileName(scene.path)), true);

        // Disable earlier counter-only HUDs; they would hide/move the text.
        foreach (MonoBehaviour component in ui.GetComponents<MonoBehaviour>())
        {
            if (component == null) continue;
            string n = component.GetType().Name;
            if (n == "ImmersiveEyeBlockDCounterHUD" ||
                n == "ImmersiveEyeBlockDLiveCounterV17" ||
                n == "ImmersiveEyeBlockDPanelCounterV171" ||
                n == "ImmersiveEyeBlockDAlwaysVisibleCounterV172")
            {
                Undo.RecordObject(component, "Disable old Block D counter display");
                component.enabled = false;
            }
        }
        GameObject oldHUD = Find("BlockDLiveCount_V17");
        if (oldHUD != null)
        {
            Undo.RecordObject(oldHUD, "Hide duplicate counter");
            oldHUD.SetActive(false);
        }

        // Restore the ORIGINAL count label to the ORIGINAL entire panel.
        Undo.SetTransformParent(labelRect, panel.transform, "Put selection counter back into the full Block D panel");
        Undo.RecordObject(labelRect, "Place count between title and instructions");
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, 36f);
        labelRect.sizeDelta = new Vector2(800f, 45f);
        labelRect.localScale = Vector3.one;
        labelRect.localRotation = Quaternion.identity;
        Vector3 lp = labelRect.localPosition;
        labelRect.localPosition = new Vector3(lp.x, lp.y, 0f);
        labelRect.SetAsLastSibling();

        Undo.RecordObject(label, "Format full panel counter");
        label.text = "Selected: 0 / 4";
        label.fontSize = 34f;
        label.color = new Color(0.12f, 0.19f, 0.20f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        Undo.RecordObject(labelGO, "Keep count text enabled");
        labelGO.SetActive(true);

        ImmersiveEyeBlockDFullPanelV173 script = ui.GetComponent<ImmersiveEyeBlockDFullPanelV173>();
        if (script == null) script = Undo.AddComponent<ImmersiveEyeBlockDFullPanelV173>(ui);
        SerializedObject so = new SerializedObject(script);
        so.FindProperty("blockDEnvironment").objectReferenceValue = env;
        so.FindProperty("blockDIntroPanel").objectReferenceValue = intro;
        so.FindProperty("endPanel").objectReferenceValue = end;
        so.FindProperty("blockDConfirmPanel").objectReferenceValue = panel;
        so.FindProperty("selectionCountText").objectReferenceValue = label;
        SerializedProperty list = so.FindProperty("foods");
        list.arraySize = foods.Count;
        for (int i = 0; i < foods.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = foods[i];
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(label);
        EditorUtility.SetDirty(script);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("ImmersiveEye v17.3",
            "The ENTIRE Block D panel will be shown from the start of Block D. The counter is back inside the panel. Save scene, Build and Run.\n\nBackup: " + backup, "OK");
        Debug.Log("IE_V17_3|FULL_BLOCK_D_PANEL_FROM_START|foods=" + foods.Count + "|backup=" + backup);
    }
}
#endif
