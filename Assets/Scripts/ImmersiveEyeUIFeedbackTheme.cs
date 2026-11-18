using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Visual-only Unity/XR UI feedback for the existing World Space UI Canvas.
/// Uses regular EventSystem pointer events, including XR UI Input Module events.
/// Does not alter onClick/onValueChanged, selection/consent state, raycasts,
/// AOI objects or any experiment/data recorder behaviour.
/// </summary>
[DisallowMultipleComponent]
public sealed class ImmersiveEyeUIFeedbackTheme : MonoBehaviour
{
    [Header("Visual feedback colours")]
    public Color hoverOutline = new Color(0.18f, 0.96f, 1.00f, 1f);
    public Color pressedOutline = new Color(0.25f, 1.00f, 0.55f, 1f);
    public Color checkedOutline = new Color(0.43f, 1.00f, 0.47f, 1f);

    private readonly HashSet<Selectable> themedControls = new HashSet<Selectable>();
    private Coroutine scanRoutine;

    private void OnEnable()
    {
        ApplyTheme();
        if (Application.isPlaying)
            scanRoutine = StartCoroutine(ScanNewControls());
    }

    private void OnDisable()
    {
        if (scanRoutine != null)
        {
            StopCoroutine(scanRoutine);
            scanRoutine = null;
        }
    }

    private IEnumerator ScanNewControls()
    {
        // Runtime participant picker creates 50 buttons during Awake.
        // Check infrequently; no per-frame work during demanding Blocks C/D.
        var delay = new WaitForSecondsRealtime(1.0f);
        while (true)
        {
            yield return delay;
            ApplyTheme();
        }
    }

    /// <summary>Also callable from the Editor to style existing scene controls.</summary>
    public int ApplyTheme()
    {
        int added = 0;
        Selectable[] all = GetComponentsInChildren<Selectable>(true);
        foreach (Selectable control in all)
        {
            if (control == null || !themedControls.Add(control))
                continue;

            // These are tints, preserving the button's original design/background.
            // Unity's Selectable handles normal/hover/down transitions.
            ColorBlock colors = control.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.75f, 1.00f, 0.97f, 1f);
            colors.pressedColor = new Color(0.38f, 0.97f, 0.78f, 1f);
            colors.selectedColor = new Color(0.75f, 1.00f, 0.91f, 1f);
            colors.disabledColor = new Color(0.54f, 0.60f, 0.62f, 0.56f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            control.transition = Selectable.Transition.ColorTint;
            control.colors = colors;

            // Existing scene and new runtime buttons receive identical effects.
            ImmersiveEyeUIPointerGlow glow = control.GetComponent<ImmersiveEyeUIPointerGlow>();
            if (glow == null) glow = control.gameObject.AddComponent<ImmersiveEyeUIPointerGlow>();
            glow.Configure(hoverOutline, pressedOutline, checkedOutline);
            added++;
        }
        return added;
    }
}

/// <summary>
/// Non-blocking visual feedback. Outline is on the existing graphic only;
/// it does not add raycast targets or intercept XR pointer clicks.
/// </summary>
[DisallowMultipleComponent]
public sealed class ImmersiveEyeUIPointerGlow : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    private Selectable selectable;
    private Toggle toggle;
    private Outline outline;
    private Graphic checkMark;
    private Color hover;
    private Color pressed;
    private Color checkedColor;
    private bool hovering;
    private bool holding;
    private Coroutine clickFlash;

    public void Configure(Color hoverColor, Color pressedColor, Color checkedTint)
    {
        hover = hoverColor;
        pressed = pressedColor;
        checkedColor = checkedTint;
        selectable = GetComponent<Selectable>();
        toggle = GetComponent<Toggle>();

        if (selectable != null && selectable.targetGraphic != null)
        {
            // Outline is drawn on the button/checkbox box, not its text.
            GameObject target = selectable.targetGraphic.gameObject;
            outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            outline.useGraphicAlpha = false;
        }

        if (toggle != null)
        {
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
            toggle.onValueChanged.AddListener(OnToggleChanged);
            checkMark = toggle.graphic;
            if (checkMark != null) checkMark.color = checkedColor;
        }
        Refresh();
    }

    private void OnDestroy()
    {
        if (toggle != null) toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void OnDisable()
    {
        hovering = false;
        holding = false;
        if (clickFlash != null)
        {
            StopCoroutine(clickFlash);
            clickFlash = null;
        }
        Refresh();
    }

    private void OnToggleChanged(bool _) => Refresh();

    private void Refresh()
    {
        if (outline == null) return;
        bool on = toggle != null && toggle.isOn;
        bool active = selectable == null || selectable.IsInteractable();
        outline.enabled = active && (holding || hovering || on);
        outline.effectColor = holding ? pressed : on ? checkedColor : hover;
    }

    public void OnPointerEnter(PointerEventData e) { hovering = true; Refresh(); }
    public void OnPointerExit(PointerEventData e) { hovering = false; holding = false; Refresh(); }
    public void OnPointerDown(PointerEventData e) { holding = true; Refresh(); }
    public void OnPointerUp(PointerEventData e) { holding = false; Refresh(); }

    public void OnPointerClick(PointerEventData e)
    {
        if (clickFlash != null) StopCoroutine(clickFlash);
        clickFlash = StartCoroutine(BriefPressFlash());
    }

    private IEnumerator BriefPressFlash()
    {
        holding = true;
        Refresh();
        yield return new WaitForSecondsRealtime(0.16f);
        holding = false;
        clickFlash = null;
        Refresh();
    }
}
