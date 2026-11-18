using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Non-invasive per-block frame-budget probe for C/D on VIVE Focus Vision.
/// Does not request gaze samples or modify graphics settings; it measures only.
/// Never treats Unity frame count as proof of independent hardware gaze samples.
/// </summary>
[DefaultExecutionOrder(1450)]
[DisallowMultipleComponent]
public sealed class ImmersiveEyeFrameBudgetDiagnostic : MonoBehaviour
{
    private GazeRecorder gaze;
    private HomePageController home;
    private XRDisplaySubsystem display;
    private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>(2);

    private ProfilerRecorder cpuRecorder;
    private bool active;
    private bool hasCpuRecorder;
    private string block;
    private string outputFile;
    private DateTime blockStartUtc;
    private double blockStartTime;
    private double windowStartTime;
    private int windowStartSamples;
    private int windowFrames;
    private int totalFrames;
    private int initialSamples;
    private double windowFrameMsTotal;
    private float maxFrameMs;
    private readonly List<float> frameTimes = new List<float>(180);
    private double cpuMsTotal;
    private int cpuCount;
    private double gpuMsTotal;
    private int gpuCount;
    private float reportedRefreshHz;
    private int reportedDroppedFrames;
    private int dropsReportedCount;
    private float lastViewport;
    private float lastFoveation;
    private readonly StringBuilder rows = new StringBuilder(4096);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        var existing = FindFirstObjectByType<ImmersiveEyeFrameBudgetDiagnostic>(FindObjectsInactive.Include);
        if (existing != null) return;
        var g = FindFirstObjectByType<GazeRecorder>(FindObjectsInactive.Include);
        if (g != null) g.gameObject.AddComponent<ImmersiveEyeFrameBudgetDiagnostic>();
    }

    private void Awake()
    {
        gaze = GetComponent<GazeRecorder>();
        if (gaze == null)
            gaze = FindFirstObjectByType<GazeRecorder>(FindObjectsInactive.Include);
        home = FindFirstObjectByType<HomePageController>(FindObjectsInactive.Include);
        Debug.Log("IE_FRAME_BUDGET_V13|READY|probe_only|MP4_UNCHANGED|GAZE_UNCHANGED");
    }

    private static bool IsTargetBlock(string name)
    {
        return string.Equals(name, "BlockC", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "BlockD", StringComparison.OrdinalIgnoreCase);
    }

    private void LateUpdate()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bool nowRecording = gaze != null && gaze.Recording && IsTargetBlock(gaze.BlockName);
        string currentBlock = gaze != null ? gaze.BlockName : null;
        if (active && (!nowRecording || !string.Equals(block, currentBlock, StringComparison.Ordinal)))
            StopBlock();

        if (nowRecording && !active)
            StartBlock(currentBlock);

        if (!active) return;

        float dt = Time.unscaledDeltaTime;
        float frameMs = Mathf.Max(0.0f, dt * 1000f);
        frameTimes.Add(frameMs);
        windowFrameMsTotal += frameMs;
        maxFrameMs = Mathf.Max(maxFrameMs, frameMs);
        windowFrames++;
        totalFrames++;

        if (hasCpuRecorder && cpuRecorder.Valid)
        {
            long ns = cpuRecorder.LastValue;
            if (ns > 0)
            {
                cpuMsTotal += ns / 1000000.0;
                cpuCount++;
            }
        }

        if (display != null && display.running)
        {
            try
            {
                if (display.TryGetAppGPUTimeLastFrame(out float seconds) &&
                    seconds > 0 && seconds < 1)
                {
                    gpuMsTotal += seconds * 1000.0;
                    gpuCount++;
                }
                if (display.TryGetDisplayRefreshRate(out float refresh) && refresh > 0)
                    reportedRefreshHz = refresh;
                if (display.TryGetDroppedFrameCount(out int dropped) && dropped >= 0)
                {
                    reportedDroppedFrames += dropped;
                    dropsReportedCount++;
                }
                lastFoveation = display.foveatedRenderingLevel;
            }
            catch (Exception) { /* XR timings are optional. */ }
        }
        lastViewport = XRSettings.renderViewportScale;

        double now = Time.realtimeSinceStartupAsDouble;
        if (now - windowStartTime >= 1.0)
            FinishWindow(now);
