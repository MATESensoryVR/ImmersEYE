using System;
using UnityEngine;
using UnityEngine.InputSystem;

using VIVE.OpenXR.SecondaryViewConfiguration;


public class SpectatorRecordingSync : MonoBehaviour
{
    // ============================================================
    // RECORDER
    // ============================================================

    [Header("ImmersiveEye Recorder")]

    [Tooltip("Drag RecorderManager here.")]
    public ImmersiveEyeRecorder recorder;


    // ============================================================
    // EDITOR TESTING
    // ============================================================

    [Header("Editor Simulation")]

    [Tooltip(
        "Allows F9/F10 to simulate VIVE system recording "
        + "while testing on Mac."
    )]
    public bool simulateInEditor = true;


    // ============================================================
    // PUBLIC STATUS
    // ============================================================

    public bool IsVideoRecording
    {
        get;
        private set;
    }


    public string VideoStartUtc
    {
        get;
        private set;
    }


    public string VideoStopUtc
    {
        get;
        private set;
    }


    // ============================================================
    // PRIVATE
    // ============================================================

    private SpectatorCameraBased spectatorCamera;

    private bool callbacksRegistered = false;


    // ============================================================
    // UNITY START
    // ============================================================

    private void Start()
    {
        FindRecorder();

        SetupSpectatorCamera();
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
#if UNITY_EDITOR

        if (!simulateInEditor)
        {
            return;
        }


        Keyboard keyboard =
            Keyboard.current;


        if (keyboard == null)
        {
            return;
        }


        // --------------------------------------------------------
        // F9 = simulate video recording START
        // --------------------------------------------------------

        if (
            keyboard.f9Key.wasPressedThisFrame
        )
        {
            HandleSpectatorStart();
        }


        // --------------------------------------------------------
        // F10 = simulate video recording STOP
        // --------------------------------------------------------

        if (
            keyboard.f10Key.wasPressedThisFrame
        )
        {
            HandleSpectatorStop();
        }

#endif
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
                "SpectatorRecordingSync: "
                +
                "ImmersiveEyeRecorder was not found."
            );
        }
    }


    // ============================================================
    // SETUP SPECTATOR CAMERA
    // ============================================================

    private void SetupSpectatorCamera()
    {
        // --------------------------------------------------------
        // First try HTC's singleton
        // --------------------------------------------------------

        spectatorCamera =
            SpectatorCameraBased.Instance;


        // --------------------------------------------------------
        // Then search scene
        // --------------------------------------------------------

        if (spectatorCamera == null)
        {
            spectatorCamera =
                FindAnyObjectByType<SpectatorCameraBased>();
        }


        // --------------------------------------------------------
        // Create it if it is not already present
        // --------------------------------------------------------

        if (spectatorCamera == null)
        {
            spectatorCamera =
                gameObject.AddComponent<SpectatorCameraBased>();


            Debug.Log(
                "SpectatorRecordingSync: "
                +
                "SpectatorCameraBased component created."
            );
        }


        RegisterCallbacks();


        Debug.Log(
            "SpectatorRecordingSync ready."
        );
    }


    // ============================================================
    // REGISTER HTC CALLBACKS
    // ============================================================

    private void RegisterCallbacks()
    {
        if (spectatorCamera == null)
        {
            return;
        }


        if (callbacksRegistered)
        {
            return;
        }


        spectatorCamera.OnSpectatorStart +=
            HandleSpectatorStart;


        spectatorCamera.OnSpectatorStop +=
            HandleSpectatorStop;


        callbacksRegistered =
            true;


        Debug.Log(
            "SpectatorRecordingSync: "
            +
            "VIVE spectator callbacks registered."
        );
    }


    // ============================================================
    // UNREGISTER CALLBACKS
    // ============================================================

    private void UnregisterCallbacks()
    {
        if (spectatorCamera == null)
        {
            return;
        }


        if (!callbacksRegistered)
        {
            return;
        }


        spectatorCamera.OnSpectatorStart -=
            HandleSpectatorStart;


        spectatorCamera.OnSpectatorStop -=
            HandleSpectatorStop;


        callbacksRegistered =
            false;
    }


    // ============================================================
    // VIDEO START
    // ============================================================

    private void HandleSpectatorStart()
    {
        if (IsVideoRecording)
        {
            return;
        }


        IsVideoRecording =
            true;


        VideoStartUtc =
            DateTime.UtcNow.ToString(
                "O"
            );


        VideoStopUtc =
            "";


        Debug.Log(
            "============================================\n"
            +
            "VIVE VIDEO RECORDING STARTED\n"
            +
            "UTC: "
            +
            VideoStartUtc
            +
            "\n"
            +
            "============================================"
        );


        if (
            recorder != null
            &&
            recorder.IsRecording
        )
        {
            recorder.MarkEvent(
                "video",
                "recording_start",
                "vive_spectator"
            );
        }
        else
        {
            Debug.LogWarning(
                "SpectatorRecordingSync: "
                +
                "Video started while ImmersiveEye "
                +
                "CSV recording was not active."
            );
        }
    }


    // ============================================================
    // VIDEO STOP
    // ============================================================

    private void HandleSpectatorStop()
    {
        if (!IsVideoRecording)
        {
            return;
        }


        IsVideoRecording =
            false;


        VideoStopUtc =
            DateTime.UtcNow.ToString(
                "O"
            );


        Debug.Log(
            "============================================\n"
            +
            "VIVE VIDEO RECORDING STOPPED\n"
            +
            "UTC: "
            +
            VideoStopUtc
            +
            "\n"
            +
            "============================================"
        );


        if (
            recorder != null
            &&
            recorder.IsRecording
        )
        {
            recorder.MarkEvent(
                "video",
                "recording_stop",
                "vive_spectator"
            );
        }
        else
        {
            Debug.LogWarning(
                "SpectatorRecordingSync: "
                +
                "Video stopped while ImmersiveEye "
                +
                "CSV recording was not active."
            );
        }
    }


    // ============================================================
    // DESTROY
    // ============================================================

    private void OnDestroy()
    {
        UnregisterCallbacks();
    }
}