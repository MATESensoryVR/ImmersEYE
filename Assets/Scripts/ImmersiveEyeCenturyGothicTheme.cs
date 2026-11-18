using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Applies a user-provided TMP Century Gothic font to UI elements that are
/// instantiated at runtime (participant picker, counter, dynamic dialogs).
/// Does not alter UI event handlers, experiment state, AOIs or recording.
/// Attach only to a persistent UI Canvas, via the v19 Editor menu.
/// </summary>
[DisallowMultipleComponent]
public sealed class ImmersiveEyeCenturyGothicTheme : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset centuryGothicFont;

    public void SetFont(TMP_FontAsset font)
    {
        centuryGothicFont = font;
        ApplyToChildren();
    }

    private void OnEnable()
    {
        ApplyToChildren();
        if (Application.isPlaying)
            StartCoroutine(ApplyDuringInitialUISetup());
    }

    private IEnumerator ApplyDuringInitialUISetup()
    {
        // Catch UI instantiated by HomePanel/participant picker Awake and Start.
        yield return null;
        ApplyToChildren();
        for (int i = 0; i < 6; i++)
        {
            yield return new WaitForSecondsRealtime(1.0f);
            ApplyToChildren();
        }
    }

    private void ApplyToChildren()
    {
        if (centuryGothicFont == null) return;
        TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in labels)
        {
            if (label != null && label.font != centuryGothicFont)
                label.font = centuryGothicFont;
        }
    }
}
