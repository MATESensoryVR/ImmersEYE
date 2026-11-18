using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Independent, append-as-you-go Block D behavioural event CSV.
/// The existing GazeRecorder/DataManager gaze CSVs remain unchanged.
/// Attach to an always-active GameObject (e.g., BlockDController),
/// and bind BeginRecording / CompleteAndSave to its trial UnityEvents.
/// </summary>
public class BlockDSelectionRecorder : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BlockDPlateManager plateManager;
    [SerializeField] private HomePageController homePageController;

    [Header("Study Settings")]
    [SerializeField] private string studyId = "VRET_HTC";
    [SerializeField] private string outputFolderName = "ImmersiveEyeData";

    [Header("Runtime Status (Read Only)")]
    [SerializeField] private bool recording = false;
    [SerializeField] private int eventCount = 0;
    [SerializeField] private string lastSavedFilePath = "";

    private string participantId = "";
    private string sessionId = "";
    private double startTime;
    private StreamWriter writer;

    public bool Recording => recording;
    public int EventCount => eventCount;
    public string LastSavedFilePath => lastSavedFilePath;

    private void OnEnable()
    {
        if (plateManager != null)
            plateManager.FoodSelectionDetailed += OnFoodSelectionDetailed;
    }

    private void OnDisable()
    {
        if (plateManager != null)
            plateManager.FoodSelectionDetailed -= OnFoodSelectionDetailed;
        CloseFile();
    }

    public void BeginRecording()
    {
        if (recording)
        {
            Debug.LogWarning("BlockDSelectionRecorder: Already recording.");
            return;
        }
        if (plateManager == null || homePageController == null)
        {
            Debug.LogError("BlockDSelectionRecorder: Assign Plate Manager and Home Page Controller.");
            return;
        }

        participantId = CleanIdentifier(homePageController.GetParticipantID());
        sessionId = CleanIdentifier(homePageController.GetSessionID());
        if (string.IsNullOrWhiteSpace(participantId) || string.IsNullOrWhiteSpace(sessionId))
        {
            Debug.LogError("BlockDSelectionRecorder: Participant ID and Session ID are required.");
            return;
        }

        try
        {
            // Match DataManager's participant folder, but use a separate events file.
            string directory = Path.Combine(
                Application.persistentDataPath, outputFolderName, participantId);
            Directory.CreateDirectory(directory);
            string requestedPath = Path.Combine(directory,
                participantId + "_" + sessionId + "_BlockD_food_events.csv");
            string path = MakeUniquePath(requestedPath);
            writer = new StreamWriter(path, false, new UTF8Encoding(false));
            writer.WriteLine("study_id,participant_id,session_id,block_name,event_index,timestamp_utc,elapsed_s,event_type,food_object_name,plate_slot,selected_count,final_foods_in_slot_order");
            writer.Flush();

            lastSavedFilePath = path;
            eventCount = 0;
            startTime = Time.realtimeSinceStartupAsDouble;
            recording = true;
            WriteEvent("BLOCK_D_START", "", 0, plateManager.SelectedCount, "");
            Debug.Log("BLOCK_D_EVENT_RECORDING_START | " + lastSavedFilePath);
        }
        catch (Exception e)
        {
            Debug.LogError("BlockDSelectionRecorder: Failed to create CSV: " + e);
            CloseFile();
        }
    }

    private void OnFoodSelectionDetailed(string foodName, bool added, int count, int slot)
    {
        if (!recording) return;
        WriteEvent(added ? "FOOD_SELECTED" : "FOOD_REMOVED", foodName, slot, count, "");
    }

    public void CompleteAndSave()
    {
        if (!recording) return;

        // List is in plate-slot order, not necessarily chronological order.
        // Chronological selection/removal order is preserved by the events above.
        var finalFoods = plateManager != null
            ? plateManager.GetSelectedFoodNamesInSlotOrder()
            : new System.Collections.Generic.List<string>();
        string joined = string.Join("|", finalFoods);
        int finalCount = plateManager != null ? plateManager.SelectedCount : 0;
        WriteEvent(finalCount == 4 ? "BLOCK_D_CONFIRMED" : "BLOCK_D_STOPPED_INCOMPLETE",
            "", 0, finalCount, joined);
        CloseFile();
        Debug.Log("BLOCK_D_EVENT_DATA_SAVED | " + lastSavedFilePath);
    }

    private void WriteEvent(string eventType, string foodName, int slot, int selectedCount, string finalFoods)
    {
        if (!recording || writer == null) return;
        try
        {
            double elapsed = Time.realtimeSinceStartupAsDouble - startTime;
            string[] fields = {
                EscapeCsv(studyId), EscapeCsv(participantId), EscapeCsv(sessionId), "BlockD",
                eventCount.ToString(CultureInfo.InvariantCulture),
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                elapsed.ToString("F6", CultureInfo.InvariantCulture),
                EscapeCsv(eventType), EscapeCsv(foodName),
                slot > 0 ? slot.ToString(CultureInfo.InvariantCulture) : "",
                selectedCount.ToString(CultureInfo.InvariantCulture), EscapeCsv(finalFoods)
            };
            writer.WriteLine(string.Join(",", fields));
            writer.Flush(); // each interaction is durable even if the headset exits unexpectedly
            eventCount++;
        }
        catch (Exception e)
        {
            Debug.LogError("BlockDSelectionRecorder: CSV write failed: " + e);
            CloseFile();
        }
    }

    private void CloseFile()
    {
        recording = false;
        if (writer == null) return;
        try { writer.Flush(); writer.Dispose(); }
        catch (Exception e) { Debug.LogWarning("BlockDSelectionRecorder: File close failed: " + e); }
        finally { writer = null; }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && writer != null) writer.Flush();
    }

    private static string MakeUniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        string dir = Path.GetDirectoryName(path);
        string name = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        int i = 2;
        string candidate;
        do { candidate = Path.Combine(dir, name + "_" + i.ToString("D2") + ext); i++; }
        while (File.Exists(candidate));
        return candidate;
    }

    private static string CleanIdentifier(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        // Fixed cross-platform set includes path separators regardless of current OS.
        foreach (char c in "/\\:*?\"<>|") s = s.Replace(c, '_');
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    private static string EscapeCsv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n") || s.Contains("\r"))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
