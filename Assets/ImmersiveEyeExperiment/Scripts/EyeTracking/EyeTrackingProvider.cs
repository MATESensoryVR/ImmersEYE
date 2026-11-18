using System;
using UnityEngine;

using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;


public class EyeTrackingProvider : MonoBehaviour
{
    // ============================================================
    // TRACKING SPACE
    // ============================================================

    [Header("Tracking Space")]

    [Tooltip(
        "Drag XR Origin (VR) here. "
        + "Raw HTC gaze poses are stored in tracking/local space. "
        + "This transform is used to convert them into Unity world space."
    )]
    [SerializeField]
    private Transform trackingOrigin;


    // ============================================================
    // STATUS
    // ============================================================

    [Header("Runtime Status")]

    [Tooltip(
        "True when HTC eye-tracking hardware calls are enabled. "
        + "They are intentionally disabled in the Mac Unity Editor."
    )]
    [SerializeField]
    private bool hardwareCallsEnabled = false;


    [SerializeField]
    private bool gazeDataAvailable = false;


    [SerializeField]
    private bool pupilDataAvailable = false;


    [SerializeField]
    private bool geometricDataAvailable = false;


    // ============================================================
    // LEFT EYE - GAZE
    // ============================================================

    [Header("Left Eye - Gaze")]

    [SerializeField]
    private bool leftGazeValid = false;


    [Tooltip("Raw gaze origin in HTC/OpenXR tracking space.")]
    [SerializeField]
    private Vector3 leftGazeOriginLocal = Vector3.zero;


    [Tooltip(
        "Gaze direction derived from the HTC gaze-pose orientation."
    )]
    [SerializeField]
    private Vector3 leftGazeDirectionLocal = Vector3.forward;


    [SerializeField]
    private Quaternion leftGazeRotationLocal =
        Quaternion.identity;


    // ============================================================
    // RIGHT EYE - GAZE
    // ============================================================

    [Header("Right Eye - Gaze")]

    [SerializeField]
    private bool rightGazeValid = false;


    [Tooltip("Raw gaze origin in HTC/OpenXR tracking space.")]
    [SerializeField]
    private Vector3 rightGazeOriginLocal = Vector3.zero;


    [Tooltip(
        "Gaze direction derived from the HTC gaze-pose orientation."
    )]
    [SerializeField]
    private Vector3 rightGazeDirectionLocal = Vector3.forward;


    [SerializeField]
    private Quaternion rightGazeRotationLocal =
        Quaternion.identity;


    // ============================================================
    // LEFT EYE - PUPIL
    // ============================================================

    [Header("Left Eye - Pupil")]

    [SerializeField]
    private bool leftPupilDiameterValid = false;


    [SerializeField]
    private float leftPupilDiameter = float.NaN;


    [SerializeField]
    private bool leftPupilPositionValid = false;


    [SerializeField]
    private Vector2 leftPupilPosition = Vector2.zero;


    // ============================================================
    // RIGHT EYE - PUPIL
    // ============================================================

    [Header("Right Eye - Pupil")]

    [SerializeField]
    private bool rightPupilDiameterValid = false;


    [SerializeField]
    private float rightPupilDiameter = float.NaN;


    [SerializeField]
    private bool rightPupilPositionValid = false;


    [SerializeField]
    private Vector2 rightPupilPosition = Vector2.zero;


    // ============================================================
    // LEFT EYE - GEOMETRY
    // ============================================================

    [Header("Left Eye - Geometry")]

    [SerializeField]
    private bool leftEyeGeometryValid = false;


    [SerializeField]
    private float leftEyeOpenness = float.NaN;


    [SerializeField]
    private float leftEyeSqueeze = float.NaN;


    [SerializeField]
    private float leftEyeWide = float.NaN;


    // ============================================================
    // RIGHT EYE - GEOMETRY
    // ============================================================

    [Header("Right Eye - Geometry")]

    [SerializeField]
    private bool rightEyeGeometryValid = false;


    [SerializeField]
    private float rightEyeOpenness = float.NaN;


    [SerializeField]
    private float rightEyeSqueeze = float.NaN;


    [SerializeField]
    private float rightEyeWide = float.NaN;


    // ============================================================
    // PUBLIC READ-ONLY ACCESS
    // ============================================================

    public bool HardwareCallsEnabled =>
        hardwareCallsEnabled;


    public bool GazeDataAvailable =>
        gazeDataAvailable;


    public bool PupilDataAvailable =>
        pupilDataAvailable;


    public bool GeometricDataAvailable =>
        geometricDataAvailable;


    // ------------------------------------------------------------
    // LEFT GAZE
    // ------------------------------------------------------------

    public bool LeftGazeValid =>
        leftGazeValid;


