using UnityEngine;
using TMPro;

/// <summary>
/// Display-only Block D counter. Draws inside the existing confirmation panel.
/// Never alters plate selection, gaze recording, food events or video recording.
/// Checks movement of the original food objects (same approach as v17).
/// </summary>
[DisallowMultipleComponent]
public sealed class ImmersiveEyeBlockDPanelCounterV171 : MonoBehaviour
{
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private TMP_Text selectionCountText;
    [SerializeField] private Transform[] foods;
    [SerializeField] private float movementThresholdMeters = 0.05f;

    private Transform[] initialParents;
    private Vector3[] initialPositions;
    private bool previousPanelActive;

    private void Awake()
    {
        int length = foods == null ? 0 : foods.Length;
        initialParents = new Transform[length];
        initialPositions = new Vector3[length];
        for (int i = 0; i < length; i++)
        {
            if (foods[i] == null) continue;
            initialParents[i] = foods[i].parent;
            initialPositions[i] = foods[i].position;
        }
        if (selectionCountText != null)
        {
            selectionCountText.text = "Selected: 0 / 4";
            selectionCountText.gameObject.SetActive(true);
        }
    }

    private void LateUpdate()
    {
        if (confirmationPanel == null || selectionCountText == null || foods == null)
            return;

        // The panel's normal activation controls visibility. Do not create a new overlay.
        bool active = confirmationPanel.activeInHierarchy;
        if (!active)
        {
            previousPanelActive = false;
            return;
        }
        if (!previousPanelActive)
        {
            previousPanelActive = true;
            selectionCountText.gameObject.SetActive(true);
            selectionCountText.text = "Selected: 0 / 4";
        }

        float threshold2 = movementThresholdMeters * movementThresholdMeters;
        int count = 0;
        for (int i = 0; i < foods.Length; i++)
        {
            Transform food = foods[i];
            if (food == null) continue;
            if (food.parent != initialParents[i] ||
                (food.position - initialPositions[i]).sqrMagnitude > threshold2)
                count++;
        }
        selectionCountText.text = "Selected: " + Mathf.Clamp(count, 0, 4) + " / 4";
    }
}
