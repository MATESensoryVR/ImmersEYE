using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;


public class ExperimentControlUI : MonoBehaviour
{
    // ============================================================
    // RECORDER
    // ============================================================

    [Header("Recorder")]
    public ImmersiveEyeRecorder recorder;


    // ============================================================
    // UI REFERENCES
    // ============================================================

    private TMP_InputField studyInput;
    private TMP_InputField participantInput;
    private TMP_InputField sessionInput;

    private TMP_InputField trialInput;
    private TMP_InputField conditionInput;
    private TMP_InputField stimulusInput;

    private TMP_InputField eventTypeInput;
    private TMP_InputField eventNameInput;
    private TMP_InputField eventValueInput;

    private TextMeshProUGUI statusText;

    private Button startButton;
    private Button eventButton;
    private Button recalibrationButton;
    private Button stopButton;


    // ============================================================
    // COLORS
    // ============================================================

    private readonly Color pageBackground =
        new Color32(228, 235, 232, 255);

    private readonly Color panelColor =
        new Color32(248, 250, 249, 255);

    private readonly Color greenColor =
        new Color32(0, 122, 94, 255);

    private readonly Color darkGreenColor =
        new Color32(0, 88, 68, 255);

    private readonly Color textColor =
        new Color32(30, 37, 39, 255);

    private readonly Color inputColor =
        new Color32(255, 255, 255, 255);

    private readonly Color disabledColor =
        new Color32(175, 180, 180, 255);


    // ============================================================
    // UNITY
    // ============================================================

    private void Start()
    {
        FindRecorder();

        CreateEventSystemIfNeeded();

        BuildInterface();

        RefreshUI();
    }


    private void Update()
    {
        RefreshUI();
    }


    // ============================================================
    // FIND RECORDER
    // ============================================================

    private void FindRecorder()
    {
        if (recorder != null)
        {
            return;
        }

        recorder =
            FindAnyObjectByType<ImmersiveEyeRecorder>();

        if (recorder == null)
        {
            Debug.LogError(
                "ExperimentControlUI: ImmersiveEyeRecorder not found."
            );
        }
    }


    // ============================================================
    // EVENT SYSTEM
    // ============================================================

