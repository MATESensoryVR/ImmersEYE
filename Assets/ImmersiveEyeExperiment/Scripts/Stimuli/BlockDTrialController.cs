
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Controls Block D food selection.
/// Participants must select exactly four foods before
/// the confirmation panel becomes available.
/// </summary>
public class BlockDTrialController : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]
    [SerializeField] private GameObject blockDEnvironment;
    [SerializeField] private GameObject confirmSelectionUI;
    [SerializeField] private BlockDPlateManager plateManager;
    [SerializeField] private TMP_Text selectionCountText;
    [SerializeField] private TMP_Text feedbackText;

    // ============================================================
    // EVENTS
    // ============================================================

    [Header("Events")]
    [SerializeField] private UnityEvent onBlockDStarted = new UnityEvent();

    [SerializeField] private UnityEvent onBlockDCompleted = new UnityEvent();

    // ============================================================
    // RUNTIME STATUS
    // ============================================================

    [Header("Runtime Status")]
    [SerializeField] private bool blockDRunning = false;

    [SerializeField] private string currentPhase = "IDLE";

    [SerializeField] private float elapsedTime = 0f;

    private string startTimestampUTC;

    public bool BlockDRunning => blockDRunning;
    public string CurrentPhase => currentPhase;
    public float ElapsedTime => elapsedTime;

    // ============================================================
    // INITIALIZATION
    // ============================================================

    private void Start()
    {
        blockDRunning = false;
        currentPhase = "IDLE";
        elapsedTime = 0f;
        startTimestampUTC = "";

        if (blockDEnvironment != null)
        {
            blockDEnvironment.SetActive(false);
        }

        if (confirmSelectionUI != null)
        {
            confirmSelectionUI.SetActive(false);
        }

        Debug.Log("BLOCK_D_CONTROLLER_INITIALIZED");
    }

    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (!blockDRunning)
        {
            return;
        }

        elapsedTime += Time.unscaledDeltaTime;

        UpdateSelectionText();
        UpdateConfirmationVisibility();
    }

    // ============================================================
    // SELECTION COUNT
    // ============================================================

    private void UpdateSelectionText()
    {
        if (selectionCountText != null && plateManager != null)
        {
            selectionCountText.text =
                $"Selected: {plateManager.SelectedCount} / 4";
        }
    }

    // ============================================================
    // CONFIRMATION VISIBILITY
    // ============================================================

    private void UpdateConfirmationVisibility()
    {
        if (confirmSelectionUI == null || plateManager == null)
        {
            return;
        }

        bool exactlyFourSelected =
            plateManager.HasExactlyFourFoods;

        if (confirmSelectionUI.activeSelf != exactlyFourSelected)
        {
            confirmSelectionUI.SetActive(exactlyFourSelected);
        }

        if (exactlyFourSelected)
        {
            currentPhase = "READY_TO_CONFIRM";
        }
        else
        {
            currentPhase = "FOOD_SELECTION";
        }
    }

    // ============================================================
    // START BLOCK D
    // ============================================================

    public void StartBlockD()
    {
        if (blockDRunning)
        {
            Debug.LogWarning(
                "Block D is already running."
            );

            return;
        }

        if (blockDEnvironment == null || plateManager == null)
        {
            Debug.LogError(
                "Block D: Environment or Plate Manager is not assigned."
            );

            return;
        }

        // Hide confirmation panel before starting.
        if (confirmSelectionUI != null)
        {
            confirmSelectionUI.SetActive(false);
        }

        // Show Block D environment.
        blockDEnvironment.SetActive(true);

        // Reset previous selections.
        plateManager.ResetSelections();

        // Enable interaction with food objects.
        plateManager.SetInteractionEnabled(true);

        // Reset runtime values.
        blockDRunning = true;
        currentPhase = "FOOD_SELECTION";
        elapsedTime = 0f;

        startTimestampUTC =
            DateTime.UtcNow.ToString("o");

        if (feedbackText != null)
        {
            feedbackText.text = "";
        }

        UpdateSelectionText();

        Debug.Log(
            "BLOCK_D_START|" + startTimestampUTC
        );

        onBlockDStarted?.Invoke();
    }

    // ============================================================
    // CONFIRM SELECTION
    // ============================================================

    public void ConfirmSelection()
    {
        CompleteBlockD();
    }

    // ============================================================
    // COMPLETE BLOCK D
    // ============================================================

    public void CompleteBlockD()
    {
        if (!blockDRunning)
        {
            return;
        }

        if (plateManager == null ||
            !plateManager.HasExactlyFourFoods)
        {
            if (feedbackText != null)
            {
                feedbackText.text =
                    "Please select exactly four foods before confirming.";
            }

            Debug.LogWarning(
                "BLOCK_D_CONFIRM_REJECTED|Exactly 4 foods are required."
            );

            return;
        }

        List<string> selected =
            plateManager.GetSelectedFoodNamesInSlotOrder();

        string endTimestampUTC =
            DateTime.UtcNow.ToString("o");

        blockDRunning = false;
        currentPhase = "COMPLETE";

        plateManager.SetInteractionEnabled(false);

        Debug.Log(
            "BLOCK_D_END|" + endTimestampUTC
        );

        Debug.Log(
            "BLOCK_D_DURATION_SECONDS|" +
            elapsedTime.ToString(
                "F3",
                System.Globalization.CultureInfo.InvariantCulture
            )
        );

        Debug.Log(
            "BLOCK_D_FINAL_FOODS|" +
            string.Join(",", selected)
        );

        if (confirmSelectionUI != null)
        {
            confirmSelectionUI.SetActive(false);
        }

        if (blockDEnvironment != null)
        {
            blockDEnvironment.SetActive(false);
        }

        // Existing Inspector completion events:
        // 1. Stop gaze recording
        // 2. Save gaze CSV
        // 3. Save Block D food selections
        // 4. Show experiment end screen

        onBlockDCompleted?.Invoke();
    }

    // ============================================================
    // STOP BLOCK D
    // ============================================================

    public void StopBlockD()
    {
        blockDRunning = false;
        currentPhase = "STOPPED";

        if (plateManager != null)
        {
            plateManager.SetInteractionEnabled(false);
        }

        if (confirmSelectionUI != null)
        {
            confirmSelectionUI.SetActive(false);
        }

        if (blockDEnvironment != null)
        {
            blockDEnvironment.SetActive(false);
        }

        Debug.Log("BLOCK_D_STOPPED");
    }
}
