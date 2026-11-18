using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VR-friendly numeric keypad for the existing HomePanel's ParticipantIDInput and
/// SessionIDInput. Add to the active HomePanel (NOT an inactive keypad panel).
/// It creates its own World Space Canvas UI at runtime, compatible with XR UI rays.
/// It deliberately does not change HomePageController or its registration events.
/// </summary>
[DisallowMultipleComponent]
public sealed class VRNumericKeypad : MonoBehaviour
{
    [Header("Existing HomePanel Fields")]
    [SerializeField] private TMP_InputField participantIDInput;
    [SerializeField] private TMP_InputField sessionIDInput;

    [Header("Keypad Settings")]
    [SerializeField, Range(1, 12)] private int maxDigits = 8;

    private GameObject keypadPanel;
    private TMP_Text headingText;
    private TMP_Text valueText;
    private TMP_InputField activeInput;
    private string prefix;
    private string digits = "";

    private void Awake()
    {
        if (participantIDInput == null)
            participantIDInput = FindInput("ParticipantIDInput");
        if (sessionIDInput == null)
            sessionIDInput = FindInput("SessionIDInput");

        if (participantIDInput == null || sessionIDInput == null)
        {
            Debug.LogError("VRNumericKeypad: Assign both existing TMP input fields on HomePanel.", this);
            enabled = false;
            return;
        }

        ConfigureInput(participantIDInput);
        ConfigureInput(sessionIDInput);

        participantIDInput.onSelect.AddListener(OnParticipantSelected);
        sessionIDInput.onSelect.AddListener(OnSessionSelected);

        CreateKeypad();
    }

    private TMP_InputField FindInput(string objectName)
    {
        foreach (TMP_InputField input in GetComponentsInChildren<TMP_InputField>(true))
            if (input.gameObject.name == objectName)
                return input;
        return null;
    }

    private static void ConfigureInput(TMP_InputField input)
    {
        // The controller selects the field; only our keypad edits its text.
        input.readOnly = true;
        input.shouldHideSoftKeyboard = true;
        input.shouldHideMobileInput = true;
        input.shouldActivateOnSelect = false;
    }

    private void OnParticipantSelected(string _) => Open(participantIDInput, "P", "Participant ID");
    private void OnSessionSelected(string _) => Open(sessionIDInput, "S", "Session ID");

    // Optional public methods if you later prefer an explicit button over input selection.
    public void OpenParticipant() => Open(participantIDInput, "P", "Participant ID");
    public void OpenSession() => Open(sessionIDInput, "S", "Session ID");

    private void Open(TMP_InputField input, string selectedPrefix, string heading)
    {
        if (input == null || keypadPanel == null) return;

        activeInput = input;
        prefix = selectedPrefix;
        digits = ExtractDigits(input.text);
        if (digits.Length > maxDigits)
            digits = digits.Substring(0, maxDigits);

        headingText.text = heading;
        RefreshDisplay();
        keypadPanel.SetActive(true);
        keypadPanel.transform.SetAsLastSibling();
    }

    private static string ExtractDigits(string existing)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrEmpty(existing))
            foreach (char c in existing)
                if (c >= '0' && c <= '9') builder.Append(c);
        return builder.ToString();
    }

    private void PressDigit(int digit)
    {
        if (activeInput == null || digits.Length >= maxDigits) return;
        digits += digit.ToString();
        ApplyValue();
    }

    private void Backspace()
    {
        if (activeInput == null || digits.Length == 0) return;
        digits = digits.Substring(0, digits.Length - 1);
        ApplyValue();
    }

    private void Clear()
    {
        if (activeInput == null) return;
        digits = "";
        ApplyValue();
    }

    private void ApplyValue()
    {
        // Empty value, not bare "P"/"S": avoids enabling Start Session without digits.
        activeInput.text = digits.Length == 0 ? "" : prefix + digits;
        RefreshDisplay();
        // Using .text invokes HomePageController's existing onValueChanged listener.
    }

    private void RefreshDisplay()
    {
        if (valueText != null)
            valueText.text = prefix + (digits.Length == 0 ? "_" : digits);
    }

    private void Done()
    {
        if (activeInput == null) return;
        // When blank, leave the keypad open until an ID is entered.
        if (digits.Length == 0) return;
        keypadPanel.SetActive(false);
        activeInput = null;
    }

    private void OnDisable()
    {
        if (keypadPanel != null) keypadPanel.SetActive(false);
        activeInput = null;
    }

    private void OnDestroy()
    {
        if (participantIDInput != null)
            participantIDInput.onSelect.RemoveListener(OnParticipantSelected);
        if (sessionIDInput != null)
            sessionIDInput.onSelect.RemoveListener(OnSessionSelected);
        if (keypadPanel != null) Destroy(keypadPanel);
    }

    private void CreateKeypad()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("VRNumericKeypad: HomePanel must be under the existing UI Canvas.", this);
            return;
        }

        keypadPanel = new GameObject("VRNumericKeypad_Runtime", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        keypadPanel.transform.SetParent(canvas.transform, false);

        RectTransform root = (RectTransform)keypadPanel.transform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(540f, 780f);
        root.anchoredPosition = new Vector2(540f, 0f);
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;

        Image background = keypadPanel.GetComponent<Image>();
        background.color = new Color(0.08f, 0.13f, 0.17f, 0.98f);
        background.raycastTarget = true;

        headingText = CreateLabel(root, "KeypadHeading", new Vector2(0f, 326f),
            new Vector2(480f, 60f), 40, Color.white);
        valueText = CreateLabel(root, "KeypadValue", new Vector2(0f, 247f),
            new Vector2(470f, 76f), 51, Color.white);

        string[,] keys = {
            { "1", "2", "3" },
            { "4", "5", "6" },
            { "7", "8", "9" },
            { "Clear", "0", "DEL" }
        };

        for (int row = 0; row < 4; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                string key = keys[row, col];
                Button button = CreateButton(root, "Key_" + key,
                    new Vector2(-167f + col * 167f, 144f - row * 106f),
                    new Vector2(148f, 90f), key, new Color(0.24f, 0.32f, 0.37f, 1f));

                if (key == "Clear") button.onClick.AddListener(Clear);
                else if (key == "DEL") button.onClick.AddListener(Backspace);
                else
                {
                    int capturedDigit = int.Parse(key);
                    button.onClick.AddListener(() => PressDigit(capturedDigit));
                }
            }
        }

        Button doneButton = CreateButton(root, "Key_Done", new Vector2(0f, -309f),
            new Vector2(475f, 80f), "DONE", new Color(0.05f, 0.47f, 0.37f, 1f));
        doneButton.onClick.AddListener(Done);
        keypadPanel.SetActive(false);
    }

    private static TMP_Text CreateLabel(RectTransform parent, string name,
        Vector2 position, Vector2 size, int fontSize, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    private static Button CreateButton(RectTransform parent, string name,
        Vector2 position, Vector2 size, string label, Color backgroundColor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;

        Image img = go.GetComponent<Image>();
        img.color = backgroundColor;
        img.raycastTarget = true;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = img;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        TMP_Text text = CreateLabel(rt, "Label", Vector2.zero,
            size - new Vector2(12f, 12f), label.Length > 1 ? 31 : 44, Color.white);
        text.text = label;
        return button;
    }
}