    private void CreateEventSystemIfNeeded()
    {
        EventSystem existing =
            FindAnyObjectByType<EventSystem>();

        if (existing != null)
        {
            return;
        }

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule)
        );
    }


    // ============================================================
    // BUILD INTERFACE
    // ============================================================

    private void BuildInterface()
    {
        Camera mainCamera =
            Camera.main;


        // ========================================================
        // CANVAS
        // ========================================================

        GameObject canvasObject =
            new GameObject(
                "ExperimentCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );


        Canvas canvas =
            canvasObject.GetComponent<Canvas>();


        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();


#if UNITY_EDITOR

        // ========================================================
        // LARGE DESKTOP TEST UI
        // ========================================================

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;


        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;


        scaler.referenceResolution =
            new Vector2(
                1600f,
                1000f
            );


        scaler.matchWidthOrHeight =
            0.5f;


        RectTransform desktopCanvasRect =
            canvasObject.GetComponent<RectTransform>();


        desktopCanvasRect.anchorMin =
            Vector2.zero;


        desktopCanvasRect.anchorMax =
            Vector2.one;


        desktopCanvasRect.offsetMin =
            Vector2.zero;


        desktopCanvasRect.offsetMax =
            Vector2.zero;

#else

        // ========================================================
        // FOCUS VISION VR UI
        // ========================================================

        canvas.renderMode =
            RenderMode.WorldSpace;


        canvas.worldCamera =
            mainCamera;


        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ConstantPixelSize;


        RectTransform vrCanvasRect =
            canvasObject.GetComponent<RectTransform>();


        vrCanvasRect.sizeDelta =
            new Vector2(
                1400f,
                900f
            );


        vrCanvasRect.localScale =
            Vector3.one * 0.00115f;


        if (mainCamera != null)
        {
            canvasObject.transform.position =
                mainCamera.transform.position
                +
                mainCamera.transform.forward * 1.15f;


            canvasObject.transform.rotation =
                mainCamera.transform.rotation;
        }
        else
        {
            canvasObject.transform.position =
                new Vector3(
                    0f,
                    1.5f,
                    1.15f
                );


            canvasObject.transform.rotation =
                Quaternion.identity;
        }

#endif


        canvas.sortingOrder =
            100;


        // ========================================================
        // PAGE BACKGROUND
        // ========================================================

        GameObject background =
            CreateUIObject(
                "Background",
                canvasObject.transform
            );


        RectTransform backgroundRect =
            background.GetComponent<RectTransform>();


        Stretch(
            backgroundRect
        );


        Image backgroundImage =
            background.AddComponent<Image>();


        backgroundImage.color =
            pageBackground;


        // ========================================================
        // MAIN PANEL
        // ========================================================

        GameObject panel =
            CreateUIObject(
                "MainPanel",
                background.transform
            );


        RectTransform panelRect =
            panel.GetComponent<RectTransform>();


#if UNITY_EDITOR

        panelRect.anchorMin =
            new Vector2(
                0.5f,
                0.5f
            );


        panelRect.anchorMax =
            new Vector2(
                0.5f,
                0.5f
            );


        panelRect.pivot =
            new Vector2(
                0.5f,
                0.5f
            );


        panelRect.sizeDelta =
            new Vector2(
                1450f,
                900f
            );


        panelRect.anchoredPosition =
            Vector2.zero;

#else

        Stretch(
            panelRect
        );

#endif


        Image panelImage =
            panel.AddComponent<Image>();


        panelImage.color =
            panelColor;


        VerticalLayoutGroup mainLayout =
            panel.AddComponent<VerticalLayoutGroup>();


        mainLayout.padding =
            new RectOffset(
                45,
                45,
                28,
                28
            );


        mainLayout.spacing =
            12;


        mainLayout.childControlWidth =
            true;


        mainLayout.childControlHeight =
            false;


        mainLayout.childForceExpandWidth =
            true;


        mainLayout.childForceExpandHeight =
            false;


        // ========================================================
        // HEADER
        // ========================================================

        TextMeshProUGUI title =
            CreateText(
                panel.transform,
                "ImmersiveEye Recorder",
                38,
                FontStyles.Bold
            );


        title.color =
            darkGreenColor;


        title.alignment =
            TextAlignmentOptions.Center;


        SetHeight(
            title.gameObject,
            48
        );


        TextMeshProUGUI subtitle =
            CreateText(
                panel.transform,
                "MATE Digital Sensory Science",
                19,
                FontStyles.Normal
            );


        subtitle.color =
            textColor;


        subtitle.alignment =
            TextAlignmentOptions.Center;


        SetHeight(
            subtitle.gameObject,
            30
        );


        // ========================================================
        // TWO COLUMN AREA
        // ========================================================

        GameObject columns =
            CreateUIObject(
                "Columns",
                panel.transform
            );


        SetHeight(
            columns,
            590
        );


        HorizontalLayoutGroup columnsLayout =
            columns.AddComponent<HorizontalLayoutGroup>();


        columnsLayout.spacing =
            35;


        columnsLayout.childControlWidth =
            true;


        columnsLayout.childControlHeight =
            true;


        columnsLayout.childForceExpandWidth =
            true;


        columnsLayout.childForceExpandHeight =
            true;


        // ========================================================
        // LEFT COLUMN
        // ========================================================

        GameObject leftColumn =
            CreateUIObject(
                "LeftColumn",
                columns.transform
            );


        VerticalLayoutGroup leftLayout =
            leftColumn.AddComponent<VerticalLayoutGroup>();


        leftLayout.spacing =
            8;


        leftLayout.childControlWidth =
            true;


        leftLayout.childControlHeight =
            false;


        leftLayout.childForceExpandWidth =
            true;


        leftLayout.childForceExpandHeight =
            false;


        // --------------------------------------------------------
        // SESSION
        // --------------------------------------------------------

        CreateSectionTitle(
            leftColumn.transform,
            "SESSION"
        );


        studyInput =
            CreateInputField(
                leftColumn.transform,
                "Study ID",
                recorder != null
                    ? recorder.studyId
                    : "VRET_HTC"
            );


        participantInput =
            CreateInputField(
                leftColumn.transform,
                "Participant ID",
                recorder != null
                    ? recorder.participantId
                    : "P001"
            );


        sessionInput =
            CreateInputField(
                leftColumn.transform,
                "Session ID",
                recorder != null
                    ? recorder.sessionId
                    : "S01"
            );


        // --------------------------------------------------------
        // TRIAL CONTEXT
        // --------------------------------------------------------

        CreateSectionTitle(
            leftColumn.transform,
            "TRIAL CONTEXT"
        );


        trialInput =
            CreateInputField(
                leftColumn.transform,
                "Trial ID",
                recorder != null
                    ? recorder.trialId
                    : "T01"
            );


        conditionInput =
            CreateInputField(
                leftColumn.transform,
                "Condition",
                recorder != null
                    ? recorder.condition
                    : "Control"
            );


        stimulusInput =
            CreateInputField(
                leftColumn.transform,
                "Stimulus ID",
                recorder != null
                    ? recorder.stimulusId
                    : ""
            );


        // ========================================================
        // RIGHT COLUMN
        // ========================================================

        GameObject rightColumn =
            CreateUIObject(
                "RightColumn",
                columns.transform
            );


        VerticalLayoutGroup rightLayout =
            rightColumn.AddComponent<VerticalLayoutGroup>();


        rightLayout.spacing =
            8;


        rightLayout.childControlWidth =
            true;


        rightLayout.childControlHeight =
            false;


        rightLayout.childForceExpandWidth =
            true;


        rightLayout.childForceExpandHeight =
            false;


        // --------------------------------------------------------
        // EVENT MARKER
        // --------------------------------------------------------

        CreateSectionTitle(
            rightColumn.transform,
            "EVENT MARKER"
        );


        eventTypeInput =
            CreateInputField(
                rightColumn.transform,
                "Event Type",
                "event"
            );


        eventNameInput =
            CreateInputField(
                rightColumn.transform,
                "Event Name",
                "marker"
            );


        eventValueInput =
            CreateInputField(
                rightColumn.transform,
                "Event Value",
                ""
            );


        // --------------------------------------------------------
        // INFO
        // --------------------------------------------------------

        CreateSectionTitle(
            rightColumn.transform,
            "EXAMPLES"
        );


        TextMeshProUGUI exampleText =
            CreateText(
                rightColumn.transform,

                "Event Type: stimulus\n"
                +
                "Event Name: presentation_start\n"
                +
                "Event Value: Sugar10\n\n"
                +
                "Trial and stimulus can be changed while recording.",

                17,
                FontStyles.Normal
            );


        exampleText.color =
            textColor;


        exampleText.alignment =
            TextAlignmentOptions.TopLeft;


        exampleText.textWrappingMode =
            TextWrappingModes.Normal;


        SetHeight(
            exampleText.gameObject,
            165
        );


        // ========================================================
        // LISTEN FOR CONTEXT CHANGES
        // ========================================================

        trialInput.onValueChanged.AddListener(
            delegate
            {
                ApplyTrialContext();
            }
        );


        conditionInput.onValueChanged.AddListener(
            delegate
            {
                ApplyTrialContext();
            }
        );


        stimulusInput.onValueChanged.AddListener(
            delegate
            {
                ApplyTrialContext();
            }
        );


        // ========================================================
        // STATUS
        // ========================================================

        statusText =
            CreateText(
                panel.transform,
                "READY",
                22,
                FontStyles.Bold
            );


        statusText.color =
            textColor;


        statusText.alignment =
            TextAlignmentOptions.Center;


        SetHeight(
            statusText.gameObject,
            35
        );


        // ========================================================
        // BUTTON ROW
        // ========================================================

        GameObject buttonRow =
            CreateUIObject(
                "ButtonRow",
                panel.transform
            );


        SetHeight(
            buttonRow,
            75
        );


        HorizontalLayoutGroup buttonLayout =
            buttonRow.AddComponent<HorizontalLayoutGroup>();


        buttonLayout.spacing =
            18;


        buttonLayout.childControlWidth =
            true;


        buttonLayout.childControlHeight =
            true;


        buttonLayout.childForceExpandWidth =
            true;


        buttonLayout.childForceExpandHeight =
            true;


        startButton =
            CreateButton(
                buttonRow.transform,
                "START SESSION",
                OnStartSession
            );


        eventButton =
            CreateButton(
                buttonRow.transform,
                "MARK EVENT",
                OnMarkEvent
            );


        recalibrationButton =
            CreateButton(
                buttonRow.transform,
                "RECALIBRATION",
                OnRecalibration
            );


        stopButton =
            CreateButton(
                buttonRow.transform,
                "STOP SESSION",
                OnStopSession
            );


        RefreshUI();
    }


    // ============================================================
    // START SESSION
    // ============================================================

    private void OnStartSession()
    {
        if (recorder == null)
        {
            Debug.LogError(
                "Cannot start session: recorder missing."
            );

            return;
        }


        if (recorder.IsRecording)
        {
            return;
        }


        recorder.studyId =
            CleanValue(
                studyInput.text,
                "VRET_HTC"
            );


        recorder.participantId =
            CleanValue(
                participantInput.text,
                "P001"
            );


        recorder.sessionId =
            CleanValue(
                sessionInput.text,
                "S01"
            );


        ApplyTrialContext();


        recorder.StartRecording();


        if (recorder.IsRecording)
        {
            recorder.MarkEvent(
                "session",
                "start",
                ""
            );
        }


        RefreshUI();
    }


    // ============================================================
    // APPLY TRIAL CONTEXT
    // ============================================================

    private void ApplyTrialContext()
    {
        if (
            recorder == null
            ||
            trialInput == null
            ||
            conditionInput == null
            ||
            stimulusInput == null
        )
        {
            return;
        }


        recorder.SetTrialContext(
            CleanValue(
                trialInput.text,
                "T01"
            ),

            CleanValue(
                conditionInput.text,
                "Control"
            ),

            CleanOptionalValue(
                stimulusInput.text
            )
        );
    }


    // ============================================================
    // MARK EVENT
    // ============================================================

    private void OnMarkEvent()
    {
        if (
            recorder == null
            ||
            !recorder.IsRecording
        )
        {
            return;
        }


        ApplyTrialContext();


        string eventType =
            CleanValue(
                eventTypeInput.text,
                "event"
            );


        string eventName =
            CleanValue(
                eventNameInput.text,
                "marker"
            );


        string eventValue =
            CleanOptionalValue(
                eventValueInput.text
            );


        recorder.MarkEvent(
            eventType,
            eventName,
            eventValue
        );
    }


    // ============================================================
    // RECALIBRATION
    // ============================================================

    private void OnRecalibration()
    {
        if (
            recorder == null
            ||
            !recorder.IsRecording
        )
        {
            return;
        }


        ApplyTrialContext();


        recorder.MarkEvent(
            "calibration",
            "recalibration_requested",
            ""
        );
    }


    // ============================================================
    // STOP SESSION
    // ============================================================

    private void OnStopSession()
    {
        if (
            recorder == null
            ||
            !recorder.IsRecording
        )
        {
            return;
        }


        recorder.StopRecording();


        RefreshUI();
    }


    // ============================================================
    // REFRESH UI
    // ============================================================

    private void RefreshUI()
    {
        if (
            recorder == null
            ||
            startButton == null
        )
        {
            return;
        }


        bool recording =
            recorder.IsRecording;


        startButton.interactable =
            !recording;


        eventButton.interactable =
            recording;


        recalibrationButton.interactable =
            recording;


        stopButton.interactable =
            recording;


        studyInput.interactable =
            !recording;


        participantInput.interactable =
            !recording;


        sessionInput.interactable =
            !recording;


        trialInput.interactable =
            true;


        conditionInput.interactable =
            true;


        stimulusInput.interactable =
            true;


        eventTypeInput.interactable =
            recording;


        eventNameInput.interactable =
            recording;


        eventValueInput.interactable =
            recording;


        if (recording)
        {
            statusText.text =
                "RECORDING"
                +
                "   |   "
                +
                recorder.SamplesWritten
                +
                " samples"
                +
                "   |   "
                +
                recorder.trialId
                +
                "   |   "
                +
                recorder.condition;


            statusText.color =
                greenColor;
        }
        else
        {
            statusText.text =
                "READY";


            statusText.color =
                textColor;
        }
    }


    // ============================================================
    // SECTION TITLE
    // ============================================================

    private void CreateSectionTitle(
        Transform parent,
        string value
    )
    {
        TextMeshProUGUI section =
            CreateText(
                parent,
                value,
                18,
                FontStyles.Bold
            );


        section.color =
            greenColor;


        section.alignment =
            TextAlignmentOptions.MidlineLeft;


        SetHeight(
            section.gameObject,
            32
        );
    }


    // ============================================================
    // INPUT FIELD
    // ============================================================

    private TMP_InputField CreateInputField(
        Transform parent,
        string label,
        string defaultValue
    )
    {
        GameObject container =
            CreateUIObject(
                label + "_Container",
                parent
            );


        SetHeight(
            container,
            82
        );


        VerticalLayoutGroup layout =
            container.AddComponent<VerticalLayoutGroup>();


        layout.spacing =
            4;


        layout.childControlWidth =
            true;


        layout.childControlHeight =
            false;


        layout.childForceExpandWidth =
            true;


        layout.childForceExpandHeight =
            false;


        // ========================================================
        // LABEL
        // ========================================================

        TextMeshProUGUI labelText =
            CreateText(
                container.transform,
                label,
                18,
                FontStyles.Bold
            );


        labelText.color =
            textColor;


        labelText.alignment =
            TextAlignmentOptions.MidlineLeft;


        SetHeight(
            labelText.gameObject,
            28
        );


        // ========================================================
        // FIELD
        // ========================================================

        GameObject fieldObject =
            CreateUIObject(
                label + "_Input",
                container.transform
            );


        Image fieldBackground =
            fieldObject.AddComponent<Image>();


        fieldBackground.color =
            inputColor;


        SetHeight(
            fieldObject,
            50
        );


        TMP_InputField inputField =
            fieldObject.AddComponent<TMP_InputField>();


        // ========================================================
        // TEXT AREA
        // ========================================================

        GameObject textArea =
            CreateUIObject(
                "Text Area",
                fieldObject.transform
            );


        RectTransform textAreaRect =
            textArea.GetComponent<RectTransform>();


        textAreaRect.anchorMin =
            Vector2.zero;


        textAreaRect.anchorMax =
            Vector2.one;


        textAreaRect.offsetMin =
            new Vector2(
                14,
                4
            );


        textAreaRect.offsetMax =
            new Vector2(
                -14,
                -4
            );


        // ========================================================
        // INPUT TEXT
        // ========================================================

        TextMeshProUGUI inputText =
            CreateText(
                textArea.transform,
                defaultValue,
                22,
                FontStyles.Normal
            );


        inputText.color =
            textColor;


        inputText.alignment =
            TextAlignmentOptions.MidlineLeft;


        inputText.textWrappingMode =
            TextWrappingModes.NoWrap;


        Stretch(
            inputText.rectTransform
        );


        // ========================================================
        // PLACEHOLDER
        // ========================================================

        TextMeshProUGUI placeholder =
            CreateText(
                textArea.transform,
                label,
                22,
                FontStyles.Italic
            );


        placeholder.color =
            new Color(
                0.4f,
                0.4f,
                0.4f,
                0.45f
            );


        placeholder.alignment =
            TextAlignmentOptions.MidlineLeft;


        placeholder.textWrappingMode =
            TextWrappingModes.NoWrap;


        Stretch(
            placeholder.rectTransform
        );


        // ========================================================
        // INPUT FIELD SETTINGS
        // ========================================================

        inputField.textViewport =
            textAreaRect;


        inputField.textComponent =
            inputText;


        inputField.placeholder =
            placeholder;


        inputField.text =
            defaultValue;


        inputField.lineType =
            TMP_InputField.LineType.SingleLine;


        inputField.selectionColor =
            new Color32(
                0,
                122,
                94,
                90
            );


        return inputField;
    }


    // ============================================================
    // BUTTON
    // ============================================================

    private Button CreateButton(
        Transform parent,
        string label,
        UnityEngine.Events.UnityAction action
    )
    {
        GameObject buttonObject =
            CreateUIObject(
                label,
                parent
            );


        Image image =
            buttonObject.AddComponent<Image>();


        image.color =
            greenColor;


        Button button =
            buttonObject.AddComponent<Button>();


        ColorBlock colors =
            button.colors;


        colors.normalColor =
            greenColor;


        colors.highlightedColor =
            darkGreenColor;


        colors.pressedColor =
            darkGreenColor;


        colors.selectedColor =
            greenColor;


        colors.disabledColor =
            disabledColor;


        button.colors =
            colors;


        TextMeshProUGUI buttonText =
            CreateText(
                buttonObject.transform,
                label,
                18,
                FontStyles.Bold
            );


        buttonText.color =
            Color.white;


        buttonText.alignment =
            TextAlignmentOptions.Center;


        buttonText.textWrappingMode =
            TextWrappingModes.NoWrap;


        Stretch(
            buttonText.rectTransform
        );


        button.onClick.AddListener(
            action
        );


        return button;
    }


    // ============================================================
    // CREATE TEXT
    // ============================================================

    private TextMeshProUGUI CreateText(
        Transform parent,
        string value,
        float fontSize,
        FontStyles style
    )
    {
        GameObject textObject =
            CreateUIObject(
                "Text",
                parent
            );


        TextMeshProUGUI text =
            textObject.AddComponent<TextMeshProUGUI>();


        text.text =
            value;


        text.fontSize =
            fontSize;


        text.fontStyle =
            style;


        text.textWrappingMode =
            TextWrappingModes.NoWrap;


        return text;
    }


    // ============================================================
    // CREATE UI OBJECT
    // ============================================================

    private GameObject CreateUIObject(
        string objectName,
        Transform parent
    )
    {
        GameObject instance =
            new GameObject(
                objectName,
                typeof(RectTransform)
            );


        instance.transform.SetParent(
            parent,
            false
        );


        return instance;
    }


    // ============================================================
    // STRETCH
    // ============================================================

    private void Stretch(
        RectTransform rect
    )
    {
        rect.anchorMin =
            Vector2.zero;


        rect.anchorMax =
            Vector2.one;


        rect.offsetMin =
            Vector2.zero;


        rect.offsetMax =
            Vector2.zero;
    }


    // ============================================================
    // HEIGHT
    // ============================================================

    private void SetHeight(
        GameObject target,
        float height
    )
    {
        LayoutElement element =
            target.GetComponent<LayoutElement>();


        if (element == null)
        {
            element =
                target.AddComponent<LayoutElement>();
        }


        element.minHeight =
            height;


        element.preferredHeight =
            height;


        element.flexibleHeight =
            0;
    }


    // ============================================================
    // CLEAN REQUIRED VALUE
    // ============================================================

    private string CleanValue(
        string value,
        string fallback
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }


        return value.Trim();
    }


    // ============================================================
    // CLEAN OPTIONAL VALUE
    // ============================================================

    private string CleanOptionalValue(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }


        return value.Trim();
    }
}