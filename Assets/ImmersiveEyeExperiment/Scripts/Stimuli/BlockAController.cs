using System.Collections;
using UnityEngine;
using UnityEngine.Events;


public class BlockAController : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]

    [SerializeField]
    private GameObject fixationCross;

    [SerializeField]
    private GameObject blockAStimulus;


    // ============================================================
    // TIMING
    // ============================================================

    [Header("Timing")]

    [Min(0.1f)]
    [SerializeField]
    private float fixationDuration = 1.5f;

    [Min(0.1f)]
    [SerializeField]
    private float stimulusDuration = 10.0f;

    [SerializeField]
    private bool automaticallyEndStimulus = true;


    // ============================================================
    // EVENTS
    // ============================================================

    [Header("Events")]

    [SerializeField]
    private UnityEvent onBlockAStarted;

    [SerializeField]
    private UnityEvent onFixationStarted;

    [SerializeField]
    private UnityEvent onStimulusStarted;

    [SerializeField]
    private UnityEvent onBlockACompleted;


    // ============================================================
    // RUNTIME STATUS
    // ============================================================

    [Header("Runtime Status")]

    [SerializeField]
    private bool blockARunning = false;

    [SerializeField]
    private string currentPhase = "IDLE";

    [SerializeField]
    private float phaseElapsedTime = 0.0f;


    private Coroutine blockCoroutine;


    // ============================================================
    // PUBLIC ACCESS
    // ============================================================

    public bool BlockARunning =>
        blockARunning;

    public string CurrentPhase =>
        currentPhase;

    public float PhaseElapsedTime =>
        phaseElapsedTime;


    // ============================================================
    // UNITY
    // ============================================================

    private void Start()
    {
        if (fixationCross != null)
        {
            fixationCross.SetActive(false);
        }

        if (blockAStimulus != null)
        {
            blockAStimulus.SetActive(false);
        }
    }


    // ============================================================
    // START BLOCK A
    // ============================================================

    public void StartBlockA()
    {
        if (blockARunning)
        {
            return;
        }

        if (fixationCross == null)
        {
            Debug.LogWarning(
                "BlockAController: Fixation Cross is not assigned."
            );

            return;
        }

        if (blockAStimulus == null)
        {
            Debug.LogWarning(
                "BlockAController: Block A Stimulus is not assigned."
            );

            return;
        }

        blockCoroutine =
            StartCoroutine(
                RunBlockA()
            );
    }


    // ============================================================
    // BLOCK SEQUENCE
    // ============================================================

    private IEnumerator RunBlockA()
    {
        blockARunning = true;

        currentPhase = "BLOCK_START";

        phaseElapsedTime = 0.0f;


        fixationCross.SetActive(false);

        blockAStimulus.SetActive(false);


        Debug.Log(
            "BLOCK_A_START"
        );

        onBlockAStarted?.Invoke();


        // ========================================================
        // FIXATION
        // ========================================================

        currentPhase = "FIXATION";

        phaseElapsedTime = 0.0f;


        fixationCross.SetActive(true);


        Debug.Log(
            "BLOCK_A_FIXATION_ONSET"
        );

        onFixationStarted?.Invoke();


        while (
            phaseElapsedTime
            <
            fixationDuration
        )
        {
            phaseElapsedTime +=
                Time.deltaTime;

            yield return null;
        }


        fixationCross.SetActive(false);


        Debug.Log(
            "BLOCK_A_FIXATION_OFFSET"
        );


        // ========================================================
        // STIMULUS
        // ========================================================

        currentPhase = "STIMULUS";

        phaseElapsedTime = 0.0f;


        blockAStimulus.SetActive(true);


        Debug.Log(
            "BLOCK_A_STIMULUS_ONSET"
        );

        onStimulusStarted?.Invoke();


        if (automaticallyEndStimulus)
        {
            while (
                phaseElapsedTime
                <
                stimulusDuration
            )
            {
                phaseElapsedTime +=
                    Time.deltaTime;

                yield return null;
            }


            CompleteBlockA();
        }
    }


    // ============================================================
    // COMPLETE BLOCK A
    // ============================================================

    public void CompleteBlockA()
    {
        if (!blockARunning)
        {
            return;
        }


        fixationCross.SetActive(false);

        blockAStimulus.SetActive(false);


        currentPhase = "COMPLETE";

        blockARunning = false;

        blockCoroutine = null;


        Debug.Log(
            "BLOCK_A_STIMULUS_OFFSET"
        );

        Debug.Log(
            "BLOCK_A_END"
        );


        onBlockACompleted?.Invoke();
    }


    // ============================================================
    // STOP
    // ============================================================

    public void StopBlockA()
    {
        if (blockCoroutine != null)
        {
            StopCoroutine(
                blockCoroutine
            );

            blockCoroutine = null;
        }


        if (fixationCross != null)
        {
            fixationCross.SetActive(false);
        }


        if (blockAStimulus != null)
        {
            blockAStimulus.SetActive(false);
        }


        blockARunning = false;

        currentPhase = "STOPPED";

        phaseElapsedTime = 0.0f;


        Debug.Log(
            "BLOCK_A_STOPPED"
        );
    }
}