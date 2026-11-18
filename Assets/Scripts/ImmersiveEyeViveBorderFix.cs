using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Temporary HTC VIVE Focus Vision / Unity OpenXR Android viewport-border workaround.
/// Based on HTC VIVE OpenXR's documented RemoveRenderMask workaround.
/// Attach to an active-at-start GameObject such as XR Origin (VR).
/// Does not change tracking, camera transforms, eye-tracking, food models, or interaction.
/// IMPORTANT: Turning off the occlusion mesh may reduce GPU performance.
/// Remove/disable this component and rebuild if performance worsens or the border persists.
/// </summary>
public sealed class ImmersiveEyeViveBorderFix : MonoBehaviour
{
    private Coroutine applyCoroutine;

    private void OnEnable()
    {
        applyCoroutine = StartCoroutine(ApplyAfterXRStarts());
    }

    private void OnDisable()
    {
        if (applyCoroutine != null)
        {
            StopCoroutine(applyCoroutine);
            applyCoroutine = null;
        }
    }

    private IEnumerator ApplyAfterXRStarts()
    {
        var displays = new List<XRDisplaySubsystem>();
        while (true)
        {
            displays.Clear();
            SubsystemManager.GetSubsystems(displays);
            bool running = false;
            foreach (var display in displays)
            {
                if (display != null && display.running)
                {
                    running = true;
                    break;
                }
            }
            if (running)
                break;
            yield return null;
        }

        XRSettings.occlusionMaskScale = 0f;
        XRSettings.useOcclusionMesh = false;
        Debug.Log("IMMERSIVEEYE_VIVE_BORDER_FIX|applied=true|occlusionMaskScale=0|useOcclusionMesh=false");
        applyCoroutine = null;
    }
}
