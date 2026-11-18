using UnityEngine;

/// <summary>
/// Direct video recording follows the SAME recording state and block name used
/// by the project's existing GazeRecorder. No changes to Blocks A-D, UI, CSV,
/// Android MediaProjection, or the existing MP4 backend.
/// </summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class ImmersiveEyeAutoVideoFromGaze : MonoBehaviour
{
    [Header("Assign automatically using Tools > ImmersiveEye > Link Video to Gaze Recording")]
    [SerializeField] private GazeRecorder gazeRecorder;
    [SerializeField] private ImmersiveEyeVideoRecorder videoRecorder;

    private string attemptedBlock = "";
    private bool attemptedForThisGazeRun;
    private bool wasGazeRecording;

    private void Awake()
    {
        if (videoRecorder == null) videoRecorder = GetComponent<ImmersiveEyeVideoRecorder>();
        if (gazeRecorder == null)
            gazeRecorder = FindFirstObjectByType<GazeRecorder>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (videoRecorder == null || gazeRecorder == null) return;
        bool gazeActive = gazeRecorder.isActiveAndEnabled && gazeRecorder.Recording;
        string block = gazeRecorder.BlockName;
        if (!string.IsNullOrEmpty(block)) block = block.Trim();

        if (!gazeActive)
        {
            if (videoRecorder.IsRecording)
            {
                Debug.Log("IE_DIRECT_MP4|AUTO_GAZE_STOP|" + videoRecorder.CurrentBlock);
                videoRecorder.StopVideo();
            }
            if (wasGazeRecording)
            {
                attemptedForThisGazeRun = false;
                attemptedBlock = "";
            }
            wasGazeRecording = false;
            return;
        }

        if (videoRecorder.IsRecording)
        {
            if (videoRecorder.CurrentBlock == block)
            {
                wasGazeRecording = true;
                return;
            }
            Debug.Log("IE_DIRECT_MP4|AUTO_BLOCK_CHANGED|" + videoRecorder.CurrentBlock + "|" + block);
            videoRecorder.StopVideo();
        }

        // Do not retry a failed capture every frame. That would harm VR performance.
        if (attemptedForThisGazeRun && attemptedBlock == block)
        {
            wasGazeRecording = true;
            return;
        }
        attemptedForThisGazeRun = true;
        attemptedBlock = block;
        wasGazeRecording = true;
        Debug.Log("IE_DIRECT_MP4|AUTO_GAZE_START|" + block);
        switch (block)
        {
            case "BlockA": videoRecorder.StartBlockAVideo(); break;
            case "BlockB": videoRecorder.StartBlockBVideo(); break;
            case "BlockC": videoRecorder.StartBlockCVideo(); break;
            case "BlockD": videoRecorder.StartBlockDVideo(); break;
            default:
                Debug.LogWarning("IE_DIRECT_MP4|UNKNOWN_BLOCK|" + block);
                break;
        }
        Debug.Log("IE_DIRECT_MP4|AUTO_START_RESULT|" + block + "|" + videoRecorder.Status);
    }

    private void OnDisable()
    {
        if (videoRecorder != null && videoRecorder.IsRecording)
            videoRecorder.StopVideo();
    }
}