#endif
    }

    private void StartBlock(string name)
    {
        active = true;
        block = name;
        blockStartUtc = DateTime.UtcNow;
        blockStartTime = windowStartTime = Time.realtimeSinceStartupAsDouble;
        windowStartSamples = initialSamples = gaze.RecordedSampleCount;
        totalFrames = 0;
        rows.Clear();
        rows.AppendLine("row_type,participant_id,session_id,block,elapsed_s,window_duration_s,unity_fps,gaze_records_per_s,mean_unity_frame_ms,p95_unity_frame_ms,max_unity_frame_ms,cpu_main_thread_ms_if_available,xr_app_gpu_ms_if_available,xr_display_refresh_hz_if_available,xr_viewport_scale,xr_fixed_foveation_level,dropped_frames_if_available,unity_frames,gaze_records");
        ResetWindowCounters();

        displays.Clear();
        SubsystemManager.GetSubsystems(displays);
        display = null;
        foreach (var d in displays)
            if (d != null && d.running) { display = d; break; }

        hasCpuRecorder = false;
        try
        {
            cpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time", 8);
            hasCpuRecorder = cpuRecorder.Valid;
        }
        catch (Exception) { hasCpuRecorder = false; }
        Debug.Log("IE_FRAME_BUDGET_V13|START|" + name + "|gpuApi=" + (display != null) + "|cpuApi=" + hasCpuRecorder);
    }

    private void FinishWindow(double endTime)
    {
        double seconds = Math.Max(0.001, endTime - windowStartTime);
        int lastSamples = gaze != null ? gaze.RecordedSampleCount : windowStartSamples;
        int gazeDelta = Math.Max(0, lastSamples - windowStartSamples);
        var ci = CultureInfo.InvariantCulture;
        frameTimes.Sort();
        float p95 = frameTimes.Count > 0
            ? frameTimes[Math.Min(frameTimes.Count - 1, (int)Math.Ceiling(frameTimes.Count * .95) - 1)]
            : 0f;
        string pid = Safe(home != null ? home.GetParticipantID() : "Unknown");
        string sid = Safe(home != null ? home.GetSessionID() : "Unknown");
        rows.Append("window,").Append(pid).Append(',').Append(sid).Append(',').Append(block).Append(',')
            .Append((endTime - blockStartTime).ToString("F3", ci)).Append(',')
            .Append(seconds.ToString("F3", ci)).Append(',')
            .Append((windowFrames / seconds).ToString("F2", ci)).Append(',')
            .Append((gazeDelta / seconds).ToString("F2", ci)).Append(',')
            .Append(windowFrames > 0 ? (windowFrameMsTotal / windowFrames).ToString("F2", ci) : "").Append(',')
            .Append(frameTimes.Count > 0 ? p95.ToString("F2", ci) : "").Append(',')
            .Append(frameTimes.Count > 0 ? maxFrameMs.ToString("F2", ci) : "").Append(',')
            .Append(cpuCount > 0 ? (cpuMsTotal / cpuCount).ToString("F2", ci) : "").Append(',')
            .Append(gpuCount > 0 ? (gpuMsTotal / gpuCount).ToString("F2", ci) : "").Append(',')
            .Append(reportedRefreshHz > 0 ? reportedRefreshHz.ToString("F2", ci) : "").Append(',')
            .Append(lastViewport.ToString("F3", ci)).Append(',')
            .Append(lastFoveation.ToString("F3", ci)).Append(',')
            .Append(dropsReportedCount > 0 ? reportedDroppedFrames.ToString(ci) : "").Append(',')
            .Append(windowFrames).Append(',').Append(gazeDelta).AppendLine();
        windowStartTime = endTime;
        windowStartSamples = lastSamples;
        ResetWindowCounters();
    }

    private void ResetWindowCounters()
    {
        windowFrames = 0;
        windowFrameMsTotal = 0;
        maxFrameMs = 0;
        frameTimes.Clear();
        cpuMsTotal = 0;
        cpuCount = 0;
        gpuMsTotal = 0;
        gpuCount = 0;
        reportedRefreshHz = 0;
        reportedDroppedFrames = 0;
        dropsReportedCount = 0;
    }

    private void StopBlock()
    {
        if (!active) return;
        double now = Time.realtimeSinceStartupAsDouble;
        if (now - windowStartTime > .05 && windowFrames > 0)
            FinishWindow(now);
        active = false;
        if (hasCpuRecorder) cpuRecorder.Dispose();
        hasCpuRecorder = false;
        display = null;
        try
        {
            string directory = Path.Combine(Application.persistentDataPath, "Diagnostics");
            Directory.CreateDirectory(directory);
            string pid = Safe(home != null ? home.GetParticipantID() : "Unknown");
            string sid = Safe(home != null ? home.GetSessionID() : "Unknown");
            string filename = "FrameBudget_" + pid + "_" + sid + "_" + Safe(block) + "_" +
                blockStartUtc.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture) + ".csv";
            outputFile = Path.Combine(directory, filename);
            File.WriteAllText(outputFile, rows.ToString(), new UTF8Encoding(false));
            Debug.Log("IE_FRAME_BUDGET_V13|SAVED|" + outputFile + "|frames=" + totalFrames);
        }
        catch (Exception e) { Debug.LogError("IE_FRAME_BUDGET_V13|SAVE_FAILED|" + e.Message); }
        block = "";
    }

    private void OnDisable() { StopBlock(); }
    private void OnApplicationPause(bool paused) { if (paused) StopBlock(); }
    private void OnDestroy() { StopBlock(); }

    private static string Safe(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "Unknown";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s.Trim()) sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
        return sb.ToString();
    }
}
