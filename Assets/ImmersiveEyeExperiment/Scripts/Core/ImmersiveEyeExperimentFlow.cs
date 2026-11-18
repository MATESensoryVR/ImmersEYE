using UnityEngine;

/// <summary>
/// Coordinates introduction panels and existing Block A-D controllers.
/// The calibration system should call ShowBlockAIntro() only after validation.
/// Connect completion UnityEvents in the Inspector (see Setup_Guide.md).
/// </summary>
public class ImmersiveEyeExperimentFlow : MonoBehaviour
{
    public enum ExperimentStage
    {
        WaitingForCalibration,
        IntroA, RunningA,
        IntroB, RunningB,
        IntroC, RunningC,
        IntroD, RunningD,
        Completed
    }

    [Header("Introduction / Completion Panels")]
    [SerializeField] private GameObject blockAIntroPanel;
    [SerializeField] private GameObject blockBIntroPanel;
    [SerializeField] private GameObject blockCIntroPanel;
    [SerializeField] private GameObject blockDIntroPanel;
    [SerializeField] private GameObject endPanel;

    [Header("Existing Block Controllers")]
    [SerializeField] private BlockAController blockAController;
    [SerializeField] private BlockBTrialController blockBController;
    [SerializeField] private BlockCTrialController blockCController;
    [SerializeField] private BlockDTrialController blockDController;

    [Header("Runtime Status (Read Only)")]
    [SerializeField] private ExperimentStage currentStage = ExperimentStage.WaitingForCalibration;

    public ExperimentStage CurrentStage => currentStage;

    private void Awake()
    {
        // Leave the pre-existing registration/calibration UI to its own controller.
        HideFlowPanels();
        currentStage = ExperimentStage.WaitingForCalibration;
    }

    private void HideFlowPanels()
    {
        SetActiveIfAssigned(blockAIntroPanel, false);
        SetActiveIfAssigned(blockBIntroPanel, false);
        SetActiveIfAssigned(blockCIntroPanel, false);
        SetActiveIfAssigned(blockDIntroPanel, false);
        SetActiveIfAssigned(endPanel, false);
    }

    private static void SetActiveIfAssigned(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    private void ShowPanel(GameObject panel, ExperimentStage stage)
    {
        if (panel == null)
        {
            Debug.LogError("ImmersiveEyeExperimentFlow: Missing panel for " + stage);
            return;
        }

        HideFlowPanels();
        panel.SetActive(true);
        currentStage = stage;
        Debug.Log("EXPERIMENT_STAGE|" + currentStage);
    }

    // Call from the existing calibration/validation success (or approved override) flow.
    public void ShowBlockAIntro() => ShowPanel(blockAIntroPanel, ExperimentStage.IntroA);
    public void ShowBlockBIntro() => ShowPanel(blockBIntroPanel, ExperimentStage.IntroB);
    public void ShowBlockCIntro() => ShowPanel(blockCIntroPanel, ExperimentStage.IntroC);
    public void ShowBlockDIntro() => ShowPanel(blockDIntroPanel, ExperimentStage.IntroD);
    public void ShowEndPanel() => ShowPanel(endPanel, ExperimentStage.Completed);

    public void StartBlockA()
    {
        if (blockAController == null) { Debug.LogError("Missing Block A Controller."); return; }
        HideFlowPanels();
        currentStage = ExperimentStage.RunningA;
        blockAController.StartBlockA();
    }

    public void StartBlockB()
    {
        if (blockBController == null) { Debug.LogError("Missing Block B Controller."); return; }
        HideFlowPanels();
        currentStage = ExperimentStage.RunningB;
        blockBController.StartBlockB();
    }

    public void StartBlockC()
    {
        if (blockCController == null) { Debug.LogError("Missing Block C Controller."); return; }
        HideFlowPanels();
        currentStage = ExperimentStage.RunningC;
        blockCController.StartBlockC();
    }

    public void StartBlockD()
    {
        if (blockDController == null) { Debug.LogError("Missing Block D Controller."); return; }
        HideFlowPanels();
        currentStage = ExperimentStage.RunningD;
        blockDController.StartBlockD();
    }
}
