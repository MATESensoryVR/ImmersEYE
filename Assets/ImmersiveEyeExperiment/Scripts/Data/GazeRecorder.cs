using System;
using System.Collections.Generic;
using UnityEngine;

public class GazeRecorder : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]

    [SerializeField]
    private EyeTrackingProvider eyeTrackingProvider;

    [SerializeField]
    private GazeRaycaster gazeRaycaster;

    [SerializeField]
    private Transform headCamera;


    // ============================================================
    // RECORDING SETTINGS
    // ============================================================

    [Header("Recording Settings")]

    [SerializeField]
    private string blockName = "BlockA";

    [SerializeField]
    private string stimulusName = "FoodPlate";

    [Tooltip(
        "Target sampling frequency. "
        + "The recorder will never fabricate multiple gaze samples "
        + "within one Unity frame."
    )]
    [Min(1.0f)]
    [SerializeField]
    private float targetSamplingRateHz = 90.0f;


    // ============================================================
    // RUNTIME STATUS
    // ============================================================

    [Header("Runtime Status")]

    [SerializeField]
    private bool recording = false;

    [SerializeField]
    private int recordedSampleCount = 0;

    [SerializeField]
    private string currentAOIName = "";

    [SerializeField]
    private string currentAOIId = "";

    [SerializeField]
    private float recordingElapsedTime = 0.0f;

    [SerializeField]
    private float achievedSamplingRateHz = 0.0f;


    // ============================================================
    // SAMPLE STRUCTURE
    // ============================================================

    [Serializable]
    public class GazeSample
    {
        public long sampleIndex;

        public string timestampUtc;

        public float unityTime;

        public float recordingElapsedTime;

        public int frameNumber;


        public string blockName;

        public string stimulusName;


        public bool leftGazeValid;

        public bool rightGazeValid;

        public bool combinedGazeValid;


        public Vector3 leftGazeOriginWorld;

        public Vector3 leftGazeDirectionWorld;

        public Vector3 rightGazeOriginWorld;

        public Vector3 rightGazeDirectionWorld;

        public Vector3 combinedGazeOriginWorld;

        public Vector3 combinedGazeDirectionWorld;


        public bool leftPupilDiameterValid;

        public float leftPupilDiameter;

        public bool rightPupilDiameterValid;

        public float rightPupilDiameter;


        public bool leftPupilPositionValid;

        public Vector2 leftPupilPosition;

        public bool rightPupilPositionValid;

        public Vector2 rightPupilPosition;


        public bool leftEyeGeometryValid;

        public float leftEyeOpenness;

        public float leftEyeSqueeze;

        public float leftEyeWide;


        public bool rightEyeGeometryValid;

        public float rightEyeOpenness;

        public float rightEyeSqueeze;

        public float rightEyeWide;


        public Vector3 headPosition;

        public Quaternion headRotation;


        public bool gazeHit;

        public string hitObjectName;

        public Vector3 hitPosition;

        public float hitDistance;


        public bool aoiFound;

        public string aoiName;

        public string aoiCategory;

        public string aoiId;
    }


    // ============================================================
    // INTERNAL DATA
    // ============================================================

    private readonly List<GazeSample> samples =
        new List<GazeSample>();

    private float recordingStartTime;

    private float nextSampleTime;

    private float sampleInterval;


    // ============================================================
    // PUBLIC ACCESS
    // ============================================================

    public bool Recording =>
        recording;

    public int RecordedSampleCount =>
        recordedSampleCount;

    public float AchievedSamplingRateHz =>
        achievedSamplingRateHz;

    public IReadOnlyList<GazeSample> Samples =>
        samples;

    public string BlockName =>
        blockName;

    public string StimulusName =>
        stimulusName;


    // ============================================================
    // PREPARE BLOCK A
    // ============================================================

    public void PrepareBlockARecording()
    {
        if (recording)
        {
            Debug.LogWarning(
                "GazeRecorder: Cannot change recording context while recording."
            );

            return;
        }

        blockName = "BlockA";
        stimulusName = "FoodPlate";

        Debug.Log(
            "GAZE_CONTEXT_SET"
            + " | Block = "
            + blockName
            + " | Stimulus = "
            + stimulusName
        );
    }


    // ============================================================
    // PREPARE BLOCK B
    // ============================================================

    public void PrepareBlockBRecording()
    {
        if (recording)
        {
            Debug.LogWarning(
                "GazeRecorder: Cannot change recording context while recording."
            );

            return;
        }

        blockName = "BlockB";
        stimulusName = "FourPlateFoodDisplay";

        Debug.Log(
            "GAZE_CONTEXT_SET"
            + " | Block = "
            + blockName
            + " | Stimulus = "
            + stimulusName
        );
    }


    // ============================================================
    // PREPARE BLOCK C
    // ============================================================

    public void PrepareBlockCRecording()
    {
        if (recording)
        {
            Debug.LogWarning(
                "GazeRecorder: Cannot change recording context while recording."
            );

            return;
        }

        blockName = "BlockC";
        stimulusName = "ImmersiveDiningFoodDisplay";

        Debug.Log(
            "GAZE_CONTEXT_SET"
            + " | Block = "
            + blockName
            + " | Stimulus = "
            + stimulusName
        );
    }


    // ============================================================
    // PREPARE BLOCK D
    // ============================================================

    public void PrepareBlockDRecording()
    {
        if (recording)
        {
            Debug.LogWarning(
                "GazeRecorder: Cannot change recording context while recording."
            );

            return;
        }

        blockName = "BlockD";
        stimulusName = "InteractiveFoodSelection";

        Debug.Log(
            "GAZE_CONTEXT_SET"
            + " | Block = "
            + blockName
            + " | Stimulus = "
            + stimulusName
        );
    }


    // ============================================================
    // UNITY
    // ============================================================

    private void Update()
    {
        if (!recording)
        {
            return;
        }

        float now =
            Time.unscaledTime;

        recordingElapsedTime =
            now - recordingStartTime;

        /*
         * Record at most ONE sample per Unity frame.
         *
         * This prevents us from fabricating duplicate gaze samples
         * if rendering temporarily falls below 90 FPS.
         */
        if (now >= nextSampleTime)
        {
            RecordSample();

            nextSampleTime +=
                sampleInterval;

            /*
             * If Unity falls substantially behind, resynchronise
             * instead of creating several duplicate samples.
             */
            if (
                now - nextSampleTime
                >
                sampleInterval
            )
            {
                nextSampleTime =
                    now + sampleInterval;
            }
        }

        if (recordingElapsedTime > 0.0f)
        {
            achievedSamplingRateHz =
                recordedSampleCount
                /
                recordingElapsedTime;
        }
    }


    // ============================================================
    // START RECORDING
    // ============================================================

    public void StartRecording()
    {
        if (recording)
        {
            return;
        }

        samples.Clear();

        recordedSampleCount = 0;

        currentAOIName = "";

        currentAOIId = "";

        achievedSamplingRateHz = 0.0f;

        sampleInterval =
            1.0f
            /
            Mathf.Max(
                1.0f,
                targetSamplingRateHz
            );

        recordingStartTime =
            Time.unscaledTime;

        recordingElapsedTime =
            0.0f;

        nextSampleTime =
            recordingStartTime;

        recording =
            true;

        Debug.Log(
            "GAZE_RECORDING_START"
            + " | Block = "
            + blockName
            + " | Stimulus = "
            + stimulusName
            + " | Target Hz = "
            + targetSamplingRateHz.ToString("F1")
        );
    }


    // ============================================================
    // STOP RECORDING
    // ============================================================

    public void StopRecording()
    {
        if (!recording)
        {
            return;
        }

        recording =
            false;

        if (recordingElapsedTime > 0.0f)
        {
            achievedSamplingRateHz =
                recordedSampleCount
                /
                recordingElapsedTime;
        }

        Debug.Log(
            "GAZE_RECORDING_STOP"
            + " | Samples = "
            + recordedSampleCount
            + " | Duration = "
            + recordingElapsedTime.ToString("F3")
            + " s"
            + " | Achieved Hz = "
            + achievedSamplingRateHz.ToString("F2")
        );
    }


    // ============================================================
    // RECORD ONE SAMPLE
    // ============================================================

    private void RecordSample()
    {
        if (eyeTrackingProvider == null)
        {
            return;
        }

        GazeSample sample =
            new GazeSample();

        sample.sampleIndex =
            recordedSampleCount;

        sample.timestampUtc =
            DateTime.UtcNow.ToString("o");

        sample.unityTime =
            Time.unscaledTime;

        sample.recordingElapsedTime =
            Time.unscaledTime
            -
            recordingStartTime;

        sample.frameNumber =
            Time.frameCount;


        sample.blockName =
            blockName;

        sample.stimulusName =
            stimulusName;


        // ========================================================
        // GAZE
        // ========================================================

        sample.leftGazeValid =
            eyeTrackingProvider.LeftGazeValid;

        sample.rightGazeValid =
            eyeTrackingProvider.RightGazeValid;

        sample.combinedGazeValid =
            eyeTrackingProvider.CombinedGazeValid;


        sample.leftGazeOriginWorld =
            eyeTrackingProvider.LeftGazeOriginWorld;

        sample.leftGazeDirectionWorld =
            eyeTrackingProvider.LeftGazeDirectionWorld;

        sample.rightGazeOriginWorld =
            eyeTrackingProvider.RightGazeOriginWorld;

        sample.rightGazeDirectionWorld =
            eyeTrackingProvider.RightGazeDirectionWorld;

        sample.combinedGazeOriginWorld =
            eyeTrackingProvider.CombinedGazeOriginWorld;

        sample.combinedGazeDirectionWorld =
            eyeTrackingProvider.CombinedGazeDirectionWorld;


        // ========================================================
        // PUPIL
        // ========================================================

        sample.leftPupilDiameterValid =
            eyeTrackingProvider.LeftPupilDiameterValid;

        sample.leftPupilDiameter =
            eyeTrackingProvider.LeftPupilDiameter;

        sample.rightPupilDiameterValid =
            eyeTrackingProvider.RightPupilDiameterValid;

        sample.rightPupilDiameter =
            eyeTrackingProvider.RightPupilDiameter;


        sample.leftPupilPositionValid =
            eyeTrackingProvider.LeftPupilPositionValid;

        sample.leftPupilPosition =
            eyeTrackingProvider.LeftPupilPosition;

        sample.rightPupilPositionValid =
            eyeTrackingProvider.RightPupilPositionValid;

        sample.rightPupilPosition =
            eyeTrackingProvider.RightPupilPosition;


        // ========================================================
        // EYE GEOMETRY
        // ========================================================

        sample.leftEyeGeometryValid =
            eyeTrackingProvider.LeftEyeGeometryValid;

        sample.leftEyeOpenness =
            eyeTrackingProvider.LeftEyeOpenness;

        sample.leftEyeSqueeze =
            eyeTrackingProvider.LeftEyeSqueeze;

        sample.leftEyeWide =
            eyeTrackingProvider.LeftEyeWide;


        sample.rightEyeGeometryValid =
            eyeTrackingProvider.RightEyeGeometryValid;

        sample.rightEyeOpenness =
            eyeTrackingProvider.RightEyeOpenness;

        sample.rightEyeSqueeze =
            eyeTrackingProvider.RightEyeSqueeze;

        sample.rightEyeWide =
            eyeTrackingProvider.RightEyeWide;


        // ========================================================
        // HEAD POSE
        // ========================================================

        if (headCamera != null)
        {
            sample.headPosition =
                headCamera.position;

            sample.headRotation =
                headCamera.rotation;
        }


        // ========================================================
        // RAYCAST / AOI
        // ========================================================

        if (gazeRaycaster != null)
        {
            sample.gazeHit =
                gazeRaycaster.GazeHit;

            sample.hitObjectName =
                gazeRaycaster.HitObjectName;

            sample.hitPosition =
                gazeRaycaster.HitPosition;

            sample.hitDistance =
                gazeRaycaster.HitDistance;


            sample.aoiFound =
                gazeRaycaster.AOIFound;

            sample.aoiName =
                gazeRaycaster.AOIName;

            sample.aoiCategory =
                gazeRaycaster.AOICategory;

            sample.aoiId =
                gazeRaycaster.AOIId;


            currentAOIName =
                gazeRaycaster.AOIName;

            currentAOIId =
                gazeRaycaster.AOIId;
        }


        samples.Add(
            sample
        );

        recordedSampleCount++;
    }


    // ============================================================
    // RESET
    // ============================================================

    public void ClearRecordedSamples()
    {
        if (recording)
        {
            return;
        }

        samples.Clear();

        recordedSampleCount = 0;

        recordingElapsedTime = 0.0f;

        achievedSamplingRateHz = 0.0f;

        currentAOIName = "";

        currentAOIId = "";
    }
}