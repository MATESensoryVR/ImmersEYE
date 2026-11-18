
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class BlockBTrialController : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]
    [SerializeField] private GameObject fixationCross;
    [SerializeField] private GameObject blockBStimulus;


    // ============================================================
    // TIMING
    // ============================================================

    [Header("Timing")]
    [Min(0.1f)]
    [SerializeField] private float fixationDuration = 1.5f;

    [Min(0.1f)]
    [SerializeField] private float stimulusDuration = 10.0f;

    [SerializeField] private bool automaticallyEndStimulus = true;


    // ============================================================
    // EVENTS
    // ============================================================

    [Header("Events")]
    [SerializeField] private UnityEvent onBlockBStarted;
    [SerializeField] private UnityEvent onFixationStarted;
    [SerializeField] private UnityEvent onStimulusStarted;
    [SerializeField] private UnityEvent onBlockBCompleted;


    // ============================================================
    // RUNTIME STATUS
    // ============================================================

    [Header("Runtime Status")]
    [SerializeField] private bool blockBRunning = false;
    [SerializeField] private string currentPhase = "IDLE";
    [SerializeField] private float phaseElapsedTime = 0.0f;

    private Coroutine blockCoroutine;


    // ============================================================
    // PUBLIC ACCESS
    // ============================================================

    public bool BlockBRunning => blockBRunning;
    public string CurrentPhase => currentPhase;
    public float PhaseElapsedTime => phaseElapsedTime;


    // ============================================================
    // INITIALIZATION
    // ============================================================

    private void Start()
    {
        // Reset the runtime state at experiment startup.
        // Prevent a previously serialized running state
        // from blocking StartBlockB().

        blockBRunning = false;
        currentPhase = "IDLE";
        phaseElapsedTime = 0.0f;
        blockCoroutine = null;

        if (fixationCross != null)
        {
            fixationCross.SetActive(false);
        }

        if (blockBStimulus != null)
        {
            blockBStimulus.SetActive(false);
        }

        Debug.Log("BLOCK_B_CONTROLLER_INITIALIZED");
    }


    // ============================================================
    // START BLOCK B
    // ============================================================

    public void StartBlockB()
    {
        if (blockBRunning)
        {
            Debug.LogWarning(
                "BlockBTrialController: Block B is already running."
            );

            return;
        }

        if (fixationCross == null)
        {
            Debug.LogWarning(
                "BlockBTrialController: Fixation Cross is not assigned."
            );

            return;
        }

        if (blockBStimulus == null)
        {
            Debug.LogWarning(
                "BlockBTrialController: Block B Stimulus is not assigned."
            );

            return;
        }

        Debug.Log("BLOCK_B_START_REQUESTED");

        blockCoroutine = StartCoroutine(RunBlockB());
    }


    // ============================================================
    // BLOCK B SEQUENCE
    // ============================================================

    private IEnumerator RunBlockB()
    {
        blockBRunning = true;
        currentPhase = "BLOCK_START";
        phaseElapsedTime = 0.0f;

        fixationCross.SetActive(false);
        blockBStimulus.SetActive(false);

        Debug.Log("BLOCK_B_START");

        onBlockBStarted?.Invoke();


        // ========================================================
        // PHASE 1: FIXATION (1.5 SECONDS)
        // ========================================================

        currentPhase = "FIXATION";
        phaseElapsedTime = 0.0f;

        fixationCross.SetActive(true);

        Debug.Log("BLOCK_B_FIXATION_ONSET");

        onFixationStarted?.Invoke();

        while (phaseElapsedTime < fixationDuration)
        {
            phaseElapsedTime += Time.deltaTime;
            yield return null;
        }

        fixationCross.SetActive(false);

        Debug.Log("BLOCK_B_FIXATION_OFFSET");


        // ========================================================
        // PHASE 2: FOOD STIMULUS (10 SECONDS)
        // ========================================================

        currentPhase = "STIMULUS";
        phaseElapsedTime = 0.0f;

        blockBStimulus.SetActive(true);

        Debug.Log("BLOCK_B_STIMULUS_ONSET");

        onStimulusStarted?.Invoke();

        if (automaticallyEndStimulus)
        {
            while (phaseElapsedTime < stimulusDuration)
            {
                phaseElapsedTime += Time.deltaTime;
                yield return null;
            }

            CompleteBlockB();
        }
    }


    // ============================================================
    // COMPLETE BLOCK B
    // ============================================================

    public void CompleteBlockB()
    {
        if (!blockBRunning)
        {
            return;
        }

        fixationCross.SetActive(false);
        blockBStimulus.SetActive(false);

        currentPhase = "COMPLETE";
        phaseElapsedTime = 0.0f;

        blockBRunning = false;
        blockCoroutine = null;

        Debug.Log("BLOCK_B_STIMULUS_OFFSET");
        Debug.Log("BLOCK_B_END");

        // Existing Unity Inspector events:
        // 1. Stop gaze recording
        // 2. Save gaze CSV
        // 3. Show Block C introduction

        onBlockBCompleted?.Invoke();
    }
}
