using TMPro;
using UnityEngine;

/// <summary>
/// Independent live Block D count. The source count is kept by the existing BlockDTrialController.
/// The HUD must live outside BlockDConfirmPanel, whose activation is conditional.
/// Food transform movement is used only as a secondary count source; no experiment logic is changed.
/// </summary>
[DisallowMultipleComponent]
public sealed class ImmersiveEyeBlockDCounterHUD : MonoBehaviour
{
    [SerializeField] private GameObject environment;
    [SerializeField] private GameObject introPanel;
    [SerializeField] private TMP_Text existingCounter;
    [SerializeField] private TMP_Text alwaysVisibleCounter;
    [SerializeField] private Transform[] trackedFoodTransforms;
    private Vector3[] initialWorldPositions;
    private Transform[] initialParents;
    private bool wasRunning;

    private void Awake()
    {
        initialWorldPositions = new Vector3[trackedFoodTransforms.Length];
        initialParents = new Transform[trackedFoodTransforms.Length];
        for (int i = 0; i < trackedFoodTransforms.Length; i++)
        {
            Transform t = trackedFoodTransforms[i];
            if (t == null) continue;
            initialWorldPositions[i] = t.position;
            initialParents[i] = t.parent;
        }
        if (alwaysVisibleCounter != null) alwaysVisibleCounter.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (alwaysVisibleCounter == null || environment == null) return;
        bool running = environment.activeInHierarchy && (introPanel == null || !introPanel.activeInHierarchy);
        if (running != wasRunning)
        {
            wasRunning = running;
            alwaysVisibleCounter.gameObject.SetActive(running);
            if (running) alwaysVisibleCounter.text = "Selected: 0 / 4";
        }
        if (!running) return;

        int moved = 0;
        for (int i = 0; i < trackedFoodTransforms.Length; i++)
        {
            Transform t = trackedFoodTransforms[i];
            if (t == null) continue;
            if (t.parent != initialParents[i] || (t.position - initialWorldPositions[i]).sqrMagnitude > 0.0025f)
                moved++;
        }
        moved = Mathf.Clamp(moved, 0, 4);
        // Original panel count may only update after four selections; display the physical plate occupancy.
        // Does not alter 4-food selection constraint or food events.
        alwaysVisibleCounter.text = "Selected: " + moved + " / 4";
    }
}
