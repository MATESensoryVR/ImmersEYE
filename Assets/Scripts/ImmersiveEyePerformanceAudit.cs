using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Diagnostics only: samples Unity frames and real gaze CSV sample counts during each block.
/// Does not generate or interpolate gaze samples, alter tracking, render anything or add
/// sustained I/O during recording. Writes one compact file after each block ends.
/// Automatically attaches to the existing RecorderManager at scene startup.
/// </summary>
[DefaultExecutionOrder(1300)]
public sealed class ImmersiveEyePerformanceAudit : MonoBehaviour
{
    private GazeRecorder gaze;
    private ImmersiveEyeVideoRecorder video;
    private HomePageController home;
    private bool active;
    private string block;
    private double blockStart, windowStart;
    private int frames, windowFrames, windowGazeStart, blockGazeStart, lastObservedGazeCount;
    private double worstFrameMs;
    private double lastFrameAt;
    private readonly List<string> lines = new List<string>(64);
    private string outputPath;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttach()
    {
        var owner = FindFirstObjectByType<ImmersiveEyeVideoRecorder>(FindObjectsInactive.Include);
        if (owner != null && owner.GetComponent<ImmersiveEyePerformanceAudit>() == null)
            owner.gameObject.AddComponent<ImmersiveEyePerformanceAudit>();
    }

    private void Awake()
    {
        gaze = FindFirstObjectByType<GazeRecorder>(FindObjectsInactive.Include);
        video = GetComponent<ImmersiveEyeVideoRecorder>();
        home = FindFirstObjectByType<HomePageController>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (gaze == null) return;
        bool isRecording = gaze.isActiveAndEnabled && gaze.Recording;
        string nowBlock = gaze.BlockName ?? "Unknown";
        if (active && (!isRecording || block != nowBlock)) EndBlock();
        if (isRecording && !active) BeginBlock(nowBlock);
        if (!active) return;

        lastObservedGazeCount = gaze.RecordedSampleCount;
        double now = Time.realtimeSinceStartupAsDouble;
        double deltaMs = Math.Max(0, now - lastFrameAt) * 1000.0;
        if (frames > 0 && deltaMs > worstFrameMs) worstFrameMs = deltaMs;
        lastFrameAt = now;
        frames++;
        windowFrames++;
        if (now - windowStart >= 1.0)
        {
            FlushWindow(now, false);
        }
    }

    private void BeginBlock(string name)
    {
        block = name;
        active = true;
        frames = windowFrames = 0;
        blockGazeStart = windowGazeStart = lastObservedGazeCount = gaze.RecordedSampleCount;
        blockStart = windowStart = lastFrameAt = Time.realtimeSinceStartupAsDouble;
        worstFrameMs = 0;
        lines.Clear();
        lines.Add("utc_timestamp,participant_id,session_id,block,window_seconds,unity_fps,actual_gaze_csv_hz,"
                + "video_active,window_unity_frames,window_gaze_samples,worst_frame_ms,window_start_realtime_s");
        var dir = Path.Combine(Application.persistentDataPath, "Diagnostics");
        Directory.CreateDirectory(dir);
        var pid = Safe(home != null ? home.GetParticipantID() : "Unknown");
        var sid = Safe(home != null ? home.GetSessionID() : "Unknown");
        outputPath = Path.Combine(dir, "GazeFPS_" + pid + "_" + sid + "_"
                                     + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "_" + Safe(name) + ".csv");
        Debug.Log("IE_PERF|START|" + block + "|" + pid + "|" + sid);
    }

    private void FlushWindow(double now, bool final)
    {
        double duration = now - windowStart;
        if (duration <= 0.001 || windowFrames == 0) return;
        int currentGazeCount = gaze.Recording ? gaze.RecordedSampleCount : lastObservedGazeCount;
        int gazeCount = Math.Max(0, currentGazeCount - windowGazeStart);
        double fps = windowFrames / duration;
        double gazeHz = gazeCount / duration;
        string pid = Safe(home != null ? home.GetParticipantID() : "Unknown");
        string sid = Safe(home != null ? home.GetSessionID() : "Unknown");
        var inv = CultureInfo.InvariantCulture;
        lines.Add(DateTime.UtcNow.ToString("O") + "," + pid + "," + sid + "," + Safe(block) + ","
            + duration.ToString("F4", inv) + "," + fps.ToString("F2", inv) + ","
            + gazeHz.ToString("F2", inv) + "," + (video != null && video.IsRecording ? "1" : "0") + ","
            + windowFrames + "," + gazeCount + "," + worstFrameMs.ToString("F2", inv) + ","
            + windowStart.ToString("F4", inv));
        if (!final) Debug.Log("IE_PERF|" + block + "|fps=" + fps.ToString("F1", inv)
                              + "|gazeHz=" + gazeHz.ToString("F1", inv)
                              + "|video=" + (video != null && video.IsRecording ? "ON" : "OFF"));
        windowStart = now;
        windowGazeStart = currentGazeCount;
        windowFrames = 0;
        worstFrameMs = 0;
    }

    private void EndBlock()
    {
        if (!active) return;
        double now = Time.realtimeSinceStartupAsDouble;
        FlushWindow(now, true);
        double duration = Math.Max(0.001, now - blockStart);
        double fps = frames / duration;
        int gazeCount = Math.Max(0, lastObservedGazeCount - blockGazeStart);
        double gazeHz = gazeCount / duration;
        active = false;
        try
        {
            File.WriteAllLines(outputPath, lines, new UTF8Encoding(false));
            Debug.Log("IE_PERF|END|" + block + "|fps=" + fps.ToString("F2", CultureInfo.InvariantCulture)
                      + "|gazeHz=" + gazeHz.ToString("F2", CultureInfo.InvariantCulture)
                      + "|file=" + outputPath);
        }
        catch (Exception e) { Debug.LogError("IE_PERF|FILE_FAILED|" + e.Message); }
    }

    private void OnDisable() { EndBlock(); }
    private void OnApplicationQuit() { EndBlock(); }

    private static string Safe(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unknown";
        var s = new StringBuilder();
        foreach (char c in value.Trim())
            s.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        return s.ToString();
    }
}
