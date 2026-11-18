using TMPro;
using UnityEngine;

/// <summary>
/// Display-only. Keeps the original SelectionCountText on the MAIN UI canvas,
/// independent of BlockDConfirmPanel activation. Does not change selection,
/// physics, gaze, AOI, events, or recording.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(3000)]
public sealed class ImmersiveEyeBlockDAlwaysVisibleCounterV172 : MonoBehaviour
{
    [SerializeField] private GameObject blockDEnvironment;
    [SerializeField] private GameObject blockDIntroPanel;
    [SerializeField] private TMP_Text selectionCountText;
    [SerializeField] private Transform[] foods;
    [SerializeField] private float movementThresholdMeters = 0.05f;

    private Vector3[] homePositions;
    private Transform[] homeParents;
    private bool wasRunning;

    private void Awake()
    {
        CaptureHome();
        if (selectionCountText != null)
        {
            selectionCountText.text = "Selected: 0 / 4";
            selectionCountText.gameObject.SetActive(false);
        }
    }

    private void CaptureHome()
    {
        int count = foods == null ? 0 : foods.Length;
        homePositions = new Vector3[count];
        homeParents = new Transform[count];
        for (int i = 0; i < count; i++)
        {
            if (foods[i] == null) continue;
            homePositions[i] = foods[i].position;
            homeParents[i] = foods[i].parent;
        }
    }

    private void LateUpdate()
    {
        if (selectionCountText == null || blockDEnvironment == null) return;
        bool running = blockDEnvironment.activeInHierarchy &&
            (blockDIntroPanel == null || !blockDIntroPanel.activeInHierarchy);
        // Do not rely on confirmation panel; it may activate only at four foods.
        if (!running)
        {
            if (selectionCountText.gameObject.activeSelf)
                selectionCountText.gameObject.SetActive(false);
            wasRunning = false;
            return;
        }

        if (!wasRunning)
        {
            CaptureHome();
            wasRunning = true;
        }

        if (!selectionCountText.gameObject.activeSelf)
            selectionCountText.gameObject.SetActive(true);

        // Display estimate based on original food transforms, not fake gaze data.
        int selected = 0;
        float sqrThreshold = movementThresholdMeters * movementThresholdMeters;
        for (int i = 0; i < homePositions.Length; i++)
        {
            Transform food = foods[i];
            if (food == null) continue;
            if (food.parent != homeParents[i] ||
                (food.position - homePositions[i]).sqrMagnitude > sqrThreshold)
                selected++;
        }
        selectionCountText.text = "Selected: " + Mathf.Clamp(selected, 0, 4) + " / 4";
    }
}
