#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Select the existing EndPanel in the Hierarchy and run the Tools menu item.
/// Adds an idempotent UI button with a self-wired click handler.
/// </summary>
public static class ImmersiveEyeAddEndReturnButton
{
    private const string ButtonName = "ReturnToImmersiveEyeButton";

    [MenuItem("Tools/ImmersiveEye/Add Return to ImmersiveEye Button to Selected EndPanel")]
    private static void AddButton()
    {
        GameObject panel = Selection.activeGameObject;
        if (panel == null || panel.GetComponent<RectTransform>() == null)
        {
            EditorUtility.DisplayDialog("Select EndPanel",
                "In the Hierarchy, select your existing EndPanel (a UI GameObject with a RectTransform), then run this tool again.", "OK");
            return;
        }

        Transform found = panel.transform.Find(ButtonName);
        if (found != null)
        {
            Selection.activeGameObject = found.gameObject;
            Debug.Log("IMMERSIVEEYE_RETURN_HOME_BUTTON|alreadyExists=true|parent=" + panel.name + "|created=false");
            return;
        }

        GameObject go = new GameObject(ButtonName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(Button), typeof(ImmersiveEyeReturnHomeButton));
        Undo.RegisterCreatedObjectUndo(go, "Add Return to ImmersiveEye Button");
        go.transform.SetParent(panel.transform, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.12f);
        rect.anchorMax = new Vector2(0.5f, 0.12f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(460f, 86f);
        rect.localScale = Vector3.one;

        Image background = go.GetComponent<Image>();
        background.color = new Color(0.03f, 0.50f, 0.43f, 1f);
        background.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.interactable = true;
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;

        GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(textGo, "Add Return Button Label");
        textGo.transform.SetParent(go.transform, false);
        RectTransform textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 6f);
        textRect.offsetMax = new Vector2(-12f, -6f);

        TextMeshProUGUI label = textGo.GetComponent<TextMeshProUGUI>();
        label.text = "RETURN TO IMMERSIVEEYE";
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 32f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 20f;
        label.fontSizeMax = 32f;
        label.color = Color.white;
        label.raycastTarget = false;

        // Explicitly leave the root's scale untouched; only create a child button.
        EditorUtility.SetDirty(panel);
        if (panel.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(panel.scene);
        Selection.activeGameObject = go;

        Debug.Log("IMMERSIVEEYE_RETURN_HOME_BUTTON|created=true|parent=" + panel.name +
                  "|text=RETURN TO IMMERSIVEEYE|action=reloadCurrentScene");
    }
}
#endif
