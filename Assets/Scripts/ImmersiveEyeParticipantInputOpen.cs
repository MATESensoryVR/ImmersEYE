using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Open the existing P001–P100 picker on XR pointer click, even if
/// TMP_InputField is already selected. Does not replace its input handling.</summary>
[DisallowMultipleComponent]
public sealed class ImmersiveEyeParticipantInputOpen : MonoBehaviour, IPointerClickHandler
{
    public ImmersiveEyeParticipantPicker picker;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (picker != null) picker.OpenPicker();
    }
}
