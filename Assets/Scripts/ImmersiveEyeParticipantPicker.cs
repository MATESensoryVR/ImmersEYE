using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-click participant numbers 1..100 in two pages of 50.
/// Session remains internally S01 for backward-compatible CSV/MP4 names.
/// Does not replace HomePageController, consent, or Start Session logic.
/// Attach to existing HomePanel via the v15 Editor menu.
/// </summary>
[DisallowMultipleComponent]
public sealed class ImmersiveEyeParticipantPicker : MonoBehaviour
{
    [SerializeField] private TMP_InputField participantInput;
    [SerializeField] private TMP_InputField sessionInput;
    [SerializeField] private GameObject sessionLabel;
    [SerializeField] private string fixedSessionId = "S01";

    private GameObject panel;
    private TMP_Text title;
    private TMP_Text[] numbers = new TMP_Text[50];
    private Button[] buttons = new Button[50];
    private Button previousButton;
    private Button nextButton;
    private int page = 0;

    private void Awake()
    {
        if (participantInput == null || sessionInput == null)
        {
            Debug.LogError("IE_V15: Missing existing ParticipantIDInput or SessionIDInput", this);
            enabled = false;
            return;
        }
        sessionInput.SetTextWithoutNotify(fixedSessionId);
        sessionInput.text = fixedSessionId;  // notify any HomePageController validation listeners
        sessionInput.gameObject.SetActive(false);
        if (sessionLabel != null) sessionLabel.SetActive(false);
        participantInput.readOnly = true;
        participantInput.shouldHideSoftKeyboard = true;
        participantInput.shouldHideMobileInput = true;
        participantInput.shouldActivateOnSelect = false;
        participantInput.onSelect.AddListener(OnParticipantFieldSelected);
        // IE_PICKER_POPUP_V19_2: Also handle clicks when the input already has focus.
        ImmersiveEyeParticipantInputOpen clicker =
            participantInput.GetComponent<ImmersiveEyeParticipantInputOpen>();
        if (clicker == null) clicker = participantInput.gameObject.AddComponent<ImmersiveEyeParticipantInputOpen>();
        clicker.picker = this;
        CreateUI();
        // Do not silently assign an ID; participant must deliberately select one.
        participantInput.text = "";
    }

    private void Start()
    {
        sessionInput.text = fixedSessionId;
    }

    private void OnParticipantFieldSelected(string unused)
    {
        OpenPicker();
    }

    public void OpenPicker()
    {
        if (!isActiveAndEnabled || panel == null) return;
        // Reopen on the correct page if an existing participant ID was selected.
        string existing = participantInput != null ? participantInput.text : "";
        int id;
        page = existing.Length > 1 && existing[0] == 'P' &&
               int.TryParse(existing.Substring(1), out id) && id > 50 ? 1 : 0;
        Refresh();
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }

    public void ClosePicker()
    {
        if (panel != null) panel.SetActive(false);
        if (participantInput != null) participantInput.DeactivateInputField();
    }

    private void OnDisable()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (participantInput != null) participantInput.onSelect.RemoveListener(OnParticipantFieldSelected);
    }

    private void Choose(int number)
    {
        if (number < 1 || number > 100) return;
        participantInput.text = "P" + number.ToString("D3");
        sessionInput.text = fixedSessionId;
        ClosePicker();
        Debug.Log("IE_V15_PARTICIPANT|" + participantInput.text + "|" + fixedSessionId);
    }

    private void TurnPage(int delta)
    {
        page = Mathf.Clamp(page + delta, 0, 1);
        Refresh();
    }

    private void Refresh()
    {
        title.text = string.Format("SELECT PARTICIPANT  {0:D3}–{1:D3}   ({2}/2)", page * 50 + 1, page * 50 + 50, page + 1);
        for (int i = 0; i < 50; i++)
        {
            numbers[i].text = (page * 50 + i + 1).ToString("D3");
            buttons[i].interactable = true;
        }
        if (previousButton != null) previousButton.interactable = page > 0;
        if (nextButton != null) nextButton.interactable = page < 1;
    }

    private void CreateUI()
    {
        panel = new GameObject("ParticipantPicker_001_100", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(transform, false);
        RectTransform root = (RectTransform)panel.transform;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2(0, -478);
        root.sizeDelta = new Vector2(1080, 450);
        root.localScale = Vector3.one;
        Image bg = panel.GetComponent<Image>();
        bg.color = new Color(0.957f, 0.969f, 0.980f, 1f);
        bg.raycastTarget = true;

        title = Label(root, "PickerTitle", new Vector2(0, 195), new Vector2(1020, 42), 31);
        for (int i = 0; i < 50; ++i)
        {
            int number = i + 1; // refresh changes display; click uses current page
            int row = i / 10, col = i % 10;
            TMP_Text label;
            Button b = MakeButton(root, "ParticipantOption" + number,
                new Vector2(-465 + col * 103.3f, 128 - row * 64),
                new Vector2(93, 54), out label);
            b.onClick.AddListener(() => Choose(page * 50 + number));
            buttons[i] = b;
            numbers[i] = label;
        }
        TMP_Text prevText, nextText;
        previousButton = MakeButton(root, "Previous50", new Vector2(-185, -190), new Vector2(280, 49), out prevText);
        prevText.text = "PREVIOUS 50";
        previousButton.onClick.AddListener(() => TurnPage(-1));
        nextButton = MakeButton(root, "Next50", new Vector2(185, -190), new Vector2(280, 49), out nextText);
        nextText.text = "NEXT 50";
        nextButton.onClick.AddListener(() => TurnPage(1));
        TMP_Text closeText;
        Button closeButton = MakeButton(root, "CloseParticipantPicker",
            new Vector2(474f, 195f), new Vector2(70f, 40f), out closeText);
        closeText.text = "X";
        closeButton.onClick.AddListener(ClosePicker);
        // Don't place a dimming overlay over the HomePanel's other controls.
        Refresh();
        panel.SetActive(false); // The grid appears ONLY after clicking Participant ID.

    }

    private static TMP_Text Label(RectTransform parent, string name, Vector2 pos, Vector2 size, float fontSize)
    {
        GameObject o = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        o.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)o.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        TextMeshProUGUI t = o.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.fontSize = fontSize; t.color = new Color(0.075f, 0.165f, 0.204f, 1f);
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }

    private static Button MakeButton(RectTransform parent, string name, Vector2 pos, Vector2 size, out TMP_Text text)
    {
        GameObject o = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        o.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)o.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        Image bg = o.GetComponent<Image>();
        bg.color = new Color(0.867f, 0.933f, 0.941f, 1f);
        Button b = o.GetComponent<Button>();
        b.targetGraphic = bg;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock tint = b.colors;
        tint.normalColor = Color.white;
        tint.highlightedColor = new Color(0.66f, 0.95f, 0.95f, 1f);
        tint.pressedColor = new Color(0.35f, 0.83f, 0.73f, 1f);
        tint.selectedColor = tint.highlightedColor;
        b.colors = tint;
        text = Label(rt, "Text", Vector2.zero, size - new Vector2(6, 6), size.x > 200 ? 27 : 31);
        return b;
    }
}
