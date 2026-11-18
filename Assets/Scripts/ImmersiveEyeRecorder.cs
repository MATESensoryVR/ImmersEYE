using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR.OpenXR;

using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;
using VIVE.OpenXR.FacialTracking;


public class ImmersiveEyeRecorder : MonoBehaviour
{
    // ============================================================
    // SESSION INFORMATION
    // ============================================================

    [Header("Session Information")]
    public string studyId = "VRET_HTC";
    public string participantId = "P001";
    public string sessionId = "S01";


    [Header("Current Trial Context")]
    public string trialId = "T01";
    public string condition = "Control";
    public string stimulusId = "";


    // ============================================================
    // DEVICE / FEATURE INFORMATION
    // ============================================================

    [Header("Device Metadata")]
    public string headsetName = "VIVE Focus Vision";

    public bool eyeTrackingFeatureEnabled = true;
    public bool facialTrackingFeatureEnabled = true;
    public bool spectatorCameraFeatureEnabled = true;


    // ============================================================
    // TRACKING
    // ============================================================

    [Header("Tracking")]

    [Tooltip("Drag XR Origin > Camera Offset > Main Camera here.")]
    public Transform headTransform;

    [Range(1, 120)]
    public int sampleRateHz = 90;


    // ============================================================
    // RECORDING
    // ============================================================

    [Header("Recording")]
    public bool autoStartRecording = false;

    [Range(1, 1000)]
    public int flushEverySamples = 90;


    // ============================================================
    // PUBLIC STATUS
    // ============================================================

    public bool IsRecording
    {
        get;
        private set;
    }


    public string OutputFilePath
    {
        get
        {
            return outputFilePath;
        }
    }


    public string MetadataFilePath
    {
        get
        {
            return metadataFilePath;
        }
    }


    public long SamplesWritten
    {
        get
        {
            return sampleIndex;
        }
    }


    // ============================================================
    // FACIAL EXPRESSION NAMES
    // ============================================================

    private static readonly string[] FacialEyeNames =
    {
        "left_blink",
        "left_wide",
        "right_blink",
        "right_wide",
        "left_squeeze",
        "right_squeeze",
        "left_down",
        "right_down",
        "left_out",
        "right_in",
        "left_in",
        "right_out",
        "left_up",
        "right_up"
    };


    private static readonly string[] FacialLipNames =
    {
        "jaw_right",
        "jaw_left",
        "jaw_forward",
        "jaw_open",
        "mouth_ape_shape",
        "mouth_upper_right",
        "mouth_upper_left",
        "mouth_lower_right",
        "mouth_lower_left",
        "mouth_upper_overturn",
        "mouth_lower_overturn",
        "mouth_pout",
        "mouth_smile_right",
        "mouth_smile_left",
        "mouth_sad_right",
        "mouth_sad_left",
        "cheek_puff_right",
        "cheek_puff_left",
        "cheek_suck",
        "mouth_upper_upright",
        "mouth_upper_upleft",
        "mouth_lower_downright",
        "mouth_lower_downleft",
        "mouth_upper_inside",
        "mouth_lower_inside",
        "mouth_lower_overlay",
        "tongue_longstep1",
        "tongue_left",
        "tongue_right",
        "tongue_up",
        "tongue_down",
        "tongue_roll",
        "tongue_longstep2",
        "tongue_upright_morph",
        "tongue_upleft_morph",
        "tongue_downright_morph",
        "tongue_downleft_morph"
    };


    // ============================================================
    // EVENT STATE
    // ============================================================

    private bool eventPending = false;

    private string pendingEventType = "";
    private string pendingEventName = "";
    private string pendingEventValue = "";


    // ============================================================
    // RECORDING STATE
    // ============================================================

    private StreamWriter writer;

    private double sessionStartTime;
    private double nextSampleTime;

    private DateTime sessionStartUtc;

    private string outputFolder;
    private string sessionFolder;

    private string outputFilePath;
    private string metadataFilePath;

