using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Runtime-only optimisations for expensive Block C/D stereoscopic rendering.
/// Does not modify gaze data or the dedicated GPU MP4 encoder/camera settings.
/// Uses URP-compatible XR viewport scaling to lower the *headset* GPU workload,
/// and asks the active XR runtime for optional fixed foveation.
/// These are performance hints, not guarantees of 90 unique gaze samples/second.
/// </summary>
[DefaultExecutionOrder(1400)]
[DisallowMultipleComponent]
public sealed class ImmersiveEyeXR90Performance : MonoBehaviour
{
    [Header("Block C/D XR rendering only — NOT MP4 settings")]
    [SerializeField] private bool enableComplexBlockOptimisation = true;
    [Tooltip("XR headset viewport scale in Blocks C/D; 1.00 keeps original sharpness, 0.90 offers mild performance savings. MP4 recording is unaffected.")]
    [SerializeField, Range(0.50f, 1.00f)] private float complexBlockXRViewportScale = 1.00f;
    [Tooltip("If the device/plugin supports fixed foveated rendering and it is enabled in OpenXR settings, request this strength. No gaze-contingent foveation is requested.")]
    [SerializeField] private bool requestFixedFoveation = false;
    [SerializeField, Range(0.0f, 1.0f)] private float fixedFoveationStrength = 0.70f;
    [Tooltip("Request 90 render frames/sec from Unity; XR runtime may use its own refresh scheduling.")]
    [SerializeField] private bool request90Fps = true;

    [Header("Read-only runtime diagnostics")]
    [SerializeField] private bool optimisationActive;
    [SerializeField] private string activeBlock = "";
    [SerializeField] private float requestedViewport;
    [SerializeField] private float measuredBlockFps;
    [SerializeField] private float measuredGazeHz;

    private GazeRecorder gaze;
    private HomePageController home;
    private XRDisplaySubsystem xrDisplay;
    private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>(2);

    private float previousViewportScale;
    private float previousFoveationLevel;
    private bool changedFoveation;
    private int previousTargetFrameRate;
    private bool changedTargetFrameRate;
    private bool blockActive;
    private double blockStart;
    private int firstSampleCount;
    private int observedLastSampleCount;
    private int frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToRecorderManager()
    {
        var recorder = FindFirstObjectByType<ImmersiveEyeVideoRecorder>(FindObjectsInactive.Include);
        if (recorder != null && recorder.GetComponent<ImmersiveEyeXR90Performance>() == null)
            recorder.gameObject.AddComponent<ImmersiveEyeXR90Performance>();
    }

    private void Awake()
    {
        gaze = FindFirstObjectByType<GazeRecorder>(FindObjectsInactive.Include);
        home = FindFirstObjectByType<HomePageController>(FindObjectsInactive.Include);
#if UNITY_ANDROID && !UNITY_EDITOR
        if (request90Fps)
        {
            previousTargetFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = 90;
            changedTargetFrameRate = true;
        }
        Debug.Log("IE_XR90|READY|requestedFps=" + Application.targetFrameRate + "|video=UNCHANGED");
#endif
    }

    private static bool IsComplexBlock(string name)
    {
        return string.Equals(name, "BlockC", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "BlockD", StringComparison.OrdinalIgnoreCase);
    }

    private void LateUpdate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (gaze == null || !gaze.isActiveAndEnabled)
        {
            if (blockActive) EndBlock();
            return;
        }

        bool shouldOptimise = enableComplexBlockOptimisation &&
                              gaze.Recording && IsComplexBlock(gaze.BlockName);
        string name = gaze.BlockName ?? "Unknown";

        if (blockActive && (!shouldOptimise || !string.Equals(activeBlock, name, StringComparison.Ordinal)))
            EndBlock();

        if (shouldOptimise && !blockActive)
            BeginBlock(name);

