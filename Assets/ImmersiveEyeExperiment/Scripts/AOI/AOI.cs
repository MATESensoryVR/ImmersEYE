using UnityEngine;


public class AOI : MonoBehaviour
{
    // ============================================================
    // AOI IDENTIFICATION
    // ============================================================

    [Header("AOI Identification")]

    [Tooltip(
        "Human-readable AOI name exported to the dataset. "
        + "Examples: Food, Label, Logo, Text, CalibrationPoint."
    )]
    [SerializeField]
    private string aoiName = "AOI";


    [Tooltip(
        "Optional broader AOI category. "
        + "Examples: Packaging, Food, Text, Calibration."
    )]
    [SerializeField]
    private string aoiCategory = "";


    [Tooltip(
        "Optional identifier useful when several AOIs share the same category."
    )]
    [SerializeField]
    private string aoiId = "";


    // ============================================================
    // SETTINGS
    // ============================================================

    [Header("Settings")]

    [Tooltip(
        "If disabled, this AOI will be ignored by gaze analysis."
    )]
    [SerializeField]
    private bool activeAOI = true;


    // ============================================================
    // PUBLIC READ-ONLY ACCESS
    // ============================================================

    public string AOIName =>
        aoiName;


    public string AOICategory =>
        aoiCategory;


    public string AOIId =>
        aoiId;


    public bool ActiveAOI =>
        activeAOI;
}