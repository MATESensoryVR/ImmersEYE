#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ImmersiveEyeBlockDAlwaysVisibleCounterSetupV172
{
    [MenuItem("Tools/ImmersiveEye/v17.2 - Show Block D count from start")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "VRETScene" || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("ImmersiveEye v17.2", "Open VRETScene first.", "OK");
            return;
        }
        GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.scene == scene && go.name == name);
        GameObject ui = Find("UI");
        GameObject env = Find("BlockD_Food Items");
        GameObject intro = Find("BlockDIntroPanel");
        GameObject panel = Find("BlockDConfirmPanel");
        GameObject countObject = Find("SelectionCountText");
        GameObject managerObject = Find("BlockD_PlateManager");
        if (ui == null || env == null || panel == null || countObject == null || managerObject == null)
        {
            EditorUtility.DisplayDialog("ImmersiveEye v17.2", "Missing UI, Block D food environment, confirm panel, SelectionCountText or plate manager. No changes made.", "OK");
            return;
        }
        RectTransform root = ui.GetComponent<RectTransform>();
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        RectTransform labelRect = countObject.GetComponent<RectTransform>();
        TMP_Text label = countObject.GetComponent<TMP_Text>();
        MonoBehaviour manager = managerObject.GetComponents<MonoBehaviour>()
            .FirstOrDefault(m => m != null && m.GetType().Name == "BlockDPlateManager");
        if (root == null || panelRect == null || labelRect == null || label == null || manager == null)
        {
            EditorUtility.DisplayDialog("ImmersiveEye v17.2", "Missing RectTransforms, TMP text or BlockDPlateManager. No changes made.", "OK");
            return;
        }
        SerializedProperty sourceFoods = new SerializedObject(manager).FindProperty("foods");
        if (sourceFoods == null || !sourceFoods.isArray || sourceFoods.arraySize == 0)
        {
            EditorUtility.DisplayDialog("ImmersiveEye v17.2", "BlockDPlateManager does not expose assigned foods. No changes made.", "OK");
            return;
        }
        var tracked = new List<Transform>();
        for (int i = 0; i < sourceFoods.arraySize; i++)
        {
            UnityEngine.Object entry = sourceFoods.GetArrayElementAtIndex(i).objectReferenceValue;
            Transform food = (entry as Component)?.transform ?? (entry as GameObject)?.transform;
            if (food == null)
            {
                EditorUtility.DisplayDialog("ImmersiveEye v17.2", "A food reference is missing (index " + i + "). No changes made.", "OK");
                return;
            }
            tracked.Add(food);
        }
        if (!EditorUtility.DisplayDialog("ImmersiveEye v17.2",
            "Show the existing Selected: 0 / 4 text on the main UI from the START of Block D, even if the confirmation panel is hidden? Keep all food and recording logic unchanged?",
            "Apply and back up", "Cancel")) return;

        string backup = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "ImmersiveEye_v17_2_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(backup);
        File.Copy(Path.GetFullPath(scene.path), Path.Combine(backup, Path.GetFileName(scene.path)), true);

        // Disable ONLY older counter display components. Do not disable food selection controllers.
        foreach (MonoBehaviour c in ui.GetComponents<MonoBehaviour>())
        {
            if (c == null) continue;
            string n = c.GetType().Name;
            if (n == "ImmersiveEyeBlockDCounterHUD" ||
                n == "ImmersiveEyeBlockDLiveCounterV17" ||
                n == "ImmersiveEyeBlockDPanelCounterV171")
            {
                Undo.RecordObject(c, "Disable duplicate Block D HUD");
                c.enabled = false;
            }
        }
        GameObject duplicate = Find("BlockDLiveCount_V17");
        if (duplicate != null)
        {
            Undo.RecordObject(duplicate, "Hide duplicate Block D HUD");
            duplicate.SetActive(false);
        }

        // The count is the SAME TMP_Text referenced by BlockDTrialController.
        // Reparenting preserves serialized references; do not clone the object.
        Undo.SetTransformParent(labelRect, root, "Free counter from confirmation panel");
        Undo.RecordObject(labelRect, "Position counter above confirmation card");
        labelRect.anchorMin = panelRect.anchorMin;
        labelRect.anchorMax = panelRect.anchorMax;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = panelRect.anchoredPosition + new Vector2(0f, 36f);
        labelRect.sizeDelta = new Vector2(800f, 55f);
        labelRect.localPosition = new Vector3(labelRect.localPosition.x, labelRect.localPosition.y, 0f);
        labelRect.localRotation = Quaternion.identity;
        labelRect.localScale = Vector3.one;
        labelRect.SetAsLastSibling(); // render on top of the confirmation card

        Undo.RecordObject(label, "Style always visible count");
        label.text = "Selected: 0 / 4";
        label.fontSize = 38;
        label.color = new Color(0.08f, 0.28f, 0.26f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        Undo.RecordObject(countObject, "Counter visible in Block D");
        countObject.SetActive(true); // runtime component manages visibility per block

        ImmersiveEyeBlockDAlwaysVisibleCounterV172 hud = ui.GetComponent<ImmersiveEyeBlockDAlwaysVisibleCounterV172>();
        if (hud == null) hud = Undo.AddComponent<ImmersiveEyeBlockDAlwaysVisibleCounterV172>(ui);
        SerializedObject settings = new SerializedObject(hud);
        settings.FindProperty("blockDEnvironment").objectReferenceValue = env;
        settings.FindProperty("blockDIntroPanel").objectReferenceValue = intro;
        settings.FindProperty("selectionCountText").objectReferenceValue = label;
        SerializedProperty arr = settings.FindProperty("foods");
        arr.arraySize = tracked.Count;
        for (int i = 0; i < tracked.Count; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = tracked[i];
        settings.ApplyModifiedProperties();
        EditorUtility.SetDirty(hud);
        EditorUtility.SetDirty(label);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("IE_V17_2|BLOCK_D_COUNT_FROM_START|foods=" + tracked.Count + "|backup=" + backup);
        EditorUtility.DisplayDialog("ImmersiveEye v17.2",
            "Count moved to main UI, independent of confirmation panel. Save, Build and Run. Test 0/4 at Block D start and each click/return. Backup: " + backup,
            "OK");
    }
}
#endif
