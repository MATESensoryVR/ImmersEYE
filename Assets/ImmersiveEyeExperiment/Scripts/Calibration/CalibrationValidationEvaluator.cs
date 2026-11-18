using System;
using System.Collections.Generic;
using UnityEngine;


public class CalibrationValidationEvaluator : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]

    [SerializeField]
    private EyeTrackingProvider eyeTrackingProvider;

    [SerializeField]
    private Transform headCamera;


    // ============================================================
    // ACCEPTANCE THRESHOLDS
    // ============================================================

    [Header("Acceptance Thresholds")]

    [Tooltip(
        "Maximum acceptable overall mean angular error in degrees."
    )]
    [SerializeField]
    private float maximumAcceptableMeanError = 2.0f;


    [Tooltip(
        "Maximum acceptable mean error for any individual validation point."
    )]
    [SerializeField]
    private float maximumAcceptablePointError = 5.0f;


    [Tooltip(
        "Minimum percentage of valid combined-gaze samples required."
    )]
    [Range(0.0f, 100.0f)]
    [SerializeField]
    private float minimumValidSamplePercentage = 90.0f;


    // ============================================================
    // SAMPLING
    // ============================================================

    [Header("Sampling")]

    [Tooltip(
        "Target calibration sampling frequency. "
        + "At most one sample is stored per Unity frame."
    )]
    [Min(1.0f)]
    [SerializeField]
    private float targetSamplingRateHz = 90.0f;


    // ============================================================
    // CURRENT TARGET
    // ============================================================

    [Header("Runtime - Current Target")]

    [SerializeField]
    private bool recordingTarget = false;

    [SerializeField]
    private string currentTargetName = "";

    [SerializeField]
    private int currentTotalSamples = 0;

    [SerializeField]
    private int currentValidSamples = 0;

    [SerializeField]
    private int currentLeftValidSamples = 0;

    [SerializeField]
    private int currentRightValidSamples = 0;


    // ============================================================
    // OVERALL RESULT
    // ============================================================

    [Header("Runtime - Overall Result")]

    [SerializeField]
    private float overallMeanAngularError = float.NaN;

    [SerializeField]
    private float overallMedianAngularError = float.NaN;

    [SerializeField]
    private float overallMaximumAngularError = float.NaN;

    [SerializeField]
    private float overallValidSamplePercentage = 0.0f;

    [SerializeField]
    private float overallLeftEyeValidPercentage = 0.0f;

    [SerializeField]
    private float overallRightEyeValidPercentage = 0.0f;

    [SerializeField]
    private string validationResult = "NOT RUN";


    // ============================================================
    // RAW SAMPLE
    // ============================================================

    [Serializable]
    public class RawCalibrationSample
    {
        public long sampleIndex;
        public int targetSampleIndex;

        public string timestampUtc;

        public float unityTime;
        public float targetElapsedTime;

        public string targetName;
        public string targetId;

        public float horizontalAngleDegrees;
        public float verticalAngleDegrees;
        public float targetDistanceMetres;

        public Vector3 targetWorldPosition;


        public bool leftGazeValid;
        public bool rightGazeValid;
        public bool combinedGazeValid;


        public Vector3 leftGazeOriginWorld;
        public Vector3 leftGazeDirectionWorld;

        public Vector3 rightGazeOriginWorld;
        public Vector3 rightGazeDirectionWorld;

        public Vector3 combinedGazeOriginWorld;
        public Vector3 combinedGazeDirectionWorld;


        public Vector3 expectedDirectionWorld;


        public bool angularErrorValid;
        public float angularErrorDegrees;


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
    }


    // ============================================================
    // POINT RESULT
    // ============================================================

    [Serializable]
    public class PointResult
    {
        public string targetName;
        public string targetId;

        public float horizontalAngleDegrees;
        public float verticalAngleDegrees;
        public float targetDistanceMetres;

        public Vector3 targetWorldPosition;

        public int totalSamples;
        public int validSamples;

        public float validSamplePercentage;
        public float leftEyeValidPercentage;
        public float rightEyeValidPercentage;

        public float meanAngularError;
        public float medianAngularError;
        public float maximumAngularError;
    }


    // ============================================================
    // INTERNAL
    // ============================================================

    private CalibrationValidationTarget currentTarget;


    private readonly List<float> currentAngularErrors =
        new List<float>();


    private readonly List<float> allAngularErrors =
        new List<float>();


    private readonly List<PointResult> pointResults =
        new List<PointResult>();


    private readonly List<RawCalibrationSample> rawSamples =
        new List<RawCalibrationSample>();


    private int overallTotalSamples = 0;

    private int overallValidSamples = 0;

    private int overallLeftValidSamples = 0;

    private int overallRightValidSamples = 0;


    private float targetRecordingStartTime = 0.0f;

    private float nextSampleTime = 0.0f;

    private float sampleInterval = 0.0f;

    private int currentTargetSampleIndex = 0;


    // ============================================================
    // PUBLIC ACCESS
    // ============================================================

    public float MaximumAcceptableMeanError =>
        maximumAcceptableMeanError;


    public float MaximumAcceptablePointError =>
        maximumAcceptablePointError;


    public float MinimumValidSamplePercentage =>
        minimumValidSamplePercentage;


    public float TargetSamplingRateHz =>
        targetSamplingRateHz;


    public float OverallMeanAngularError =>
        overallMeanAngularError;


    public float OverallMedianAngularError =>
        overallMedianAngularError;


    public float OverallMaximumAngularError =>
        overallMaximumAngularError;


    public float OverallValidSamplePercentage =>
        overallValidSamplePercentage;


    public float OverallLeftEyeValidPercentage =>
        overallLeftEyeValidPercentage;


    public float OverallRightEyeValidPercentage =>
        overallRightEyeValidPercentage;


    public int OverallTotalSamples =>
        overallTotalSamples;


    public int OverallValidSamples =>
        overallValidSamples;


    public string ValidationResult =>
        validationResult;


    public IReadOnlyList<PointResult> PointResults =>
        pointResults;


    public IReadOnlyList<RawCalibrationSample> RawSamples =>
        rawSamples;


    // ============================================================
    // START COMPLETE VALIDATION
    // ============================================================

    public void BeginValidation()
    {
        pointResults.Clear();

        rawSamples.Clear();

        allAngularErrors.Clear();


        overallTotalSamples = 0;

        overallValidSamples = 0;

        overallLeftValidSamples = 0;

        overallRightValidSamples = 0;


        overallMeanAngularError =
            float.NaN;


        overallMedianAngularError =
            float.NaN;


        overallMaximumAngularError =
            float.NaN;


        overallValidSamplePercentage =
            0.0f;


        overallLeftEyeValidPercentage =
            0.0f;


        overallRightEyeValidPercentage =
            0.0f;


        validationResult =
            "RUNNING";


        sampleInterval =
            1.0f
            /
            Mathf.Max(
                1.0f,
                targetSamplingRateHz
            );


        Debug.Log(
            "CALIBRATION_EVALUATOR_BEGIN"
            + " | Target Hz = "
            + targetSamplingRateHz.ToString("F1")
        );
    }


    // ============================================================
    // START TARGET RECORDING
    // ============================================================

    public void BeginTargetRecording(
        CalibrationValidationTarget target
    )
    {
        currentTarget =
            target;


        currentAngularErrors.Clear();


        currentTotalSamples =
            0;


        currentValidSamples =
            0;


        currentLeftValidSamples =
            0;


        currentRightValidSamples =
            0;


        currentTargetSampleIndex =
            0;


        if (target != null)
        {
            currentTargetName =
                target.TargetName;
        }
        else
        {
            currentTargetName =
                "";
        }


        targetRecordingStartTime =
            Time.unscaledTime;


        nextSampleTime =
            targetRecordingStartTime;


        recordingTarget =
            true;
    }


    // ============================================================
    // SAMPLE CURRENT TARGET
    // ============================================================

    public void SampleCurrentTarget()
    {
        if (!recordingTarget)
        {
            return;
        }


        if (currentTarget == null)
        {
            return;
        }


        if (eyeTrackingProvider == null)
        {
            return;
        }


        float now =
            Time.unscaledTime;


        /*
         * Target approximately 90 Hz, but never create several
         * duplicate samples inside one Unity frame.
         */
        if (now < nextSampleTime)
        {
            return;
        }


        nextSampleTime +=
            sampleInterval;


        /*
         * If rendering falls substantially behind,
         * resynchronise instead of fabricating samples.
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


        RawCalibrationSample sample =
            new RawCalibrationSample();


        sample.sampleIndex =
            rawSamples.Count;


        sample.targetSampleIndex =
            currentTargetSampleIndex;


        sample.timestampUtc =
            DateTime.UtcNow.ToString("o");


        sample.unityTime =
            now;


        sample.targetElapsedTime =
            now
            -
            targetRecordingStartTime;


        sample.targetName =
            currentTarget.TargetName;


        sample.targetId =
            currentTarget.TargetId;


        sample.horizontalAngleDegrees =
            currentTarget.HorizontalAngleDegrees;


        sample.verticalAngleDegrees =
            currentTarget.VerticalAngleDegrees;


        sample.targetDistanceMetres =
            currentTarget.TargetDistanceMetres;


        sample.targetWorldPosition =
            currentTarget.WorldPosition;


        // ========================================================
        // VALIDITY
        // ========================================================

        sample.leftGazeValid =
            eyeTrackingProvider.LeftGazeValid;


        sample.rightGazeValid =
            eyeTrackingProvider.RightGazeValid;


        sample.combinedGazeValid =
            eyeTrackingProvider.CombinedGazeValid;


        // ========================================================
        // RAW GAZE
        // ========================================================

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
        // COUNTS
        // ========================================================

        currentTotalSamples++;

        overallTotalSamples++;


        if (sample.leftGazeValid)
        {
            currentLeftValidSamples++;

            overallLeftValidSamples++;
        }


        if (sample.rightGazeValid)
        {
            currentRightValidSamples++;

            overallRightValidSamples++;
        }


        // ========================================================
        // EXPECTED DIRECTION + ANGULAR ERROR
        // ========================================================

        sample.angularErrorValid =
            false;


        sample.angularErrorDegrees =
            float.NaN;


        Vector3 gazeOrigin =
            sample.combinedGazeOriginWorld;


        Vector3 measuredDirection =
            sample.combinedGazeDirectionWorld.normalized;


        Vector3 expectedDirection =
            (
                currentTarget.WorldPosition
                -
                gazeOrigin
            )
            .normalized;


        sample.expectedDirectionWorld =
            expectedDirection;


        if (
            sample.combinedGazeValid
            &&
            measuredDirection.sqrMagnitude
            >
            0.000001f
            &&
            expectedDirection.sqrMagnitude
            >
            0.000001f
        )
        {
            float angularError =
                Vector3.Angle(
                    expectedDirection,
                    measuredDirection
                );


            sample.angularErrorValid =
                true;


            sample.angularErrorDegrees =
                angularError;


            currentAngularErrors.Add(
                angularError
            );


            allAngularErrors.Add(
                angularError
            );


            currentValidSamples++;

            overallValidSamples++;
        }


        rawSamples.Add(
            sample
        );


        currentTargetSampleIndex++;
    }


    // ============================================================
    // END TARGET
    // ============================================================

    public void EndTargetRecording()
    {
        if (!recordingTarget)
        {
            return;
        }


        recordingTarget =
            false;


        if (currentTarget == null)
        {
            return;
        }


        PointResult result =
            new PointResult();


        result.targetName =
            currentTarget.TargetName;


        result.targetId =
            currentTarget.TargetId;


        result.horizontalAngleDegrees =
            currentTarget.HorizontalAngleDegrees;


        result.verticalAngleDegrees =
            currentTarget.VerticalAngleDegrees;


        result.targetDistanceMetres =
            currentTarget.TargetDistanceMetres;


        result.targetWorldPosition =
            currentTarget.WorldPosition;


        result.totalSamples =
            currentTotalSamples;


        result.validSamples =
            currentValidSamples;


        result.validSamplePercentage =
            CalculatePercentage(
                currentValidSamples,
                currentTotalSamples
            );


        result.leftEyeValidPercentage =
            CalculatePercentage(
                currentLeftValidSamples,
                currentTotalSamples
            );


        result.rightEyeValidPercentage =
            CalculatePercentage(
                currentRightValidSamples,
                currentTotalSamples
            );


        result.meanAngularError =
            CalculateMean(
                currentAngularErrors
            );


        result.medianAngularError =
            CalculateMedian(
                currentAngularErrors
            );


        result.maximumAngularError =
            CalculateMaximum(
                currentAngularErrors
            );


        pointResults.Add(
            result
        );


        Debug.Log(
            "VALIDATION POINT: "
            + result.targetName
            + " | Samples = "
            + result.totalSamples
            + " | Mean Error = "
            + result.meanAngularError.ToString("F3")
            + "° | Median = "
            + result.medianAngularError.ToString("F3")
            + "° | Max = "
            + result.maximumAngularError.ToString("F3")
            + "° | Valid = "
            + result.validSamplePercentage.ToString("F1")
            + "%"
        );


        currentTarget =
            null;


        currentTargetName =
            "";
    }


    // ============================================================
    // COMPLETE VALIDATION
    // ============================================================

    public void CompleteValidation()
    {
        overallMeanAngularError =
            CalculateMean(
                allAngularErrors
            );


        overallMedianAngularError =
            CalculateMedian(
                allAngularErrors
            );


        overallMaximumAngularError =
            CalculateMaximum(
                allAngularErrors
            );


        overallValidSamplePercentage =
            CalculatePercentage(
                overallValidSamples,
                overallTotalSamples
            );


        overallLeftEyeValidPercentage =
            CalculatePercentage(
                overallLeftValidSamples,
                overallTotalSamples
            );


        overallRightEyeValidPercentage =
            CalculatePercentage(
                overallRightValidSamples,
                overallTotalSamples
            );


        bool meanPassed =
            !float.IsNaN(
                overallMeanAngularError
            )
            &&
            overallMeanAngularError
            <=
            maximumAcceptableMeanError;


        bool validityPassed =
            overallValidSamplePercentage
            >=
            minimumValidSamplePercentage;


        bool allPointsPassed =
            true;


        foreach (
            PointResult result
            in pointResults
        )
        {
            if (
                float.IsNaN(
                    result.meanAngularError
                )
                ||
                result.meanAngularError
                >
                maximumAcceptablePointError
            )
            {
                allPointsPassed =
                    false;

                break;
            }
        }


        if (
            meanPassed
            &&
            validityPassed
            &&
            allPointsPassed
        )
        {
            validationResult =
                "PASS";
        }
        else
        {
            validationResult =
                "RECALIBRATION RECOMMENDED";
        }


        Debug.Log(
            "CALIBRATION VALIDATION RESULT: "
            + validationResult
            + " | Samples = "
            + overallTotalSamples
            + " | Overall Mean Error = "
            + overallMeanAngularError.ToString("F3")
            + "° | Overall Median = "
            + overallMedianAngularError.ToString("F3")
            + "° | Maximum = "
            + overallMaximumAngularError.ToString("F3")
            + "° | Valid Samples = "
            + overallValidSamplePercentage.ToString("F1")
            + "%"
        );
    }


    // ============================================================
    // CALCULATIONS
    // ============================================================

    private float CalculatePercentage(
        int valid,
        int total
    )
    {
        if (total <= 0)
        {
            return 0.0f;
        }


        return
            (
                (float)valid
                /
                total
            )
            *
            100.0f;
    }


    private float CalculateMean(
        List<float> values
    )
    {
        if (
            values == null
            ||
            values.Count == 0
        )
        {
            return float.NaN;
        }


        float total =
            0.0f;


        foreach (
            float value
            in values
        )
        {
            total +=
                value;
        }


        return
            total
            /
            values.Count;
    }


    private float CalculateMedian(
        List<float> values
    )
    {
        if (
            values == null
            ||
            values.Count == 0
        )
        {
            return float.NaN;
        }


        List<float> sorted =
            new List<float>(
                values
            );


        sorted.Sort();


        int count =
            sorted.Count;


        if (
            count
            %
            2
            ==
            1
        )
        {
            return sorted[
                count / 2
            ];
        }


        return
            (
                sorted[
                    (count / 2) - 1
                ]
                +
                sorted[
                    count / 2
                ]
            )
            /
            2.0f;
    }


    private float CalculateMaximum(
        List<float> values
    )
    {
        if (
            values == null
            ||
            values.Count == 0
        )
        {
            return float.NaN;
        }


        float maximum =
            values[0];


        foreach (
            float value
            in values
        )
        {
            if (value > maximum)
            {
                maximum =
                    value;
            }
        }


        return maximum;
    }
}