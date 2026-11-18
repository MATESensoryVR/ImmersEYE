#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor-only, reversible typography-only operation. The font itself must
/// be provided by the project owner. No proprietary font is distributed.
/// </summary>
public static class ImmersiveEyeCenturyGothicSetupV19
{
    [MenuItem("Tools/ImmersiveEye/v19 - Apply Century Gothic font")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Century Gothic", "Open VRETScene first.", "OK");
            return;
        }

        TMP_FontAsset font = FindCenturyGothic();
        if (font == null)
        {
            EditorUtility.DisplayDialog("Century Gothic asset needed",
                "No Century Gothic TextMeshPro Font Asset was found in Assets.\n\n" +
                "1. Import your licensed Century Gothic .ttf/.otf into Assets/Fonts.\n" +
                "2. Select that font and use Create > TextMeshPro > Font Asset, " +
                "or Window > TextMeshPro > Font Asset Creator.\n" +
                "3. Name the resulting asset Century Gothic SDF.\n" +
                "4. Run this menu again.\n\n" +
                "No scene changes were made.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Apply Century Gothic",
            "Apply '" + font.name + "' to the open scene's TextMeshPro labels " +
            "and add runtime support for dynamically created buttons?\n\n" +
            "This changes typography only. An Undo step will be recorded.",
            "Apply", "Cancel"))
            return;

        int textCount = 0;
        int canvasCount = 0;
        List<Canvas> rootCanvases = new List<Canvas>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.font == font) continue;
                Undo.RecordObject(label, "Apply Century Gothic");
                label.font = font;
                EditorUtility.SetDirty(label);
                textCount++;
            }

            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.transform.parent != null &&
                    canvas.transform.parent.GetComponentInParent<Canvas>() != null)
                    continue; // one runtime watcher per top-level canvas
                rootCanvases.Add(canvas);
            }
        }

        foreach (Canvas canvas in rootCanvases)
        {
            ImmersiveEyeCenturyGothicTheme theme =
                canvas.GetComponent<ImmersiveEyeCenturyGothicTheme>();
            if (theme == null)
                theme = Undo.AddComponent<ImmersiveEyeCenturyGothicTheme>(canvas.gameObject);
            Undo.RecordObject(theme, "Assign Century Gothic runtime font");
            theme.SetFont(font);
            EditorUtility.SetDirty(theme);
            canvasCount++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("IE_CENTURY_GOTHIC|SUCCESS|font=" + font.name +
                  "|sceneLabels=" + textCount + "|canvasWatchers=" + canvasCount);
        EditorUtility.DisplayDialog("ImmersiveEye font updated",
            "Century Gothic was assigned to " + textCount + " scene labels.\n" +
            "Runtime font support was attached to " + canvasCount + " canvas(es).\n\n" +
            "Save VRETScene, then Build and Run. " +
            "Please verify that all UI labels fit their existing panels.", "OK");
    }

    private static TMP_FontAsset FindCenturyGothic()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        TMP_FontAsset best = null;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (f == null) continue;
            string normalizedName = Normalize(f.name + " " + path);
            if (!normalizedName.Contains("centurygothic")) continue;
            if (best == null) best = f;
            // Prefer the regular face if both Regular and Bold are present.
            if (!normalizedName.Contains("bold") && !normalizedName.Contains("italic"))
                return f;
        }
        return best;
    }

    private static string Normalize(string value)
    {
        StringBuilder sb = new StringBuilder(value.Length);
        foreach (char c in value.ToLowerInvariant())
            if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }
}
#endif