    public Vector3 LeftGazeOriginLocal =>
        leftGazeOriginLocal;


    public Vector3 LeftGazeDirectionLocal =>
        leftGazeDirectionLocal;


    public Quaternion LeftGazeRotationLocal =>
        leftGazeRotationLocal;


    // ------------------------------------------------------------
    // RIGHT GAZE
    // ------------------------------------------------------------

    public bool RightGazeValid =>
        rightGazeValid;


    public Vector3 RightGazeOriginLocal =>
        rightGazeOriginLocal;


    public Vector3 RightGazeDirectionLocal =>
        rightGazeDirectionLocal;


    public Quaternion RightGazeRotationLocal =>
        rightGazeRotationLocal;


    // ------------------------------------------------------------
    // PUPIL
    // ------------------------------------------------------------

    public bool LeftPupilDiameterValid =>
        leftPupilDiameterValid;


    public float LeftPupilDiameter =>
        leftPupilDiameter;


    public bool LeftPupilPositionValid =>
        leftPupilPositionValid;


    public Vector2 LeftPupilPosition =>
        leftPupilPosition;


    public bool RightPupilDiameterValid =>
        rightPupilDiameterValid;


    public float RightPupilDiameter =>
        rightPupilDiameter;


    public bool RightPupilPositionValid =>
        rightPupilPositionValid;


    public Vector2 RightPupilPosition =>
        rightPupilPosition;


    // ------------------------------------------------------------
    // EYE GEOMETRY
    // ------------------------------------------------------------

    public bool LeftEyeGeometryValid =>
        leftEyeGeometryValid;


    public float LeftEyeOpenness =>
        leftEyeOpenness;


    public float LeftEyeSqueeze =>
        leftEyeSqueeze;


    public float LeftEyeWide =>
        leftEyeWide;


    public bool RightEyeGeometryValid =>
        rightEyeGeometryValid;


    public float RightEyeOpenness =>
        rightEyeOpenness;


    public float RightEyeSqueeze =>
        rightEyeSqueeze;


    public float RightEyeWide =>
        rightEyeWide;


    // ============================================================
    // WORLD-SPACE GAZE
    // ============================================================

    public Vector3 LeftGazeOriginWorld
    {
        get
        {
            if (trackingOrigin == null)
            {
                return leftGazeOriginLocal;
            }

            return trackingOrigin.TransformPoint(
                leftGazeOriginLocal
            );
        }
    }


    public Vector3 LeftGazeDirectionWorld
    {
        get
        {
            if (trackingOrigin == null)
            {
                return leftGazeDirectionLocal.normalized;
            }

            return trackingOrigin
                .TransformDirection(
                    leftGazeDirectionLocal
                )
                .normalized;
        }
    }


    public Vector3 RightGazeOriginWorld
    {
        get
        {
            if (trackingOrigin == null)
            {
                return rightGazeOriginLocal;
            }

            return trackingOrigin.TransformPoint(
                rightGazeOriginLocal
            );
        }
    }


    public Vector3 RightGazeDirectionWorld
    {
        get
        {
            if (trackingOrigin == null)
            {
                return rightGazeDirectionLocal.normalized;
            }

            return trackingOrigin
                .TransformDirection(
                    rightGazeDirectionLocal
                )
                .normalized;
        }
    }


    // ============================================================
    // DERIVED COMBINED GAZE
    // ============================================================

    /*
     * HTC's advanced API gives us per-eye gaze poses.
     *
     * This combined gaze is DERIVED by this application.
     * It is not claimed to be a raw HTC combined-gaze variable.
     */

    public bool CombinedGazeValid
    {
        get
        {
            return leftGazeValid
                   ||
                   rightGazeValid;
        }
    }


    public Vector3 CombinedGazeOriginWorld
    {
        get
        {
            if (
                leftGazeValid
                &&
                rightGazeValid
            )
            {
                return (
                    LeftGazeOriginWorld
                    +
                    RightGazeOriginWorld
                )
                * 0.5f;
            }


            if (leftGazeValid)
            {
                return LeftGazeOriginWorld;
            }


            if (rightGazeValid)
            {
                return RightGazeOriginWorld;
            }


            return Vector3.zero;
        }
    }


    public Vector3 CombinedGazeDirectionWorld
    {
        get
        {
            if (
                leftGazeValid
                &&
                rightGazeValid
            )
            {
                Vector3 direction =
                    LeftGazeDirectionWorld
                    +
                    RightGazeDirectionWorld;


                if (
                    direction.sqrMagnitude
                    >
                    0.000001f
                )
                {
                    return direction.normalized;
                }
            }


            if (leftGazeValid)
            {
                return LeftGazeDirectionWorld;
            }


            if (rightGazeValid)
            {
                return RightGazeDirectionWorld;
            }


            return Vector3.forward;
        }
    }


