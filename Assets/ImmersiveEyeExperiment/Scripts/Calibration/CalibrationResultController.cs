using UnityEngine;

public class CalibrationResultController : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]
    [Tooltip("The Calibration Validation Evaluator on CalibrationValidationManager.")]
    [SerializeField] private CalibrationValidationEvaluator validationEvaluator;

    [Header("Approved Transition to Block A")]
    [Tooltip("ExperimentManager with ImmersiveEyeExperimentFlow attached.")]
    [SerializeField] private ImmersiveEyeExperimentFlow experimentFlow;

    [Tooltip("The ResultCard that is hidden when approval succeeds.")]
    [SerializeField] private GameObject resultCard;

    // ============================================================
    // RUNTIME RESULT
    // ============================================================

    [Header("Runtime Result")]
    [SerializeField] private string validationResult = "NOT RUN";
    [SerializeField] private float meanAngularError = float.NaN;
    [SerializeField] private float medianAngularError = float.NaN;
    [SerializeField] private float maximumAngularError = float.NaN;
    [SerializeField] private float validSamplePercentage = 0.0f;

    // ============================================================
    // RESEARCHER DECISION
    // ============================================================

    [Header("Researcher Decision")]
    [SerializeField] private bool calibrationAccepted = false;
    [SerializeField] private bool recalibrationRequested = false;
    [SerializeField] private bool continueAnywayOverride = false;
    [SerializeField] private string finalDecision = "PENDING";

    // ============================================================
    // ATTEMPT
    // ============================================================

    [Header("Calibration Attempt")]
    [Min(1)]
    [SerializeField] private int calibrationAttempt = 1;

    // ============================================================
    // PUBLIC ACCESS - KEEP EXISTING API
    // ============================================================

    public string ValidationResult => validationResult;
    public float MeanAngularError => meanAngularError;
    public float MedianAngularError => medianAngularError;
    public float MaximumAngularError => maximumAngularError;
    public float ValidSamplePercentage => validSamplePercentage;
    public bool CalibrationAccepted => calibrationAccepted;
    public bool RecalibrationRequested => recalibrationRequested;
    public bool ContinueAnywayOverride => continueAnywayOverride;
    public string FinalDecision => finalDecision;
    public int CalibrationAttempt => calibrationAttempt;

    // ============================================================
    // RECEIVE VALIDATION RESULT
    // ============================================================

    public void ProcessValidationResult()
    {
        if (validationEvaluator == null)
        {
            Debug.LogWarning(
                "CalibrationResultController: Validation Evaluator is not assigned.");
            return;
        }

        validationResult = validationEvaluator.ValidationResult;
        meanAngularError = validationEvaluator.OverallMeanAngularError;
        medianAngularError = validationEvaluator.OverallMedianAngularError;
        maximumAngularError = validationEvaluator.OverallMaximumAngularError;
        validSamplePercentage = validationEvaluator.OverallValidSamplePercentage;

        calibrationAccepted = false;
        recalibrationRequested = false;
        continueAnywayOverride = false;

        finalDecision = validationResult == "PASS"
            ? "PASS - AWAITING ACCEPTANCE"
            : "RECALIBRATION RECOMMENDED";

        Debug.Log(
            "CALIBRATION_RESULT_READY" +
            " | Attempt = " + calibrationAttempt +
            " | Result = " + validationResult +
            " | Mean Error = " + meanAngularError.ToString("F3") +
            "° | Valid Samples = " + validSamplePercentage.ToString("F1") + "%");
    }

    // ============================================================
    // ACCEPT ONLY A PASSING VALIDATION
    // ============================================================

    public void AcceptCalibration()
    {
        if (validationResult != "PASS")
        {
            Debug.LogWarning(
                "CalibrationResultController: Normal acceptance blocked. " +
                "Validation must PASS. Request recalibration or use the " +
                "researcher-approved Continue Anyway pathway.");
            return;
        }

        if (!TransitionIsConfigured())
            return;

        calibrationAccepted = true;
        recalibrationRequested = false;
        continueAnywayOverride = false;
        finalDecision = "ACCEPTED";

        Debug.Log("CALIBRATION_ACCEPTED | Attempt = " + calibrationAttempt);
        AdvanceToBlockA();
    }

    // ============================================================
    // REQUEST RECALIBRATION
    // ============================================================

    public void RequestRecalibration()
    {
        calibrationAccepted = false;
        recalibrationRequested = true;
        continueAnywayOverride = false;
        finalDecision = "RECALIBRATION REQUESTED";

        Debug.Log("CALIBRATION_RETRY | Previous Attempt = " + calibrationAttempt);
    }

    // ============================================================
    // EXPLICIT RESEARCHER OVERRIDE AFTER A VALIDATION RUN
    // ============================================================

    public void ContinueAnyway()
    {
        // The override does not bypass the requirement to attempt validation.
        if (string.IsNullOrEmpty(validationResult) || validationResult == "NOT RUN")
        {
            Debug.LogWarning(
                "CalibrationResultController: Override blocked because " +
                "validation has not been completed.");
            return;
        }

        if (!TransitionIsConfigured())
            return;

        calibrationAccepted = true;
        recalibrationRequested = false;
        continueAnywayOverride = true;
        finalDecision = "ACCEPTED WITH OVERRIDE";

        Debug.Log(
            "CALIBRATION_ACCEPTED_WITH_OVERRIDE" +
            " | Attempt = " + calibrationAttempt +
            " | Validation Result = " + validationResult +
            " | Mean Error = " + meanAngularError.ToString("F3") +
            "° | Valid Samples = " + validSamplePercentage.ToString("F1") + "%");

        AdvanceToBlockA();
    }

    private bool TransitionIsConfigured()
    {
        if (experimentFlow != null)
            return true;

        Debug.LogError(
            "CalibrationResultController: Experiment Flow is not assigned. " +
            "Keeping the result screen open.");
        return false;
    }

    private void AdvanceToBlockA()
    {
        if (!calibrationAccepted || experimentFlow == null)
            return;

        if (resultCard != null)
            resultCard.SetActive(false);

        // Calls the existing central flow rather than bypassing its stage state.
        experimentFlow.ShowBlockAIntro();

        // This component is attached to CalibrationResultPanel.
        gameObject.SetActive(false);
    }

    // ============================================================
    // START NEW ATTEMPT
    // ============================================================

    public void BeginNewCalibrationAttempt()
    {
        calibrationAttempt++;
        calibrationAccepted = false;
        recalibrationRequested = false;
        continueAnywayOverride = false;
        validationResult = "NOT RUN";
        finalDecision = "PENDING";
        meanAngularError = float.NaN;
        medianAngularError = float.NaN;
        maximumAngularError = float.NaN;
        validSamplePercentage = 0.0f;

        Debug.Log(
            "CALIBRATION_NEW_ATTEMPT | Attempt = " + calibrationAttempt);
    }
}
