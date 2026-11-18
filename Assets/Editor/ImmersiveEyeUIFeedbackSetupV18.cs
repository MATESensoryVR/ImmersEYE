#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One-click, backed-up XR UI feedback setup for VRETScene.</summary>
public static class ImmersiveEyeUIFeedbackSetupV18
{
    [MenuItem("Tools/ImmersiveEye/v18 - Colour buttons, checkboxes and XR ray")]
    private static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "VRETScene" || string.IsNullOrEmpty(scene.path))
        {
            EditorUtility.DisplayDialog("ImmersiveEye v18", "Open VRETScene first. Nothing was changed.", "OK");
            return;
        }
        if (scene.isDirty)
        {
            EditorUtility.DisplayDialog("ImmersiveEye v18", "Save VRETScene (Cmd+S) before applying this fix. Nothing was changed.", "OK");
            return;
        }

        GameObject ui = FindInScene(scene, "UI");
        if (ui == null || ui.GetComponent<Canvas>() == null)
        {
            EditorUtility.DisplayDialog("ImmersiveEye v18", "The main World Space Canvas 'UI' was not found. Nothing was changed.", "OK");
            return;
        }

        MonoBehaviour[] allScripts = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        int xrRayCount = 0;
        foreach (MonoBehaviour script in allScripts)
            if (script != null && script.GetType().Name == "XRInteractorLineVisual")
                xrRayCount++;
        if (xrRayCount == 0)
        {
            EditorUtility.DisplayDialog("ImmersiveEye v18", "No XRInteractorLineVisual was found. Nothing was changed.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("ImmersiveEye v18",
            "Add hover/press colours for UI buttons and checkboxes, and change the XR controller ray:\n\n" +
            "CYAN = pointing\nGREEN = interactive target\nAMBER = blocked\n\n" +
            "Create a backup of the scene before applying?", "Back up and apply", "Cancel"))
            return;

        string src = Path.GetFullPath(scene.path);
        string backupFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "",
            "ImmersiveEye_v18_UI_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(backupFolder);
        File.Copy(src, Path.Combine(backupFolder, "VRETScene.unity"), true);

        ImmersiveEyeUIFeedbackTheme theme = ui.GetComponent<ImmersiveEyeUIFeedbackTheme>();
        if (theme == null) theme = Undo.AddComponent<ImmersiveEyeUIFeedbackTheme>(ui);
        // Record old values for Editor Undo while applying the new theme.
        Selectable[] existing = ui.GetComponentsInChildren<Selectable>(true);
        foreach (Selectable s in existing)
            if (s != null) Undo.RecordObject(s, "ImmersiveEye v18 UI colours");
        int count = theme.ApplyTheme();
        EditorUtility.SetDirty(theme);

        int linesChanged = 0;
        foreach (MonoBehaviour script in allScripts)
        {
            if (script == null || script.GetType().Name != "XRInteractorLineVisual") continue;
            Undo.RecordObject(script, "ImmersiveEye v18 ray colours");
            SerializedObject so = new SerializedObject(script);
            SerializedProperty enabled = so.FindProperty("m_SetLineColorGradient");
            SerializedProperty valid = so.FindProperty("m_ValidColorGradient");
            SerializedProperty invalid = so.FindProperty("m_InvalidColorGradient");
            SerializedProperty blocked = so.FindProperty("m_BlockedColorGradient");
            if (valid == null || invalid == null || blocked == null)
            {
                Debug.LogWarning("IE_V18: Skipped unexpected XR line visual: " + script.name);
                continue;
            }
            if (enabled != null) enabled.boolValue = true;
            valid.gradientValue = SolidGradient(new Color(0.30f, 1.00f, 0.48f, 1f));
            invalid.gradientValue = SolidGradient(new Color(0.11f, 0.84f, 1.00f, 1f));
            blocked.gradientValue = SolidGradient(new Color(1.00f, 0.70f, 0.19f, 1f));
            SerializedProperty width = so.FindProperty("m_LineWidth");
            if (width != null) width.floatValue = 0.006f;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(script);
            linesChanged++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("IE_V18_SUCCESS|sceneButtons=" + count + "|XRrays=" + linesChanged + "|backup=" + backupFolder);
        EditorUtility.DisplayDialog("ImmersiveEye v18",
            "UI colour feedback installed.\n\n" +
            count + " existing UI controls configured; " + linesChanged + " XR ray(s) recoloured.\n" +
            "Participant picker buttons are styled automatically at runtime.\n\n" +
            "Scene backup:\n" + backupFolder + "\n\nBuild and Run to test on the headset.", "OK");
    }

    private static GameObject FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root;
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
                if (child.name == name) return child.gameObject;
        }
        return null;
    }

    private static Gradient SolidGradient(Color color)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}
#endif