        if (blockActive)
        {
            frameCount++;
            observedLastSampleCount = gaze.RecordedSampleCount;
            double duration = Time.realtimeSinceStartupAsDouble - blockStart;
            if (duration > 0.25)
            {
                measuredBlockFps = (float)(frameCount / duration);
                measuredGazeHz = (float)(Math.Max(0, observedLastSampleCount - firstSampleCount) / duration);
            }
        }
#endif
    }

    private void BeginBlock(string name)
    {
        activeBlock = name;
        blockActive = true;
        optimisationActive = false;
        blockStart = Time.realtimeSinceStartupAsDouble;
        frameCount = 0;
        firstSampleCount = observedLastSampleCount = gaze.RecordedSampleCount;
        measuredBlockFps = measuredGazeHz = 0f;
        changedFoveation = false;
        previousViewportScale = XRSettings.renderViewportScale;
        requestedViewport = Mathf.Min(previousViewportScale, Mathf.Clamp(complexBlockXRViewportScale, 0.50f, 1.0f));

        // Unity 6 URP supports XRSettings.renderViewportScale without reallocating the
        // eye textures. This is an XR display hint; it is not the recording camera RT.
        try
        {
            XRSettings.renderViewportScale = requestedViewport;
            optimisationActive = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("IE_XR90|VIEWPORT_UNAVAILABLE|" + e.Message);
        }

        if (requestFixedFoveation)
        {
            try
            {
                displays.Clear();
                SubsystemManager.GetSubsystems(displays);
                xrDisplay = null;
                foreach (var d in displays) if (d != null && d.running) { xrDisplay = d; break; }
                if (xrDisplay != null)
                {
                    previousFoveationLevel = xrDisplay.foveatedRenderingLevel;
                    xrDisplay.foveatedRenderingLevel = Mathf.Max(previousFoveationLevel, fixedFoveationStrength);
                    changedFoveation = true;
                }
                else Debug.LogWarning("IE_XR90|FOVEATION_NO_ACTIVE_DISPLAY");
            }
            catch (Exception e)
            {
                Debug.LogWarning("IE_XR90|FOVEATION_UNAVAILABLE|" + e.Message);
            }
        }
        Debug.Log("IE_XR90|START|" + name + "|requestedViewport=" +
                  requestedViewport.ToString("F2", CultureInfo.InvariantCulture) +
                  "|appliedViewport=" + XRSettings.renderViewportScale.ToString("F2", CultureInfo.InvariantCulture) +
                  "|foveationRequested=" + changedFoveation +
                  "|MP4_1280x720_1.20zoom_8fps_UNCHANGED");
    }

    private void EndBlock()
    {
        if (!blockActive) return;
        double seconds = Math.Max(0.001, Time.realtimeSinceStartupAsDouble - blockStart);
        double fps = frameCount / seconds;
        double hz = Math.Max(0, observedLastSampleCount - firstSampleCount) / seconds;
        bool success = hz >= 85.0 && fps >= 85.0;
        string completedBlock = activeBlock;
        try
        {
            XRSettings.renderViewportScale = previousViewportScale;
            if (changedFoveation && xrDisplay != null)
                xrDisplay.foveatedRenderingLevel = previousFoveationLevel;
        }
        catch (Exception e) { Debug.LogWarning("IE_XR90|RESTORE_ERROR|" + e.Message); }

        blockActive = false;
        optimisationActive = false;
        activeBlock = "";
        changedFoveation = false;
        xrDisplay = null;
        Debug.Log("IE_XR90|END|" + completedBlock + "|unityFps=" +
                  fps.ToString("F2", CultureInfo.InvariantCulture) + "|gazeHz=" +
                  hz.ToString("F2", CultureInfo.InvariantCulture) +
                  "|target85PlusReached=" + success);
        WriteDiagnostic(completedBlock, seconds, fps, hz, success);
    }

    private void WriteDiagnostic(string completedBlock, double seconds, double fps, double hz, bool success)
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "Diagnostics");
            Directory.CreateDirectory(dir);
            string pid = Safe(home != null ? home.GetParticipantID() : "Unknown");
            string sid = Safe(home != null ? home.GetSessionID() : "Unknown");
            string path = Path.Combine(dir, "XR90Hz_" + pid + "_" + sid + "_" +
                Safe(completedBlock) + "_" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + ".csv");
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(700);
            sb.AppendLine("utc_timestamp,participant_id,session_id,block,duration_s,unity_fps,recorded_gaze_hz,xr_viewport_scale_requested,xr_viewport_scale_prior,ffr_requested,met_85hz_screening_threshold");
            sb.Append(DateTime.UtcNow.ToString("O")).Append(',').Append(pid).Append(',').Append(sid).Append(',')
              .Append(Safe(completedBlock)).Append(',').Append(seconds.ToString("F4", ci)).Append(',')
              .Append(fps.ToString("F3", ci)).Append(',').Append(hz.ToString("F3", ci)).Append(',')
              .Append(requestedViewport.ToString("F3", ci)).Append(',')
              .Append(previousViewportScale.ToString("F3", ci)).Append(',')
              .Append(requestFixedFoveation ? "1" : "0").Append(',').Append(success ? "1" : "0")
              .AppendLine();
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("IE_XR90|REPORT|" + path);
        }
        catch (Exception e) { Debug.LogError("IE_XR90|REPORT_FAILED|" + e.Message); }
    }

    private void OnDisable()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (blockActive) EndBlock();
        if (changedTargetFrameRate) Application.targetFrameRate = previousTargetFrameRate;
#endif
    }

    private void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (blockActive) EndBlock();
#endif
    }

    private static string Safe(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "Unknown";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s.Trim()) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        return sb.ToString();
    }
}
