using UnityEngine;
using TMPro;

/// <summary>
/// Display-only Block D panel visibility and count.
/// Keeps the ENTIRE existing BlockDConfirmPanel visible from the start of Block D.
/// Does not modify food selection, plate slots, AOIs, gaze, MP4, or event CSV logic.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(30000)]
public sealed class ImmersiveEyeBlockDFullPanelV173 : MonoBehaviour
{
    [SerializeField] private GameObject blockDEnvironment;
    [SerializeField] private GameObject blockDIntroPanel;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private GameObject blockDConfirmPanel;
    [SerializeField] private TMP_Text selectionCountText;
    [SerializeField] private Transform[] foods;
    [SerializeField, Min(0.005f)] private float movementThresholdMeters = 0.05f;

    private Transform[] startingParents;
    private Vector3[] startingPositions;
    private bool previouslyRunning;

    private void Awake()
    {
        if (selectionCountText != null)
            selectionCountText.text = "Selected: 0 / 4";
    }

    private void LateUpdate()
    {
        if (blockDEnvironment == null || blockDConfirmPanel == null)
            return;

        bool running = blockDEnvironment.activeInHierarchy
            && (blockDIntroPanel == null || !blockDIntroPanel.activeInHierarchy)
            && (endPanel == null || !endPanel.activeInHierarchy);

        if (!running)
        {
            // Keep the entire panel hidden outside the active Block D stage.
            if (blockDConfirmPanel.activeSelf)
                blockDConfirmPanel.SetActive(false);
            previouslyRunning = false;
            return;
        }

        if (!previouslyRunning)
        {
            RecordHomePositions();
            previouslyRunning = true;
            if (selectionCountText != null)
                selectionCountText.text = "Selected: 0 / 4";
        }

        // This is the whole panel: heading, count, instruction and confirm button.
        // The original Block D controller may hide it until the fourth selection.
        // Make it visible again at the end of each Unity frame.
        if (!blockDConfirmPanel.activeSelf)
            blockDConfirmPanel.SetActive(true);

        if (selectionCountText == null) return;
        if (!selectionCountText.gameObject.activeSelf)
            selectionCountText.gameObject.SetActive(true);

        // Display estimate only. The existing plate manager remains authoritative.
        int selected = 0;
        float threshold2 = movementThresholdMeters * movementThresholdMeters;
        if (foods != null && startingPositions != null)
        {
            for (int i = 0; i < foods.Length; i++)
            {
                Transform food = foods[i];
                if (food == null) continue;
                if (food.parent != startingParents[i] ||
                    (food.position - startingPositions[i]).sqrMagnitude > threshold2)
                    selected++;
            }
        }
        selectionCountText.text = "Selected: " + Mathf.Clamp(selected, 0, 4) + " / 4";
    }

    private void RecordHomePositions()
    {
        int count = foods == null ? 0 : foods.Length;
        startingParents = new Transform[count];
        startingPositions = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            if (foods[i] == null) continue;
            startingParents[i] = foods[i].parent;
            startingPositions[i] = foods[i].position;
        }
    }
}