    private string csvFileName;
    private string metadataFileName;
    private string videoFileName;

    private long sampleIndex = 0;
    private int samplesSinceFlush = 0;


    private readonly CultureInfo culture =
        CultureInfo.InvariantCulture;


    // ============================================================
    // METADATA MODEL
    // ============================================================

    [Serializable]
    private class SessionMetadata
    {
        public string metadata_schema_version;

        public string study_id;
        public string participant_id;
        public string session_id;

        public string headset;

        public bool eye_tracking_feature_enabled;
        public bool facial_tracking_feature_enabled;
        public bool spectator_camera_feature_enabled;

        public int requested_sample_rate_hz;

        public string session_start_utc;
        public string session_end_utc;

        public double session_duration_s;

        public long samples_written;

        public string csv_file;
        public string metadata_file;
        public string video_file;

        public bool video_recorded;

        public string unity_version;
        public string application_version;
        public string runtime_platform;

        public string recording_status;
    }


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        FindHeadTransform();
        PrepareOutputFolder();
    }


    private void Start()
    {
        if (autoStartRecording)
        {
            StartRecording();
        }
    }


    private void Update()
    {
        if (!IsRecording)
        {
            return;
        }

        if (writer == null)
        {
            return;
        }


        double currentTime =
            Time.realtimeSinceStartupAsDouble;


        if (currentTime < nextSampleTime)
        {
            return;
        }


        try
        {
            RecordSample();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "ImmersiveEye Recorder: RecordSample failed.\n"
                +
                exception
            );
        }


        double interval =
            1.0
            /
            Math.Max(
                1,
                sampleRateHz
            );


        nextSampleTime +=
            interval;


        if (nextSampleTime < currentTime)
        {
            nextSampleTime =
                currentTime
                +
                interval;
        }
    }


    // ============================================================
    // TRIAL CONTEXT
    // ============================================================

    public void SetTrialContext(
        string newTrialId,
        string newCondition,
        string newStimulusId = ""
    )
    {
        trialId =
            CleanValue(
                newTrialId,
                "T01"
            );


        condition =
            CleanValue(
                newCondition,
                "Control"
            );


        stimulusId =
            string.IsNullOrWhiteSpace(
                newStimulusId
            )
            ?
            ""
            :
            newStimulusId.Trim();
    }


    // ============================================================
    // EVENT
    // ============================================================

    public void MarkEvent(
        string eventType,
        string eventName,
        string eventValue = ""
    )
    {
        if (!IsRecording)
        {
            Debug.LogWarning(
                "ImmersiveEye: Cannot mark event because recording is not active."
            );

            return;
        }


        pendingEventType =
            eventType == null
            ?
            ""
            :
            eventType.Trim();


        pendingEventName =
            eventName == null
            ?
            ""
            :
            eventName.Trim();


        pendingEventValue =
            eventValue == null
            ?
            ""
            :
            eventValue.Trim();


        eventPending =
            true;
    }


    // ============================================================
    // HEAD
    // ============================================================

    private void FindHeadTransform()
    {
        if (headTransform != null)
        {
            return;
        }


        Camera mainCamera =
            Camera.main;


        if (mainCamera != null)
        {
            headTransform =
                mainCamera.transform;
        }
        else
        {
            Debug.LogWarning(
                "ImmersiveEye Recorder: Main Camera not found."
            );
        }
    }


    // ============================================================
    // OUTPUT ROOT
    // ============================================================

    private void PrepareOutputFolder()
    {
#if UNITY_EDITOR

        outputFolder =
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    "Recordings"
                )
            );

#else

        outputFolder =
            Path.Combine(
                Application.persistentDataPath,
                "Recordings"
            );

