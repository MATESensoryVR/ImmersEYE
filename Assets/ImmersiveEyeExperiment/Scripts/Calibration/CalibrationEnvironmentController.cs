using System.Collections.Generic;
using UnityEngine;


public class CalibrationEnvironmentController : MonoBehaviour
{
    // ============================================================
    // CAMERA
    // ============================================================

    [Header("Camera")]

    [SerializeField]
    private Camera participantCamera;


    [Header("Calibration Background")]

    [SerializeField]
    private Color calibrationBackgroundColor =
        Color.white;


    // ============================================================
    // OBJECTS TO HIDE
    // ============================================================

    [Header("Hide During Calibration")]

    [Tooltip(
        "Drag objects here that must not be visible during "
        + "calibration validation."
    )]
    [SerializeField]
    private List<GameObject> hideDuringCalibration =
        new List<GameObject>();


    // ============================================================
    // RUNTIME
    // ============================================================

    [Header("Runtime Status")]

    [SerializeField]
    private bool calibrationEnvironmentActive = false;


    private CameraClearFlags originalClearFlags;

    private Color originalBackgroundColor;


    private readonly List<bool> originalActiveStates =
        new List<bool>();


    // ============================================================
    // ENTER CALIBRATION ENVIRONMENT
    // ============================================================

    public void EnterCalibrationEnvironment()
    {
        if (calibrationEnvironmentActive)
        {
            return;
        }


        if (participantCamera != null)
        {
            originalClearFlags =
                participantCamera.clearFlags;


            originalBackgroundColor =
                participantCamera.backgroundColor;


            participantCamera.clearFlags =
                CameraClearFlags.SolidColor;


            participantCamera.backgroundColor =
                calibrationBackgroundColor;
        }


        originalActiveStates.Clear();


        foreach (GameObject obj in hideDuringCalibration)
        {
            if (obj == null)
            {
                originalActiveStates.Add(false);

                continue;
            }


            originalActiveStates.Add(
                obj.activeSelf
            );


            obj.SetActive(false);
        }


        calibrationEnvironmentActive =
            true;


        Debug.Log(
            "CALIBRATION_ENVIRONMENT_ENTER"
        );
    }


    // ============================================================
    // EXIT CALIBRATION ENVIRONMENT
    // ============================================================

    public void ExitCalibrationEnvironment()
    {
        if (!calibrationEnvironmentActive)
        {
            return;
        }


        if (participantCamera != null)
        {
            participantCamera.clearFlags =
                originalClearFlags;


            participantCamera.backgroundColor =
                originalBackgroundColor;
        }


        for (
            int i = 0;
            i < hideDuringCalibration.Count;
            i++
        )
        {
            GameObject obj =
                hideDuringCalibration[i];


            if (obj == null)
            {
                continue;
            }


            if (i < originalActiveStates.Count)
            {
                obj.SetActive(
                    originalActiveStates[i]
                );
            }
        }


        calibrationEnvironmentActive =
            false;


        Debug.Log(
            "CALIBRATION_ENVIRONMENT_EXIT"
        );
    }
}