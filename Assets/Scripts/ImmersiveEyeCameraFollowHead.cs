using UnityEngine;

/// <summary>
/// Moves only the dedicated observation camera to the participant's XR head
/// pose while an MP4 is being recorded. Never modifies the VR main camera.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class ImmersiveEyeCameraFollowHead : MonoBehaviour
{
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private ImmersiveEyeVideoRecorder videoRecorder;
    [SerializeField, Range(1f, 1.2f)] private float recordingZoom = 1.08f;

    private void LateUpdate()
    {
        if (videoRecorder == null || !videoRecorder.IsRecording) return;
        if (sourceCamera == null) sourceCamera = Camera.main;
        if (sourceCamera == null || sourceCamera.transform == transform) return;
        transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);
        Camera target = GetComponent<Camera>();
        if (target != null)
            target.fieldOfView = 2f * Mathf.Atan(
                Mathf.Tan(sourceCamera.fieldOfView * Mathf.Deg2Rad * 0.5f)
                / recordingZoom) * Mathf.Rad2Deg;
    }
}
