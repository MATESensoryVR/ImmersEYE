using UnityEngine;


public class CalibrationValidationTarget : MonoBehaviour
{
    // ============================================================
    // IDENTIFICATION
    // ============================================================

    [Header("Target Identification")]

    [Tooltip(
        "Human-readable name of this validation point. "
        + "Examples: Centre, Left, Right, Up, Down."
    )]
    [SerializeField]
    private string targetName = "Centre";


    [Tooltip(
        "Unique identifier exported with calibration data."
    )]
    [SerializeField]
    private string targetId = "CAL_CENTER";


    // ============================================================
    // EXPECTED VISUAL ANGLE
    // ============================================================

    [Header("Expected Visual Angle")]

    [Tooltip(
        "Horizontal visual angle in degrees. "
        + "Negative = left, positive = right."
    )]
    [SerializeField]
    private float horizontalAngleDegrees = 0.0f;


    [Tooltip(
        "Vertical visual angle in degrees. "
        + "Negative = down, positive = up."
    )]
    [SerializeField]
    private float verticalAngleDegrees = 0.0f;


    [Tooltip(
        "Distance of the target from the participant/camera in metres."
    )]
    [SerializeField]
    private float targetDistanceMetres = 2.0f;


    // ============================================================
    // SETTINGS
    // ============================================================

    [Header("Settings")]

    [Tooltip(
        "Whether this validation point should be included."
    )]
    [SerializeField]
    private bool targetEnabled = true;


    // ============================================================
    // RUNTIME
    // ============================================================

    [Header("Runtime Status")]

    [SerializeField]
    private bool currentlyPresented = false;


    // ============================================================
    // PUBLIC READ-ONLY ACCESS
    // ============================================================

    public string TargetName =>
        targetName;


    public string TargetId =>
        targetId;


    public float HorizontalAngleDegrees =>
        horizontalAngleDegrees;


    public float VerticalAngleDegrees =>
        verticalAngleDegrees;


    public float TargetDistanceMetres =>
        targetDistanceMetres;


    public bool TargetEnabled =>
        targetEnabled;


    public bool CurrentlyPresented =>
        currentlyPresented;


    public Vector3 WorldPosition =>
        transform.position;


    // ============================================================
    // PUBLIC METHODS
    // ============================================================

    public void ShowTarget()
    {
        currentlyPresented = true;

        gameObject.SetActive(true);
    }


    public void HideTarget()
    {
        currentlyPresented = false;

        gameObject.SetActive(false);
    }
}