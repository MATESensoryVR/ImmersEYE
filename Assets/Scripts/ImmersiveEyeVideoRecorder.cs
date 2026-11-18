using System;
using System.IO;
using UnityEngine;

// This interface requires a separate Android encoder backend; this file does not encode MP4 by itself.
public interface IImmersiveEyeVideoBackend
{
    // Return true only if the encoder successfully started.
    bool StartCapture(RenderTexture source, string outputPath, int frameRate);

    // Return true only after capture has stopped and the MP4 has been finalized.
    bool StopCapture();
}

public class ImmersiveEyeVideoRecorder : MonoBehaviour
{
    [Header("Capture References")]
    [SerializeField] private Camera videoCaptureCamera;
    [SerializeField] private RenderTexture videoRenderTexture;
    [SerializeField] private ImmersiveEyeRecorder sessionRecorder;
    [Tooltip("Assign the Android encoding component when we create it.")]
    [SerializeField] private MonoBehaviour encoderBackend;

    [Header("Video Settings")]
    [Range(5, 30)]
    [SerializeField] private int frameRate = 8;

    [Header("Runtime Status (Read Only)")]
    [SerializeField] private bool isRecording;
    [SerializeField] private string currentBlock = "";
    [SerializeField] private string lastOutputPath = "";
    [SerializeField] private string status = "NOT CONFIGURED";

    // Accessed only by the low-frequency native recording backend.
    public Camera VideoCaptureCamera => videoCaptureCamera;
    public bool IsRecording => isRecording;
    public string CurrentBlock => currentBlock;
    public string LastOutputPath => lastOutputPath;
    public string Status => status;

    private void Awake()
    {
        if (videoCaptureCamera != null && videoCaptureCamera != Camera.main)
            videoCaptureCamera.enabled = false;
    }

    // These four public methods can later be called from the block events.
    public void StartBlockAVideo() => StartForBlock("BlockA");
    public void StartBlockBVideo() => StartForBlock("BlockB");
    public void StartBlockCVideo() => StartForBlock("BlockC");
    public void StartBlockDVideo() => StartForBlock("BlockD");

    private void StartForBlock(string block)
    {
        if (isRecording)
        {
            Debug.LogWarning("Video recorder: already recording " + currentBlock);
            return;
        }

        if (videoCaptureCamera == null || videoRenderTexture == null)
        {
            Fail("Assign VideoCaptureCamera and ImmersiveEye_VideoRT first.");
            return;
        }

        if (videoCaptureCamera == Camera.main)
        {
            Fail("The capture camera cannot be the VR Main Camera.");
            return;
        }

        if (sessionRecorder == null)
        {
            Fail("Assign RecorderManager's ImmersiveEyeRecorder for participant/session IDs.");
            return;
        }

        IImmersiveEyeVideoBackend backend = encoderBackend as IImmersiveEyeVideoBackend;
        if (backend == null)
        {
            Fail("Android MP4 encoder not installed yet. No video file created.");
            return;
        }

        // Use the same participant/session values as the gaze CSV system.
        HomePageController home = FindFirstObjectByType<HomePageController>(
            FindObjectsInactive.Include);

        if (home == null)
        {
            Fail("HomePageController not found; cannot synchronize IDs.");
            return;
        }

        string enteredParticipant = home.GetParticipantID();
        string enteredSession = home.GetSessionID();

        if (string.IsNullOrWhiteSpace(enteredParticipant) ||
            string.IsNullOrWhiteSpace(enteredSession))
        {
            Fail("Participant or session ID is empty.");
            return;
        }

        sessionRecorder.participantId = enteredParticipant.Trim();
        sessionRecorder.sessionId = enteredSession.Trim();

        Debug.Log("IE_VIDEO_ID_SYNC|" +
                  sessionRecorder.participantId + "|" +
                  sessionRecorder.sessionId);

        string study = SafeName(sessionRecorder.studyId);
        string participant = SafeName(sessionRecorder.participantId);
        string session = SafeName(sessionRecorder.sessionId);
        string directory = Path.Combine(
            Application.persistentDataPath, "Recordings", study, participant, session);

        try
        {
            Directory.CreateDirectory(directory);
            string stamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            string filename = participant + "_" + session + "_" + block + "_" +
                              stamp + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".mp4";
            string path = Path.Combine(directory, filename);

            videoCaptureCamera.targetTexture = videoRenderTexture;
            // IMPORTANT: Never enable continuous rendering. On XR devices it renders
            // a second scene every headset frame, even when the MP4 is 8 FPS.
            // The backend submits one URP render request per captured frame instead.
            videoCaptureCamera.enabled = false;

            if (!backend.StartCapture(videoRenderTexture, path, frameRate))
            {
                videoCaptureCamera.enabled = false;
                Fail("Android encoder could not start; no video was recorded.");
                return;
            }

            isRecording = true;
            currentBlock = block;
            lastOutputPath = path;
            status = "RECORDING";
            Debug.Log("VIDEO_START | " + block + " | " + DateTime.UtcNow.ToString("O") +
                      " | " + path);
        }
        catch (Exception ex)
        {
            videoCaptureCamera.enabled = false;
            Fail("Video start failed: " + ex.Message);
        }
    }

    public void StopVideo()
    {
        if (!isRecording)
            return;

        bool finalized = false;
        try
        {
            IImmersiveEyeVideoBackend backend = encoderBackend as IImmersiveEyeVideoBackend;
            finalized = backend != null && backend.StopCapture();
        }
        catch (Exception ex)
        {
            Debug.LogError("Video stop failed: " + ex);
        }
        finally
        {
            if (videoCaptureCamera != null && videoCaptureCamera != Camera.main)
                videoCaptureCamera.enabled = false;
            isRecording = false;
        }

        bool fileExists = File.Exists(lastOutputPath);
        status = finalized && fileExists ? "MP4 SAVED" : "MP4 NOT VERIFIED";
        Debug.Log("VIDEO_STOP | " + currentBlock + " | " + DateTime.UtcNow.ToString("O") +
                  " | Status=" + status + " | " + lastOutputPath);
        currentBlock = "";
    }

    private void OnDisable()
    {
        StopVideo();
    }

    private void Fail(string message)
    {
        status = "ERROR: " + message;
        Debug.LogWarning("ImmersiveEyeVideoRecorder: " + message);
    }

    private static string SafeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Unknown";
        foreach (char character in Path.GetInvalidFileNameChars())
            value = value.Replace(character, '_');
        return value.Trim();
    }
}
