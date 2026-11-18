using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class BlockCTrialController : MonoBehaviour
{
    [Header("References")]
    public GameObject fixationCross;
    public GameObject blockCStimulus;

    [Header("Timing")]
    public float fixationDuration = 1.5f;
    public float stimulusDuration = 20f;
    public bool automaticallyEndStimulus = true;

    [Header("Events")]
    public UnityEvent onBlockCStarted;
    public UnityEvent onFixationStarted;
    public UnityEvent onStimulusStarted;
    public UnityEvent onBlockCCompleted;

    [Header("Status")]
    [SerializeField] private string currentPhase = "Idle";
    [SerializeField] private bool blockRunning = false;

    private Coroutine blockCoroutine;

    private void Start()
    {
        if (fixationCross != null)
            fixationCross.SetActive(false);

        if (blockCStimulus != null)
            blockCStimulus.SetActive(false);
    }

    public void StartBlockC()
    {
        if (blockRunning)
        {
            Debug.LogWarning("Block C is already running.");
            return;
        }

        if (fixationCross == null)
        {
            Debug.LogError("Block C Fixation Cross is not assigned.");
            return;
        }

        if (blockCStimulus == null)
        {
            Debug.LogError("Block C Stimulus is not assigned.");
            return;
        }

        blockCoroutine = StartCoroutine(RunBlockC());
    }

    private IEnumerator RunBlockC()
    {
        blockRunning = true;
        currentPhase = "Starting";

        onBlockCStarted?.Invoke();

        blockCStimulus.SetActive(false);

        fixationCross.SetActive(true);
        currentPhase = "Fixation";
        onFixationStarted?.Invoke();

        yield return new WaitForSeconds(fixationDuration);

        fixationCross.SetActive(false);

        blockCStimulus.SetActive(true);
        currentPhase = "Free Viewing";
        onStimulusStarted?.Invoke();

        if (automaticallyEndStimulus)
        {
            yield return new WaitForSeconds(stimulusDuration);
            CompleteBlockC();
        }
    }

    public void CompleteBlockC()
    {
        if (!blockRunning)
            return;

        if (blockCStimulus != null)
            blockCStimulus.SetActive(false);

        if (fixationCross != null)
            fixationCross.SetActive(false);

        blockRunning = false;
        currentPhase = "Completed";

        onBlockCCompleted?.Invoke();

        blockCoroutine = null;
    }
}