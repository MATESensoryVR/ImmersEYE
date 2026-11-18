using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-specific, reversible editor setup for ImmersiveEye VRETScene.</summary>
public static class ImmersiveEyeUXSetupV15
{
    private const string Menu = "Tools/ImmersiveEye/v15/";

    private static GameObject FindGO(string name)
    {
        Scene s = SceneManager.GetActiveScene();
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .FirstOrDefault(o => o.scene == s && o.name == name);
    }

    private static bool VerifyScene()
    {
        if (SceneManager.GetActiveScene().name == "VRETScene") return true;
        EditorUtility.DisplayDialog("Wrong scene", "Open VRETScene first. This tool does not modify other scenes.", "OK");
        return false;
    }

    private static void SetRef(SerializedObject so, string name, UnityEngine.Object obj)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) throw new InvalidOperationException("Missing v15 property: " + name);
        p.objectReferenceValue = obj;
    }

    [MenuItem(Menu + "1 - Apply participant picker and Block D counter")]
    public static void ApplyUI()
    {
        if (!VerifyScene()) return;
        GameObject home = FindGO("HomePanel");
        GameObject participantGO = FindGO("ParticipantIDInput");
        GameObject sessionGO = FindGO("SessionIDInput");
        GameObject sessionLabel = FindGO("SessionIDLabel");
        GameObject countGO = FindGO("SelectionCountText");
        GameObject panelGO = FindGO("BlockDConfirmPanel");
        GameObject environment = FindGO("BlockD_Food Items");
        GameObject intro = FindGO("BlockDIntroPanel");
        GameObject managerGO = FindGO("BlockD_PlateManager");
        GameObject ui = FindGO("UI");
        if (home == null || participantGO == null || sessionGO == null || countGO == null ||
            panelGO == null || environment == null || managerGO == null || ui == null)
        {
            EditorUtility.DisplayDialog("Scene mismatch", "One or more of the expected HomePanel / Block D UI objects are missing. Nothing changed.", "OK");
            return;
        }
        var participant = participantGO.GetComponent<TMP_InputField>();
        var session = sessionGO.GetComponent<TMP_InputField>();
        var count = countGO.GetComponent<TMP_Text>();
        if (participant == null || session == null || count == null)
        {
            EditorUtility.DisplayDialog("Missing TMP fields", "Existing input/text components were not found. Nothing changed.", "OK");
            return;
        }
        var manager = managerGO.GetComponents<MonoBehaviour>().FirstOrDefault(x => x != null && x.GetType().Name == "BlockDPlateManager");
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Missing Block D manager", "BlockDPlateManager was not found. Nothing changed.", "OK");
            return;
        }
        SerializedObject managerSO = new SerializedObject(manager);
        SerializedProperty foods = managerSO.FindProperty("foods");
        if (foods == null || !foods.isArray || foods.arraySize != 16)
        {
            EditorUtility.DisplayDialog("Unexpected food configuration", "Expected exactly 16 foods on BlockDPlateManager. Nothing changed.", "OK");
            return;
        }
        List<Transform> foodTransforms = new List<Transform>();
        for (int i = 0; i < foods.arraySize; i++)
        {
            UnityEngine.Object o = foods.GetArrayElementAtIndex(i).objectReferenceValue;
            Component c = o as Component;
            GameObject g = o as GameObject;
            Transform t = c != null ? c.transform : (g != null ? g.transform : null);
            if (t == null)
            {
                EditorUtility.DisplayDialog("Unassigned Block D food", "Food entry " + (i + 1) + " is missing. Nothing changed.", "OK");
                return;
            }
            foodTransforms.Add(t);
        }
        if (!EditorUtility.DisplayDialog("Apply v15 participant and counter UI?",
            "Create two-page P001–P100 picker (50 per page); hide Session ID input (keep internal S01); make 0/4 counter independent of the confirmation panel. Existing scripts and food events remain unchanged.", "Apply", "Cancel")) return;

        // Disable the old keypad without removing the component or its serialized references.
        foreach (MonoBehaviour mb in home.GetComponents<MonoBehaviour>())
        {
            if (mb == null || mb.GetType().Name != "VRNumericKeypad") continue;
            Undo.RecordObject(mb, "Disable old numeric keypad");
            mb.enabled = false;
        }
        var picker = home.GetComponent<ImmersiveEyeParticipantPicker>();
        if (picker == null) picker = Undo.AddComponent<ImmersiveEyeParticipantPicker>(home);
        var pickerSO = new SerializedObject(picker);
        SetRef(pickerSO, "participantInput", participant);
        SetRef(pickerSO, "sessionInput", session);
        SetRef(pickerSO, "sessionLabel", sessionLabel);
        pickerSO.FindProperty("fixedSessionId").stringValue = "S01";
        pickerSO.ApplyModifiedProperties();

        // Move existing counter outside the conditionally activated confirmation panel.
        // Keep the same TMP component, so existing BlockDTrialController references remain valid.
        if (count.transform.parent == panelGO.transform)
        {
            Undo.SetTransformParent(count.transform, ui.transform, "Move Block D counter outside confirm panel");
            RectTransform rect = count.transform as RectTransform;
            if (rect != null)
            {
                Undo.RecordObject(rect, "Place independent Block D counter");
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(1005f, -801f);
                rect.sizeDelta = new Vector2(800f, 50f);
                rect.localScale = Vector3.one;
            }
        }
        var hud = ui.GetComponent<ImmersiveEyeBlockDCounterHUD>();
        if (hud == null) hud = Undo.AddComponent<ImmersiveEyeBlockDCounterHUD>(ui);
        var hudSO = new SerializedObject(hud);
        SetRef(hudSO, "environment", environment);
        SetRef(hudSO, "introPanel", intro);
        SetRef(hudSO, "existingCounter", count);
        SetRef(hudSO, "alwaysVisibleCounter", count);
        var tracked = hudSO.FindProperty("trackedFoodTransforms");
        tracked.arraySize = foodTransforms.Count;
        for (int i = 0; i < foodTransforms.Count; i++)
            tracked.GetArrayElementAtIndex(i).objectReferenceValue = foodTransforms[i];
        hudSO.ApplyModifiedProperties();
        Undo.RecordObject(count.gameObject, "Initialize independent Block D counter");
        count.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("IE_V15|UI_CONFIGURED|P001_P100|SESSION_INTERNAL_S01|D_COUNT_0_4");
        EditorUtility.DisplayDialog("v15 UI installed", "Save scene (Cmd+S). In headset select P001–P050, then P051–P100 on page 2. Session is automatically S01; D shows 0/4 immediately when the block begins.", "OK");
    }

    [MenuItem(Menu + "2 - Repair Block A/B food visibility and spacing")]
    public static void FixFoods()
    {
        if (!VerifyScene()) return;
        GameObject a = FindGO("BlockA_Plate");
        GameObject group = FindGO("PlateA_Group");
        if (a == null || group == null)
        {
            EditorUtility.DisplayDialog("Food objects missing", "Expected BlockA_Plate and PlateA_Group. Nothing changed.", "OK");
            return;
        }
        string[] aFoods = { "Protein", "Carbohydrate", "Vegetables", "Fruits", "HealthyFat", "Beverage" };
        var foodObjects = new List<Transform>();
        foreach (string n in aFoods)
        {
            Transform child = a.transform.Find(n);
            if (child == null)
            {
                EditorUtility.DisplayDialog("Unexpected Block A", "Cannot find: " + n + ". Nothing changed.", "OK");
                return;
            }
            foodObjects.Add(child);
        }
        string[] plates = { "PlateA", "PlateB", "PlateC", "PlateD" };
        var bPlates = new List<Transform>();
        foreach (string n in plates)
        {
            Transform p = group.transform.Find(n);
            if (p == null)
            {
                EditorUtility.DisplayDialog("Unexpected Block B", "Cannot find: " + n + ". Nothing changed.", "OK");
                return;
            }
            bPlates.Add(p);
        }
        Transform basePlate = a.transform.Find("Plate_Base");
        if (basePlate == null)
        {
            EditorUtility.DisplayDialog("Missing plate", "Block A Plate_Base missing; no changes made.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Repair food layout?",
            "Re-space the six Block A foods in two rows, adjust the four Block B plates to a wider 2x2 grid, and ensure food sprites are drawn above their plates. Their existing food/AOI objects remain the same. Check the scene visually afterward.",
            "Repair A and B", "Cancel")) return;

        Undo.RecordObject(basePlate, "Block A visibility");
        SpriteRenderer baseSprite = basePlate.GetComponent<SpriteRenderer>();
        if (baseSprite != null)
        {
            Undo.RecordObject(baseSprite, "Block A plate sorting");
            baseSprite.sortingOrder = 0;
        }
        // Spread by measured sprite bounds (before layout), avoiding arbitrary overlap.
        float maxW = 0.28f, maxH = 0.20f;
        foreach (Transform f in foodObjects)
        {
            SpriteRenderer r = f.GetComponent<SpriteRenderer>();
            if (r == null) continue;
            maxW = Mathf.Max(maxW, r.bounds.size.x);
            maxH = Mathf.Max(maxH, r.bounds.size.y);
        }
        float dx = Mathf.Clamp(maxW * 1.12f, 0.38f, 0.68f);
        float dy = Mathf.Clamp(maxH * 1.18f, 0.30f, 0.53f);
        float plateY = basePlate.localPosition.y;
        for (int i = 0; i < foodObjects.Count; i++)
        {
            Transform f = foodObjects[i];
            Undo.RecordObject(f, "Re-space Block A food");
            f.localPosition = new Vector3((i % 3 - 1) * dx,
                plateY + (i < 3 ? dy * 0.5f : -dy * 0.5f),
                basePlate.localPosition.z - 0.05f - 0.001f * i);
            SpriteRenderer r = f.GetComponent<SpriteRenderer>();
            if (r != null)
            {
                Undo.RecordObject(r, "Keep Block A food visible");
                r.sortingOrder = 10 + i;
                r.enabled = true;
            }
        }
        Vector2[] positions = {
            new Vector2(-1.07f, 0.78f), new Vector2(1.07f, 0.78f),
            new Vector2(-1.07f, -0.78f), new Vector2(1.07f, -0.78f)
        };
        for (int i = 0; i < bPlates.Count; i++)
        {
            Transform plate = bPlates[i];
            Undo.RecordObject(plate, "Re-space Block B plate");
            Vector3 p = plate.localPosition;
            plate.localPosition = new Vector3(positions[i].x, positions[i].y, p.z);
            SpriteRenderer plateSprite = plate.GetComponent<SpriteRenderer>();
            if (plateSprite != null)
            {
                Undo.RecordObject(plateSprite, "Plate render order");
                plateSprite.sortingOrder = 0;
            }
            int j = 0;
            foreach (Transform food in plate)
            {
                SpriteRenderer renderer = food.GetComponent<SpriteRenderer>();
                if (renderer == null) continue;
                Undo.RecordObject(renderer, "Keep Block B food visible");
                renderer.sortingOrder = 10 + j++;
                renderer.enabled = true;
            }
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("IE_V15|A_B_FOOD_LAYOUT_REPAIRED|A=6|B=4");
        EditorUtility.DisplayDialog("Food positions updated", "Review A and B in Scene view and in-headset. Cmd+S to keep changes, or Edit > Undo before saving to revert. The installer also backed up the original scene.", "OK");
    }
}
