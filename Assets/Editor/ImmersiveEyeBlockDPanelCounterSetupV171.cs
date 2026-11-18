#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ImmersiveEyeBlockDPanelCounterSetupV171
{
    [MenuItem("Tools/ImmersiveEye/v17.1 - Put Block D counter in existing panel")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "VRETScene" || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("ImmersiveEye v17.1", "Open and save VRETScene first.", "OK");
            return;
        }

        GameObject Find(string name) => Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(go => go.scene == scene && go.name == name);

        GameObject ui = Find("UI");
        GameObject panel = Find("BlockDConfirmPanel");
        GameObject labelGo = Find("SelectionCountText");
        GameObject plateGo = Find("BlockD_PlateManager");
        if (ui == null || panel == null || labelGo == null || plateGo == null)
        {
            EditorUtility.DisplayDialog("ImmersiveEye v17.1",
                "Missing UI, BlockDConfirmPanel, SelectionCountText, or BlockD_PlateManager. No changes made.", "OK");
            return;
        }
        var label = labelGo.GetComponent<TMP_Text>();
        var rect = labelGo.GetComponent<RectTransform>();
        var manager = plateGo.GetComponents<MonoBehaviour>()
            .FirstOrDefault(c => c != null && c.GetType().Name == "BlockDPlateManager");
        if (label == null || rect == null || manager == null)
        {
            EditorUtility.DisplayDialog("ImmersiveEye v17.1", "Missing text, RectTransform or BlockDPlateManager component. No changes made.", "OK");
            return;
        }
        SerializedProperty foodEntries = new SerializedObject(manager).FindProperty("foods");
        if (foodEntries == null || !foodEntries.isArray || foodEntries.arraySize == 0)
        {
            EditorUtility.DisplayDialog("ImmersiveEye v17.1", "No food references on BlockDPlateManager. No changes made.", "OK");
            return;
        }
        Transform[] foods = new Transform[foodEntries.arraySize];
        for (int i=0; i<foods.Length; i++)
        {
            UnityEngine.Object entry = foodEntries.GetArrayElementAtIndex(i).objectReferenceValue;
            foods[i] = (entry as Component)?.transform ?? (entry as GameObject)?.transform;
            if (foods[i] == null)
            {
                EditorUtility.DisplayDialog("ImmersiveEye v17.1", "Food reference " + (i + 1) + " is missing; no changes made.", "OK");
                return;
            }
        }
        if (!EditorUtility.DisplayDialog("Fix Block D counter",
            "Move the existing SelectionCountText INTO the visible BlockDConfirmPanel (between the title and instructions) and show 0/4 immediately? Existing food selection and event logic will remain unchanged.",
            "Apply and back up", "Cancel")) return;

        string project = Directory.GetParent(Application.dataPath).FullName;
        string backup = Path.Combine(project, "ImmersiveEye_v17_1_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(backup);
        File.Copy(Path.GetFullPath(scene.path), Path.Combine(backup, Path.GetFileName(scene.path)), true);

        // Previous counter HUDs could hide the original label or draw a second one off-screen.
        foreach (MonoBehaviour component in ui.GetComponents<MonoBehaviour>())
        {
            if (component == null) continue;
            string name = component.GetType().Name;
            if (name == "ImmersiveEyeBlockDCounterHUD" || name == "ImmersiveEyeBlockDLiveCounterV17")
            {
                Undo.RecordObject(component, "Disable old Block D counter HUD");
                component.enabled = false;
            }
        }
        GameObject oldLabel = Find("BlockDLiveCount_V17");
        if (oldLabel != null)
        {
            Undo.RecordObject(oldLabel, "Hide duplicated off-screen Block D counter");
            oldLabel.SetActive(false);
        }

        Undo.SetTransformParent(rect, panel.transform, "Place live counter inside Block D panel");
        Undo.RecordObject(rect, "Position live count between title and instruction");
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 36f);
        // Reset the depth inherited when reparenting from the world-space UI root.
        Vector3 local = rect.localPosition;
        rect.localPosition = new Vector3(local.x, local.y, 0f);
        rect.sizeDelta = new Vector2(800f, 44f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.SetAsLastSibling();

        Undo.RecordObject(label, "Make Block D counter clearly readable");
        label.text = "Selected: 0 / 4";
        label.color = new Color(0.12f, 0.20f, 0.21f, 1f);
        label.fontSize = 34f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        Undo.RecordObject(labelGo, "Show Block D counter text");
        labelGo.SetActive(true);

        ImmersiveEyeBlockDPanelCounterV171 counter = ui.GetComponent<ImmersiveEyeBlockDPanelCounterV171>();
        if (counter == null) counter = Undo.AddComponent<ImmersiveEyeBlockDPanelCounterV171>(ui);
        SerializedObject so = new SerializedObject(counter);
        so.FindProperty("confirmationPanel").objectReferenceValue = panel;
        so.FindProperty("selectionCountText").objectReferenceValue = label;
        SerializedProperty arr = so.FindProperty("foods");
        arr.arraySize = foods.Length;
        for (int i=0;i<foods.Length;i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = foods[i];
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(label);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("IE_V17_1|COUNTER_INSIDE_PANEL|foods=" + foods.Length + "|backup=" + backup);
        EditorUtility.DisplayDialog("ImmersiveEye v17.1",
            "Counter is inside the Block D panel. Save and Build and Run. Verify 0/4 at start, then 1/4 through 4/4 and decrement on food return. Scene backup: " + backup,
            "OK");
    }
}
#endif