#endif


        Directory.CreateDirectory(
            outputFolder
        );
    }


    // ============================================================
    // START RECORDING
    // ============================================================

    public void StartRecording()
    {
        if (IsRecording)
        {
            return;
        }


        if (headTransform == null)
        {
            FindHeadTransform();
        }


        string cleanStudy =
            CleanFileName(
                studyId
            );


        string cleanParticipant =
            CleanFileName(
                participantId
            );


        string cleanSession =
            CleanFileName(
                sessionId
            );


        sessionFolder =
            Path.Combine(
                outputFolder,
                cleanStudy,
                cleanParticipant,
                cleanSession
            );


        Directory.CreateDirectory(
            sessionFolder
        );


        csvFileName =
            cleanParticipant
            +
            "_"
            +
            cleanSession
            +
            "_master.csv";


        metadataFileName =
            cleanParticipant
            +
            "_"
            +
            cleanSession
            +
            "_metadata.json";


        videoFileName =
            cleanParticipant
            +
            "_"
            +
            cleanSession
            +
            "_video.mp4";


        outputFilePath =
            Path.Combine(
                sessionFolder,
                csvFileName
            );


        metadataFilePath =
            Path.Combine(
                sessionFolder,
                metadataFileName
            );


        try
        {
            writer =
                new StreamWriter(
                    outputFilePath,
                    false,
                    new UTF8Encoding(false)
                );


            WriteHeader();


            sessionStartTime =
                Time.realtimeSinceStartupAsDouble;


            sessionStartUtc =
                DateTime.UtcNow;


            nextSampleTime =
                sessionStartTime;


            sampleIndex =
                0;


            samplesSinceFlush =
                0;


            eventPending =
                false;


            pendingEventType =
                "";


            pendingEventName =
                "";


            pendingEventValue =
                "";


            IsRecording =
                true;


            WriteMetadata(
                false
            );


            Debug.Log(
                "================================================\n"
                +
                "IMMERSIVEEYE RECORDING STARTED\n"
                +
                "CSV:\n"
                +
                outputFilePath
                +
                "\n\nMETADATA:\n"
                +
                metadataFilePath
                +
                "\n================================================"
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "ImmersiveEye Recorder could not start:\n"
                +
                exception
            );


            writer =
                null;


            IsRecording =
                false;
        }
    }


    // ============================================================
    // STOP RECORDING
    // ============================================================

    public void StopRecording()
    {
        if (!IsRecording)
        {
            return;
        }


        /*
         * If another event is still waiting to be written,
         * preserve it first.
         */
        if (
            writer != null
            &&
            eventPending
        )
        {
            try
            {
                RecordSample();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "ImmersiveEye: Could not flush pending event before stop.\n"
                    +
                    exception
                );
            }
        }


        /*
         * Always create a final synchronized stop row.
         */
        pendingEventType =
            "session";


        pendingEventName =
            "stop";


        pendingEventValue =
            "";


        eventPending =
            true;


        if (writer != null)
        {
            try
            {
                RecordSample();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "ImmersiveEye: Could not write final stop event.\n"
                    +
                    exception
                );
            }
        }


        IsRecording =
            false;


        if (writer != null)
        {
            try
            {
                writer.Flush();

                writer.Close();

                writer.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "ImmersiveEye Recorder: Error closing CSV.\n"
                    +
                    exception
                );
            }


            writer =
                null;
        }


        WriteMetadata(
            true
        );


        Debug.Log(
            "================================================\n"
            +
            "IMMERSIVEEYE RECORDING STOPPED\n"
            +
            "Samples written: "
            +
            sampleIndex
            +
            "\n"
            +
            "CSV:\n"
            +
            outputFilePath
            +
            "\n\nMETADATA:\n"
            +
            metadataFilePath
            +
            "\n================================================"
        );
    }


    // ============================================================
    // METADATA
    // ============================================================

    private void WriteMetadata(
        bool completed
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                metadataFilePath
            )
        )
        {
            return;
        }


        try
        {
            double duration =
                0.0;


            if (sessionStartTime > 0)
            {
                duration =
                    Time.realtimeSinceStartupAsDouble
                    -
                    sessionStartTime;


                if (duration < 0)
                {
                    duration =
                        0;
                }
            }


            SessionMetadata metadata =
                new SessionMetadata();


            metadata.metadata_schema_version =
                "1.0";


            metadata.study_id =
                studyId;


            metadata.participant_id =
                participantId;


            metadata.session_id =
                sessionId;


            metadata.headset =
                headsetName;


            metadata.eye_tracking_feature_enabled =
                eyeTrackingFeatureEnabled;


            metadata.facial_tracking_feature_enabled =
                facialTrackingFeatureEnabled;


            metadata.spectator_camera_feature_enabled =
                spectatorCameraFeatureEnabled;


            metadata.requested_sample_rate_hz =
                sampleRateHz;


            metadata.session_start_utc =
                sessionStartUtc == default(DateTime)
                ?
                ""
                :
                sessionStartUtc.ToString(
                    "O",
                    culture
                );


            metadata.session_end_utc =
                completed
                ?
                DateTime.UtcNow.ToString(
                    "O",
                    culture
                )
                :
                "";


            metadata.session_duration_s =
                duration;


            metadata.samples_written =
                sampleIndex;


            metadata.csv_file =
                csvFileName;


            metadata.metadata_file =
                metadataFileName;


            metadata.video_file =
                videoFileName;


            /*
             * Video integration comes in the next stage.
             */
            metadata.video_recorded =
                false;


            metadata.unity_version =
                Application.unityVersion;


            metadata.application_version =
                Application.version;


            metadata.runtime_platform =
                Application.platform.ToString();


            metadata.recording_status =
                completed
                ?
                "complete"
                :
                "recording";


            string json =
                JsonUtility.ToJson(
                    metadata,
                    true
                );


            File.WriteAllText(
                metadataFilePath,
                json,
                new UTF8Encoding(false)
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "ImmersiveEye Recorder: Could not write metadata JSON.\n"
                +
                exception
            );
        }
    }


    // ============================================================
    // CSV HEADER
    // ============================================================

    private void WriteHeader()
    {
        StringBuilder header =
            new StringBuilder(
                4096
            );


        header.Append(
            "study_id,"
            +
            "participant_id,"
            +
            "session_id,"
            +
            "trial_id,"
            +
            "condition,"
            +
            "stimulus_id,"
            +
            "sample_index,"
            +
            "timestamp_utc,"
            +
            "session_time_s,"
            +
            "event_type,"
            +
            "event_name,"
            +
            "event_value,"
        );


        header.Append(
            "head_x,"
            +
            "head_y,"
            +
            "head_z,"
            +
            "head_rot_x,"
            +
            "head_rot_y,"
            +
            "head_rot_z,"
            +
            "head_rot_w,"
        );


        header.Append(
            "left_gaze_valid,"
            +
            "left_gaze_pos_x,"
            +
            "left_gaze_pos_y,"
            +
            "left_gaze_pos_z,"
            +
            "left_gaze_rot_x,"
            +
            "left_gaze_rot_y,"
            +
            "left_gaze_rot_z,"
            +
            "left_gaze_rot_w,"
        );


        header.Append(
            "right_gaze_valid,"
            +
            "right_gaze_pos_x,"
            +
            "right_gaze_pos_y,"
            +
            "right_gaze_pos_z,"
            +
            "right_gaze_rot_x,"
            +
            "right_gaze_rot_y,"
            +
            "right_gaze_rot_z,"
            +
            "right_gaze_rot_w,"
        );


        header.Append(
            "left_pupil_diameter,"
            +
            "left_pupil_diameter_valid,"
            +
            "left_pupil_pos_x,"
            +
            "left_pupil_pos_y,"
            +
            "left_pupil_position_valid,"
        );


        header.Append(
            "right_pupil_diameter,"
            +
            "right_pupil_diameter_valid,"
            +
            "right_pupil_pos_x,"
            +
            "right_pupil_pos_y,"
            +
            "right_pupil_position_valid,"
        );


        header.Append(
            "left_eye_openness,"
            +
            "left_eye_squeeze,"
            +
            "left_eye_wide,"
            +
            "left_eye_geometry_valid,"
            +
            "right_eye_openness,"
            +
            "right_eye_squeeze,"
            +
            "right_eye_wide,"
            +
            "right_eye_geometry_valid,"
        );


        header.Append(
            "facial_eye_valid"
        );


        for (
            int i = 0;
            i < FacialEyeNames.Length;
            i++
        )
        {
            header.Append(
                ",facial_eye_"
                +
                FacialEyeNames[i]
            );
        }


        header.Append(
            ",facial_lip_valid"
        );


        for (
            int i = 0;
            i < FacialLipNames.Length;
            i++
        )
        {
            header.Append(
                ",facial_lip_"
                +
                FacialLipNames[i]
            );
        }


        writer.WriteLine(
            header.ToString()
        );


        writer.Flush();
    }


    // ============================================================
    // RECORD SAMPLE
    // ============================================================

    private void RecordSample()
    {
        double now =
            Time.realtimeSinceStartupAsDouble;


        double sessionTime =
            now
            -
            sessionStartTime;


        string timestamp =
            DateTime.UtcNow.ToString(
                "O",
                culture
            );


        // ========================================================
        // HEAD
        // ========================================================

        Vector3 headPosition =
            Vector3.zero;


        Quaternion headRotation =
            Quaternion.identity;


        if (headTransform != null)
        {
            headPosition =
                headTransform.position;


            headRotation =
                headTransform.rotation;
        }


        // ========================================================
        // DEFAULT EYE VALUES
        // ========================================================

        bool leftGazeValid =
            false;


        bool rightGazeValid =
            false;


        Vector3 leftGazePosition =
            Vector3.zero;


        Vector3 rightGazePosition =
            Vector3.zero;


        Quaternion leftGazeRotation =
            Quaternion.identity;


        Quaternion rightGazeRotation =
            Quaternion.identity;


        float leftPupilDiameter =
            float.NaN;


        float rightPupilDiameter =
            float.NaN;


        bool leftPupilDiameterValid =
            false;


        bool rightPupilDiameterValid =
            false;


        Vector2 leftPupilPosition =
            Vector2.zero;


        Vector2 rightPupilPosition =
            Vector2.zero;


        bool leftPupilPositionValid =
            false;


        bool rightPupilPositionValid =
            false;


        float leftEyeOpenness =
            float.NaN;


        float rightEyeOpenness =
            float.NaN;


        float leftEyeSqueeze =
            float.NaN;


        float rightEyeSqueeze =
            float.NaN;


        float leftEyeWide =
            float.NaN;


        float rightEyeWide =
            float.NaN;


        bool leftGeometryValid =
            false;


        bool rightGeometryValid =
            false;


        bool facialEyeValid =
            false;


        bool facialLipValid =
            false;


        float[] facialEyeExpressions =
            null;


        float[] facialLipExpressions =
            null;


        // ========================================================
        // HTC HARDWARE
        // ========================================================

#if !UNITY_EDITOR && UNITY_ANDROID

        // --------------------------------------------------------
        // GAZE
        // --------------------------------------------------------

        try
        {
            XR_HTC_eye_tracker.Interop.GetEyeGazeData(
                out XrSingleEyeGazeDataHTC[] gazeData
            );


            if (
                gazeData != null
                &&
                gazeData.Length >= 2
            )
            {
                XrSingleEyeGazeDataHTC left =
                    gazeData[
                        (int)
                        XrEyePositionHTC
                        .XR_EYE_POSITION_LEFT_HTC
                    ];


                XrSingleEyeGazeDataHTC right =
                    gazeData[
                        (int)
                        XrEyePositionHTC
                        .XR_EYE_POSITION_RIGHT_HTC
                    ];


                leftGazeValid =
                    left.isValid;


                rightGazeValid =
                    right.isValid;


                if (leftGazeValid)
                {
                    leftGazePosition =
                        left
                        .gazePose
                        .position
                        .ToUnityVector();


                    leftGazeRotation =
                        left
                        .gazePose
                        .orientation
                        .ToUnityQuaternion();
                }


                if (rightGazeValid)
                {
                    rightGazePosition =
                        right
                        .gazePose
                        .position
                        .ToUnityVector();


                    rightGazeRotation =
                        right
                        .gazePose
                        .orientation
                        .ToUnityQuaternion();
                }
            }
        }
        catch
        {
        }


        // --------------------------------------------------------
        // PUPIL
        // --------------------------------------------------------

        try
        {
            XR_HTC_eye_tracker.Interop.GetEyePupilData(
                out XrSingleEyePupilDataHTC[] pupilData
            );


            if (
                pupilData != null
                &&
                pupilData.Length >= 2
            )
            {
                XrSingleEyePupilDataHTC left =
                    pupilData[
                        (int)
                        XrEyePositionHTC
                        .XR_EYE_POSITION_LEFT_HTC
                    ];


                XrSingleEyePupilDataHTC right =
                    pupilData[
                        (int)
                        XrEyePositionHTC
                        .XR_EYE_POSITION_RIGHT_HTC
                    ];


                leftPupilDiameterValid =
                    left.isDiameterValid;


                rightPupilDiameterValid =
                    right.isDiameterValid;


                leftPupilPositionValid =
                    left.isPositionValid;


                rightPupilPositionValid =
                    right.isPositionValid;


                if (leftPupilDiameterValid)
                {
                    leftPupilDiameter =
                        left.pupilDiameter;
                }


                if (rightPupilDiameterValid)
                {
                    rightPupilDiameter =
                        right.pupilDiameter;
                }


                if (leftPupilPositionValid)
                {
                    leftPupilPosition =
                        new Vector2(
                            left.pupilPosition.x,
                            left.pupilPosition.y
                        );
                }


                if (rightPupilPositionValid)
                {
                    rightPupilPosition =
                        new Vector2(
                            right.pupilPosition.x,
                            right.pupilPosition.y
                        );
                }
            }
        }
        catch
        {
        }


        // --------------------------------------------------------
        // EYE GEOMETRY
        // --------------------------------------------------------

        try
        {
            XR_HTC_eye_tracker.Interop.GetEyeGeometricData(
                out XrSingleEyeGeometricDataHTC[] geometricData
            );


            if (
                geometricData != null
                &&
                geometricData.Length >= 2
            )
            {
                XrSingleEyeGeometricDataHTC left =
                    geometricData[
                        (int)
                        XrEyePositionHTC
                        .XR_EYE_POSITION_LEFT_HTC
                    ];


                XrSingleEyeGeometricDataHTC right =
                    geometricData[
                        (int)
                        XrEyePositionHTC
                        .XR_EYE_POSITION_RIGHT_HTC
                    ];


                leftGeometryValid =
                    left.isValid;


                rightGeometryValid =
                    right.isValid;


                if (leftGeometryValid)
                {
                    leftEyeOpenness =
                        left.eyeOpenness;


                    leftEyeSqueeze =
                        left.eyeSqueeze;


                    leftEyeWide =
                        left.eyeWide;
                }


                if (rightGeometryValid)
                {
                    rightEyeOpenness =
                        right.eyeOpenness;


                    rightEyeSqueeze =
                        right.eyeSqueeze;


                    rightEyeWide =
                        right.eyeWide;
                }
            }
        }
        catch
        {
        }


        // --------------------------------------------------------
        // FACIAL TRACKING
        // --------------------------------------------------------

        try
        {
            if (OpenXRSettings.Instance != null)
            {
                ViveFacialTracking feature =
                    OpenXRSettings
                    .Instance
                    .GetFeature<ViveFacialTracking>();


                if (feature != null)
                {
                    facialEyeValid =
                        feature.GetFacialExpressions(
                            XrFacialTrackingTypeHTC
                            .XR_FACIAL_TRACKING_TYPE_EYE_DEFAULT_HTC,

                            out facialEyeExpressions
                        );


                    facialLipValid =
                        feature.GetFacialExpressions(
                            XrFacialTrackingTypeHTC
                            .XR_FACIAL_TRACKING_TYPE_LIP_DEFAULT_HTC,

                            out facialLipExpressions
                        );
                }
            }
        }
        catch
        {
            facialEyeValid =
                false;


            facialLipValid =
                false;


            facialEyeExpressions =
                null;


            facialLipExpressions =
                null;
        }

#endif


        // ========================================================
        // EVENT FOR THIS ROW
        // ========================================================

        string rowEventType =
            "";


        string rowEventName =
            "";


        string rowEventValue =
            "";


        if (eventPending)
        {
            rowEventType =
                pendingEventType;


            rowEventName =
                pendingEventName;


            rowEventValue =
                pendingEventValue;


            eventPending =
                false;


            pendingEventType =
                "";


            pendingEventName =
                "";


            pendingEventValue =
                "";
        }


        // ========================================================
        // BUILD CSV ROW
        // ========================================================

        StringBuilder row =
            new StringBuilder(
                4096
            );


        Append(row, studyId);
        Append(row, participantId);
        Append(row, sessionId);

        Append(row, trialId);
        Append(row, condition);
        Append(row, stimulusId);

        Append(row, sampleIndex);
        Append(row, timestamp);
        Append(row, sessionTime);

        Append(row, rowEventType);
        Append(row, rowEventName);
        Append(row, rowEventValue);


        Append(row, headPosition.x);
        Append(row, headPosition.y);
        Append(row, headPosition.z);

        Append(row, headRotation.x);
        Append(row, headRotation.y);
        Append(row, headRotation.z);
        Append(row, headRotation.w);


        Append(row, leftGazeValid);

        Append(row, leftGazePosition.x);
        Append(row, leftGazePosition.y);
        Append(row, leftGazePosition.z);

        Append(row, leftGazeRotation.x);
        Append(row, leftGazeRotation.y);
        Append(row, leftGazeRotation.z);
        Append(row, leftGazeRotation.w);


        Append(row, rightGazeValid);

        Append(row, rightGazePosition.x);
        Append(row, rightGazePosition.y);
        Append(row, rightGazePosition.z);

        Append(row, rightGazeRotation.x);
        Append(row, rightGazeRotation.y);
        Append(row, rightGazeRotation.z);
        Append(row, rightGazeRotation.w);


        Append(row, leftPupilDiameter);
        Append(row, leftPupilDiameterValid);

        Append(row, leftPupilPosition.x);
        Append(row, leftPupilPosition.y);

        Append(row, leftPupilPositionValid);


        Append(row, rightPupilDiameter);
        Append(row, rightPupilDiameterValid);

        Append(row, rightPupilPosition.x);
        Append(row, rightPupilPosition.y);

        Append(row, rightPupilPositionValid);


        Append(row, leftEyeOpenness);
        Append(row, leftEyeSqueeze);
        Append(row, leftEyeWide);
        Append(row, leftGeometryValid);


        Append(row, rightEyeOpenness);
        Append(row, rightEyeSqueeze);
        Append(row, rightEyeWide);
        Append(row, rightGeometryValid);


        Append(row, facialEyeValid);


        for (
            int i = 0;
            i < FacialEyeNames.Length;
            i++
        )
        {
            Append(
                row,
                GetExpressionValue(
                    facialEyeExpressions,
                    i,
                    facialEyeValid
                )
            );
        }


        Append(
            row,
            facialLipValid
        );


        for (
            int i = 0;
            i < FacialLipNames.Length;
            i++
        )
        {
            float value =
                GetExpressionValue(
                    facialLipExpressions,
                    i,
                    facialLipValid
                );


            if (
                i
                ==
                FacialLipNames.Length - 1
            )
            {
                AppendLast(
                    row,
                    value
                );
            }
            else
            {
                Append(
                    row,
                    value
                );
            }
        }


        writer.WriteLine(
            row.ToString()
        );


        sampleIndex++;


        samplesSinceFlush++;


        if (
            samplesSinceFlush
            >=
            Math.Max(
                1,
                flushEverySamples
            )
        )
        {
            writer.Flush();


            samplesSinceFlush =
                0;
        }
    }


    // ============================================================
    // EXPRESSION VALUE
    // ============================================================

    private static float GetExpressionValue(
        float[] expressions,
        int index,
        bool valid
    )
    {
        if (!valid)
        {
            return float.NaN;
        }


        if (
            expressions == null
            ||
            index < 0
            ||
            index >= expressions.Length
        )
        {
            return float.NaN;
        }


        return expressions[index];
    }


    // ============================================================
    // CSV HELPERS
    // ============================================================

    private void Append(
        StringBuilder row,
        string value
    )
    {
        row.Append(
            EscapeCsv(
                value
            )
        );


        row.Append(",");
    }


    private void Append(
        StringBuilder row,
        long value
    )
    {
        row.Append(
            value.ToString(
                culture
            )
        );


        row.Append(",");
    }


    private void Append(
        StringBuilder row,
        double value
    )
    {
        if (
            double.IsNaN(
                value
            )
            ||
            double.IsInfinity(
                value
            )
        )
        {
            row.Append("");
        }
        else
        {
            row.Append(
                value.ToString(
                    "F6",
                    culture
                )
            );
        }


        row.Append(",");
    }


    private void Append(
        StringBuilder row,
        float value
    )
    {
        if (
            float.IsNaN(
                value
            )
            ||
            float.IsInfinity(
                value
            )
        )
        {
            row.Append("");
        }
        else
        {
            row.Append(
                value.ToString(
                    "F6",
                    culture
                )
            );
        }


        row.Append(",");
    }


    private void Append(
        StringBuilder row,
        bool value
    )
    {
        row.Append(
            value
            ?
            "1"
            :
            "0"
        );


        row.Append(",");
    }


    private void AppendLast(
        StringBuilder row,
        float value
    )
    {
        if (
            float.IsNaN(
                value
            )
            ||
            float.IsInfinity(
                value
            )
        )
        {
            row.Append("");
        }
        else
        {
            row.Append(
                value.ToString(
                    "F6",
                    culture
                )
            );
        }
    }


    // ============================================================
    // CLEAN VALUES
    // ============================================================

    private static string CleanValue(
        string value,
        string fallback
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                value
            )
        )
        {
            return fallback;
        }


        return value.Trim();
    }


    private static string CleanFileName(
        string value
    )
    {
        if (
            string.IsNullOrWhiteSpace(
                value
            )
        )
        {
            return "Unknown";
        }


        foreach (
            char character
            in
            Path.GetInvalidFileNameChars()
        )
        {
            value =
                value.Replace(
                    character,
                    '_'
                );
        }


        return value.Trim();
    }


    private static string EscapeCsv(
        string value
    )
    {
        if (value == null)
        {
            return "";
        }


        if (
            value.Contains(",")
            ||
            value.Contains("\"")
            ||
            value.Contains("\n")
            ||
            value.Contains("\r")
        )
        {
            return "\""
                   +
                   value.Replace(
                       "\"",
                       "\"\""
                   )
                   +
                   "\"";
        }


        return value;
    }


    // ============================================================
    // APPLICATION EVENTS
    // ============================================================

    private void OnApplicationPause(
        bool paused
    )
    {
        if (
            paused
            &&
            writer != null
        )
        {
            writer.Flush();


            WriteMetadata(
                false
            );
        }
    }


    private void OnApplicationFocus(
        bool hasFocus
    )
    {
        if (
            !hasFocus
            &&
            writer != null
        )
        {
            writer.Flush();


            WriteMetadata(
                false
            );
        }
    }


    private void OnApplicationQuit()
    {
        StopRecording();
    }


    private void OnDestroy()
    {
        StopRecording();
    }
}