    // ============================================================
    // UNITY UPDATE
    // ============================================================

    private void Update()
    {
        ResetFrameStatus();


#if !UNITY_EDITOR && UNITY_ANDROID

        hardwareCallsEnabled =
            true;


        ReadGazeData();

        ReadPupilData();

        ReadGeometricData();

#else

        /*
         * We deliberately do not call the HTC hardware API
         * while testing inside the Mac Unity Editor.
         *
         * The previous prototype showed that invoking the
         * Android eye-tracker interface without a headset/runtime
         * can interfere with normal Editor testing.
         */

        hardwareCallsEnabled =
            false;

#endif
    }


    // ============================================================
    // RESET FRAME VALIDITY
    // ============================================================

    private void ResetFrameStatus()
    {
        gazeDataAvailable =
            false;


        pupilDataAvailable =
            false;


        geometricDataAvailable =
            false;


        leftGazeValid =
            false;


        rightGazeValid =
            false;


        leftPupilDiameterValid =
            false;


        rightPupilDiameterValid =
            false;


        leftPupilPositionValid =
            false;


        rightPupilPositionValid =
            false;


        leftEyeGeometryValid =
            false;


        rightEyeGeometryValid =
            false;
    }


    // ============================================================
    // READ GAZE
    // ============================================================

    private void ReadGazeData()
    {
        try
        {
            XR_HTC_eye_tracker
                .Interop
                .GetEyeGazeData(
                    out XrSingleEyeGazeDataHTC[] gazeData
                );


            if (
                gazeData == null
                ||
                gazeData.Length < 2
            )
            {
                return;
            }


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
                leftGazeOriginLocal =
                    left
                    .gazePose
                    .position
                    .ToUnityVector();


                leftGazeRotationLocal =
                    left
                    .gazePose
                    .orientation
                    .ToUnityQuaternion();


                leftGazeDirectionLocal =
                    (
                        leftGazeRotationLocal
                        *
                        Vector3.forward
                    )
                    .normalized;
            }


            if (rightGazeValid)
            {
                rightGazeOriginLocal =
                    right
                    .gazePose
                    .position
                    .ToUnityVector();


                rightGazeRotationLocal =
                    right
                    .gazePose
                    .orientation
                    .ToUnityQuaternion();


                rightGazeDirectionLocal =
                    (
                        rightGazeRotationLocal
                        *
                        Vector3.forward
                    )
                    .normalized;
            }


            gazeDataAvailable =
                leftGazeValid
                ||
                rightGazeValid;
        }
        catch (Exception)
        {
            gazeDataAvailable =
                false;


            leftGazeValid =
                false;


            rightGazeValid =
                false;
        }
    }


    // ============================================================
    // READ PUPIL
    // ============================================================

    private void ReadPupilData()
    {
        try
        {
            XR_HTC_eye_tracker
                .Interop
                .GetEyePupilData(
                    out XrSingleEyePupilDataHTC[] pupilData
                );


            if (
                pupilData == null
                ||
                pupilData.Length < 2
            )
            {
                return;
            }


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


            pupilDataAvailable =
                leftPupilDiameterValid
                ||
                rightPupilDiameterValid
                ||
                leftPupilPositionValid
                ||
                rightPupilPositionValid;
        }
        catch (Exception)
        {
            pupilDataAvailable =
                false;
        }
    }


    // ============================================================
    // READ EYE GEOMETRY
    // ============================================================

    private void ReadGeometricData()
    {
        try
        {
            XR_HTC_eye_tracker
                .Interop
                .GetEyeGeometricData(
                    out XrSingleEyeGeometricDataHTC[] geometricData
                );


            if (
                geometricData == null
                ||
                geometricData.Length < 2
            )
            {
                return;
            }


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


            leftEyeGeometryValid =
                left.isValid;


            rightEyeGeometryValid =
                right.isValid;


            if (leftEyeGeometryValid)
            {
                leftEyeOpenness =
                    left.eyeOpenness;


                leftEyeSqueeze =
                    left.eyeSqueeze;


                leftEyeWide =
                    left.eyeWide;
            }


            if (rightEyeGeometryValid)
            {
                rightEyeOpenness =
                    right.eyeOpenness;


                rightEyeSqueeze =
                    right.eyeSqueeze;


                rightEyeWide =
                    right.eyeWide;
            }


            geometricDataAvailable =
                leftEyeGeometryValid
                ||
                rightEyeGeometryValid;
        }
        catch (Exception)
        {
            geometricDataAvailable =
                false;
        }
    }
}