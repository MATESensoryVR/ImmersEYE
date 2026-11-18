using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Runs the existing nine-point validation and adds optional, visual-only eye-gaze
/// colour feedback. Feedback does NOT alter the evaluator's gaze samples, result,
/// pass/fail thresholds, or event callbacks. Requires real XR eye-gaze data.
/// </summary>
public class CalibrationValidationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform headCamera;
    [SerializeField] private Transform validationTargetRoot;
    [SerializeField] private CalibrationValidationEvaluator validationEvaluator;

    [Header("Validation Target Order")]
    [SerializeField] private List<CalibrationValidationTarget> validationTargets =
        new List<CalibrationValidationTarget>();

    [Header("Positioning Settings")]
    [SerializeField] private bool faceCamera = true;

    [Header("Sequence Timing")]
    [Min(0f)] [SerializeField] private float preTargetDelay = 0.5f;
    [Min(0f)] [SerializeField] private float settlingDuration = 0.5f;
    [Min(0.1f)] [SerializeField] private float recordingDuration = 1.5f;
    [Min(0f)] [SerializeField] private float interTargetInterval = 0.5f;

    [Header("Eye Gaze Colour Feedback (Visual Only)")]
    [SerializeField] private bool enableRedGazeFeedback = true;
    [Tooltip("Existing VIVE EyeTrackingProvider used by the gaze-recording system. Assign the EyeTrackingProvider component from the scene.")]
    [SerializeField] private EyeTrackingProvider eyeTrackingProvider;

    [Tooltip("Legacy field preserved for Inspector compatibility. The VIVE provider already converts gaze rays to world space; this field is not used for feedback.")]
    [SerializeField] private Transform eyeGazeTrackingSpace;
    [SerializeField] private Color gazeDetectedColor = Color.red;
    [Range(0.5f, 10f)] [SerializeField] private float gazeThresholdDegrees = 3.0f;
    [Min(0f)] [SerializeField] private float gazeDwellSeconds = 0.08f;
    [Min(0f)] [SerializeField] private float gazeReleaseSeconds = 0.06f;

    [Header("Events")]
    [SerializeField] private UnityEvent onValidationStarted;
    [SerializeField] private UnityEvent onValidationCompleted;

    [Header("Runtime Status")]
    [SerializeField] private bool validationRunning = false;
    [SerializeField] private bool currentlyRecordingGaze = false;
    [SerializeField] private int numberOfTargetsFound = 0;
    [SerializeField] private int currentTargetIndex = -1;
    [SerializeField] private string currentTargetName = "";
    [SerializeField] private string currentPhase = "IDLE";

    private Coroutine validationCoroutine;
    // These two status fields help diagnose headset feedback without altering scoring.
    [Header("Eye Gaze Feedback Diagnostics (Read Only)")]
    [SerializeField] private bool viveGazeAvailableThisFrame;
    [SerializeField] private float lastGazeAngleDegrees = -1f;
    private bool sawValidViveGazeThisValidation;

    private readonly List<Graphic> activeGraphics = new List<Graphic>();
    private readonly List<Color> originalGraphicColors = new List<Color>();
    private readonly List<Renderer> activeRenderers = new List<Renderer>();
    private readonly List<MaterialPropertyBlock> originalRendererBlocks =
        new List<MaterialPropertyBlock>();
    private CalibrationValidationTarget colourTarget;
    private bool gazeColourApplied;
    private float gazeHitDuration;
    private float gazeMissDuration;

    public bool ValidationRunning => validationRunning;
    public bool CurrentlyRecordingGaze => currentlyRecordingGaze;
    public int CurrentTargetIndex => currentTargetIndex;
    public string CurrentTargetName => currentTargetName;
    public string CurrentPhase => currentPhase;

    private void Start()
    {
        // Do not change target order, sequence events or evaluator settings.
        BuildTargetListIfNeeded();
        PositionValidationTargets();
        HideAllTargets();
        if (eyeTrackingProvider == null)
            eyeTrackingProvider = FindFirstObjectByType<EyeTrackingProvider>();
        if (enableRedGazeFeedback && eyeTrackingProvider == null)
            Debug.LogWarning("VALIDATION_GAZE_FEEDBACK: EyeTrackingProvider not assigned or found. Gaze colour will remain unchanged until assigned.");
    }

    private void OnDisable()
    {
        // Restore visuals if the sequence was interrupted by a UI transition.
        RestoreTargetColours();
    }

    private void BuildTargetListIfNeeded()
    {
        if (validationTargets == null)
            validationTargets = new List<CalibrationValidationTarget>();

        if (validationTargets.Count == 0 && validationTargetRoot != null)
        {
            CalibrationValidationTarget[] foundTargets =
                validationTargetRoot.GetComponentsInChildren<CalibrationValidationTarget>(true);
            validationTargets.AddRange(foundTargets);
        }
        numberOfTargetsFound = validationTargets.Count;
    }

    [ContextMenu("Position Validation Targets")]
    public void PositionValidationTargets()
    {
        if (headCamera == null)
        {
            Debug.LogWarning("CalibrationValidationManager: Head Camera is not assigned.");
            return;
        }

        BuildTargetListIfNeeded();
        foreach (CalibrationValidationTarget target in validationTargets)
        {
            if (target != null && target.TargetEnabled)
                PositionSingleTarget(target);
        }
        numberOfTargetsFound = validationTargets.Count;
        Debug.Log("CalibrationValidationManager: Positioned " +
                  numberOfTargetsFound + " validation target(s).");
    }

    private void PositionSingleTarget(CalibrationValidationTarget target)
    {
        Quaternion angularRotation = Quaternion.Euler(
            -target.VerticalAngleDegrees, target.HorizontalAngleDegrees, 0f);
        Vector3 localDirection = angularRotation * Vector3.forward;
        Vector3 worldDirection = headCamera.TransformDirection(localDirection);
        target.transform.position = headCamera.position +
            worldDirection.normalized * target.TargetDistanceMetres;

        if (faceCamera)
        {
            Vector3 directionToCamera = headCamera.position - target.transform.position;
            if (directionToCamera.sqrMagnitude > 0.000001f)
                target.transform.rotation = Quaternion.LookRotation(
                    -directionToCamera.normalized, headCamera.up);
        }
    }

    [ContextMenu("Run Validation Sequence")]
    public void RunValidationSequence()
    {
        if (validationRunning) return;
        BuildTargetListIfNeeded();

        if (validationTargets.Count == 0)
        {
            Debug.LogWarning("CalibrationValidationManager: No validation targets assigned.");
            return;
        }
        if (validationEvaluator == null)
        {
            Debug.LogWarning("CalibrationValidationManager: CalibrationValidationEvaluator is not assigned.");
            return;
        }

        PositionValidationTargets();
        validationCoroutine = StartCoroutine(ValidationSequenceCoroutine());
    }

    private IEnumerator ValidationSequenceCoroutine()
    {
        validationRunning = true;
        currentlyRecordingGaze = false;
        currentTargetIndex = -1;
        currentTargetName = "";
        currentPhase = "STARTING";
        sawValidViveGazeThisValidation = false;
        viveGazeAvailableThisFrame = false;
        lastGazeAngleDegrees = -1f;
        if (eyeTrackingProvider == null)
            eyeTrackingProvider = FindFirstObjectByType<EyeTrackingProvider>();
        HideAllTargets();

        onValidationStarted?.Invoke();
        validationEvaluator.BeginValidation();
        Debug.Log("CALIBRATION_VALIDATION_START");

        for (int i = 0; i < validationTargets.Count; i++)
        {
            CalibrationValidationTarget target = validationTargets[i];
            if (target == null || !target.TargetEnabled) continue;

            currentTargetIndex = i;
            currentTargetName = target.TargetName;
            currentPhase = "PRE_TARGET";
            HideAllTargets();
            if (preTargetDelay > 0f)
                yield return new WaitForSeconds(preTargetDelay);

            target.ShowTarget();
            CaptureTargetColours(target);
            Debug.Log("CALIBRATION_VALIDATION_TARGET_ONSET: " + target.TargetName);

            currentPhase = "SETTLING";
            float elapsed = 0f;
            while (elapsed < settlingDuration)
            {
                UpdateGazeColourFeedback(target, Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            currentPhase = "RECORDING";
            currentlyRecordingGaze = true;
            validationEvaluator.BeginTargetRecording(target);
            Debug.Log("CALIBRATION_VALIDATION_RECORDING_START: " + target.TargetName);

            elapsed = 0f;
            while (elapsed < recordingDuration)
            {
                // Keep original evaluator sampling exactly as before.
                validationEvaluator.SampleCurrentTarget();
                UpdateGazeColourFeedback(target, Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            validationEvaluator.EndTargetRecording();
            currentlyRecordingGaze = false;
            Debug.Log("CALIBRATION_VALIDATION_RECORDING_END: " + target.TargetName);

            RestoreTargetColours();
            target.HideTarget();
            Debug.Log("CALIBRATION_VALIDATION_TARGET_OFFSET: " + target.TargetName);

            currentPhase = "INTER_TARGET";
            if (interTargetInterval > 0f)
                yield return new WaitForSeconds(interTargetInterval);
        }

        HideAllTargets();
        if (enableRedGazeFeedback && !sawValidViveGazeThisValidation)
            Debug.LogWarning("VALIDATION_GAZE_FEEDBACK: No valid VIVE gaze was received during this validation. Check EyeTrackingProvider runtime status and headset eye tracking. Validation scoring is unchanged.");
        validationEvaluator.CompleteValidation();
        currentTargetIndex = -1;
        currentTargetName = "";
        currentPhase = "COMPLETE";
        currentlyRecordingGaze = false;
        validationRunning = false;
        validationCoroutine = null;
        Debug.Log("CALIBRATION_VALIDATION_COMPLETE");
        Debug.Log("CALIBRATION_VALIDATION_FINAL_RESULT: " +
                  validationEvaluator.ValidationResult);
        onValidationCompleted?.Invoke();
    }

    public void StopValidationSequence()
    {
        if (validationCoroutine != null)
        {
            StopCoroutine(validationCoroutine);
            validationCoroutine = null;
        }
        if (currentlyRecordingGaze && validationEvaluator != null)
            validationEvaluator.EndTargetRecording();

        HideAllTargets();
        validationRunning = false;
        currentlyRecordingGaze = false;
        currentTargetIndex = -1;
        currentTargetName = "";
        currentPhase = "STOPPED";
        Debug.Log("CALIBRATION_VALIDATION_STOPPED");
    }

    [ContextMenu("Hide All Validation Targets")]
    public void HideAllTargets()
    {
        RestoreTargetColours();
        BuildTargetListIfNeeded();
        foreach (CalibrationValidationTarget target in validationTargets)
            if (target != null) target.HideTarget();
    }

    [ContextMenu("Show All Validation Targets")]
    public void ShowAllTargets()
    {
        RestoreTargetColours();
        BuildTargetListIfNeeded();
        foreach (CalibrationValidationTarget target in validationTargets)
            if (target != null && target.TargetEnabled) target.ShowTarget();
    }

    // ------------------------------------------------------------
    // PASSIVE RED FEEDBACK: uses eye tracking only, never head gaze.
    // This is NOT used for validation scoring.
    // ------------------------------------------------------------
    private void UpdateGazeColourFeedback(CalibrationValidationTarget target, float deltaTime)
    {
        if (!enableRedGazeFeedback || target == null) return;

        viveGazeAvailableThisFrame = TryGetViveEyeGazeRay(
            out Vector3 gazeOrigin, out Vector3 gazeDirection);
        if (viveGazeAvailableThisFrame)
            sawValidViveGazeThisValidation = true;

        Vector3 toTarget = target.transform.position - gazeOrigin;
        bool targetAhead = viveGazeAvailableThisFrame &&
                           toTarget.sqrMagnitude > 0.000001f &&
                           Vector3.Dot(gazeDirection, toTarget) > 0f;
        lastGazeAngleDegrees = targetAhead
            ? Vector3.Angle(gazeDirection, toTarget) : -1f;
        bool eyeHitsTarget = targetAhead &&
                             lastGazeAngleDegrees <= gazeThresholdDegrees;

        if (eyeHitsTarget)
        {
            gazeHitDuration += deltaTime;
            gazeMissDuration = 0f;
            if (!gazeColourApplied && gazeHitDuration >= gazeDwellSeconds)
            {
                ApplyGazeColour();
                Debug.Log("VALIDATION_GAZE_RED_ON|target=" + target.TargetName);
            }
        }
        else
        {
            gazeMissDuration += deltaTime;
            gazeHitDuration = 0f;
            if (gazeColourApplied && gazeMissDuration >= gazeReleaseSeconds)
            {
                RestoreVisualsOnly();
                Debug.Log("VALIDATION_GAZE_RED_OFF|target=" + target.TargetName);
            }
        }
    }

    // Use EXACTLY the same valid VIVE OpenXR gaze source as GazeRecorder.
    // EyeTrackingProvider's CombinedGaze...World properties already apply
    // XR Origin tracking-space conversion; do not transform through Camera Offset again.
    // No head or controller fallback is permitted for eye-gaze feedback.
    private bool TryGetViveEyeGazeRay(out Vector3 worldOrigin,
                                      out Vector3 worldDirection)
    {
        worldOrigin = Vector3.zero;
        worldDirection = Vector3.forward;

        if (eyeTrackingProvider == null || !eyeTrackingProvider.isActiveAndEnabled ||
            !eyeTrackingProvider.GazeDataAvailable ||
            !eyeTrackingProvider.CombinedGazeValid)
            return false;

        Vector3 origin = eyeTrackingProvider.CombinedGazeOriginWorld;
        Vector3 direction = eyeTrackingProvider.CombinedGazeDirectionWorld;

        if (!IsFinite(origin) || !IsFinite(direction) ||
            direction.sqrMagnitude <= 0.000001f)
            return false;

        worldOrigin = origin;
        worldDirection = direction.normalized;
        return true;
    }

    private static bool IsFinite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
               !float.IsNaN(v.y) && !float.IsInfinity(v.y) &&
               !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }

    private void CaptureTargetColours(CalibrationValidationTarget target)
    {
        RestoreTargetColours();
        colourTarget = target;
        gazeColourApplied = false;
        gazeHitDuration = 0f;
        gazeMissDuration = 0f;

        foreach (Graphic graphic in target.GetComponentsInChildren<Graphic>(true))
        {
            activeGraphics.Add(graphic);
            originalGraphicColors.Add(graphic.color);
        }
        foreach (Renderer targetRenderer in target.GetComponentsInChildren<Renderer>(true))
        {
            activeRenderers.Add(targetRenderer);
            MaterialPropertyBlock original = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(original);
            originalRendererBlocks.Add(original);
        }
    }

    private void ApplyGazeColour()
    {
        if (colourTarget == null) return;
        for (int i = 0; i < activeGraphics.Count; i++)
            if (activeGraphics[i] != null)
                activeGraphics[i].color = gazeDetectedColor;

        foreach (Renderer targetRenderer in activeRenderers)
        {
            if (targetRenderer == null || targetRenderer.sharedMaterial == null) continue;
            Material material = targetRenderer.sharedMaterial;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(block);
            if (material.HasProperty("_BaseColor"))
                block.SetColor("_BaseColor", gazeDetectedColor);
            if (material.HasProperty("_Color"))
                block.SetColor("_Color", gazeDetectedColor);
            targetRenderer.SetPropertyBlock(block);
        }
        gazeColourApplied = true;
    }

    private void RestoreVisualsOnly()
    {
        for (int i = 0; i < activeGraphics.Count; i++)
            if (activeGraphics[i] != null)
                activeGraphics[i].color = originalGraphicColors[i];
        for (int i = 0; i < activeRenderers.Count; i++)
            if (activeRenderers[i] != null)
                activeRenderers[i].SetPropertyBlock(originalRendererBlocks[i]);
        gazeColourApplied = false;
    }

    private void RestoreTargetColours()
    {
        RestoreVisualsOnly();
        activeGraphics.Clear();
        originalGraphicColors.Clear();
        activeRenderers.Clear();
        originalRendererBlocks.Clear();
        colourTarget = null;
        gazeHitDuration = 0f;
        gazeMissDuration = 0f;
    }
}
