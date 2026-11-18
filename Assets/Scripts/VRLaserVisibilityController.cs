using UnityEngine;

/// <summary>
/// Controls ONLY the appearance of the right-controller laser.
/// Does not disable XR Ray Interactor, input actions, or 3D/UI interaction.
/// Keeps existing serialized fields, preserving Inspector references.
/// </summary>
[DefaultExecutionOrder(500)]
public sealed class VRLaserVisibilityController : MonoBehaviour
{
    [Header("Laser Visual Components (Right Controller)")]
    [SerializeField] private Behaviour lineVisual;
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Show Laser When Any Of These Intro Panels Is Active")]
    [SerializeField] private GameObject[] introPanels;

    [Header("Hide Laser When Any Of These Objects Is Active")]
    [SerializeField] private GameObject[] stimulusAndValidationObjects;

    private bool? previousVisibility;
    private bool warnedMissingComponents;
    private CalibrationValidationManager validationManager;

    private void Awake()
    {
        ResolveVisualComponents();
        ResolveValidationManager();
    }

    private void OnEnable()
    {
        previousVisibility = null;
        ResolveVisualComponents();
        ResolveValidationManager();
    }

    // The nine-point validation can run after its UI panel has been hidden.
    // Inspect the actual sequence state rather than relying on GameObject activation.
    private void ResolveValidationManager()
    {
        if (validationManager == null)
            validationManager = FindFirstObjectByType<CalibrationValidationManager>(FindObjectsInactive.Include);
    }

    private void ResolveVisualComponents()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        // Avoid a hard dependency on a particular XRI namespace/version.
        if (lineVisual == null)
        {
            foreach (Behaviour component in GetComponents<Behaviour>())
            {
                if (component != null && component.GetType().Name == "XRInteractorLineVisual")
                {
                    lineVisual = component;
                    break;
                }
            }
        }

        if (!warnedMissingComponents && (lineVisual == null || lineRenderer == null))
        {
            Debug.LogWarning("VRLaserVisibilityController: XR Interactor Line Visual or Line Renderer not found on this controller. Assign the missing reference in the Inspector.", this);
            warnedMissingComponents = true;
        }
    }

    private void LateUpdate()
    {
        if (lineVisual == null || lineRenderer == null)
            ResolveVisualComponents();

        if (validationManager == null)
            ResolveValidationManager();

        bool validationRunning = validationManager != null && validationManager.ValidationRunning;
        bool showLaser = AnyActive(introPanels)
                         && !AnyActive(stimulusAndValidationObjects)
                         && !validationRunning;

        // Enforce every frame rather than only at a stage transition:
        // the XR Interaction Toolkit may update the visible line during runtime.
        // XR Ray Interactor is intentionally NOT disabled.
        if (lineVisual != null && lineVisual.enabled != showLaser)
            lineVisual.enabled = showLaser;

        if (lineRenderer != null && lineRenderer.enabled != showLaser)
            lineRenderer.enabled = showLaser;

        if (!previousVisibility.HasValue || previousVisibility.Value != showLaser)
        {
            Debug.Log("VR_LASER_VISIBILITY|" + (showLaser ? "VISIBLE" : "HIDDEN"), this);
            previousVisibility = showLaser;
        }
    }

    private static bool AnyActive(GameObject[] objects)
    {
        if (objects == null)
            return false;

        foreach (GameObject item in objects)
        {
            if (item != null && item.activeInHierarchy)
                return true;
        }

        return false;
    }
}
