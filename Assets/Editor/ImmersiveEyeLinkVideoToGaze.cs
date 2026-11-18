#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ImmersiveEyeLinkVideoToGaze
{
    [MenuItem("Tools/ImmersiveEye/Link Video to Gaze Recording (Automatic Blocks A-D)")]
    public static void Configure()
    {
        var videoRecorders = Object.FindObjectsByType<ImmersiveEyeVideoRecorder>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var gazeRecorders = Object.FindObjectsByType<GazeRecorder>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (gazeRecorders.Length != 1 || videoRecorders.Length != 1)
        {
            Debug.LogError("IE_DIRECT_MP4|AUTO_LINK_ABORT: expected one GazeRecorder and " +
                "one ImmersiveEyeVideoRecorder; found " + gazeRecorders.Length +
                " and " + videoRecorders.Length + ". Scene unchanged.");
            return;
        }
        var video = videoRecorders[0];
        var gaze = gazeRecorders[0];
        if (!video.gameObject.scene.IsValid() || !gaze.gameObject.scene.IsValid())
        {
            Debug.LogError("IE_DIRECT_MP4|AUTO_LINK_ABORT: Open the ImmersiveEye experiment scene first.");
            return;
        }
        var serializedVideo = new SerializedObject(video);
        var cameraProperty = serializedVideo.FindProperty("videoCaptureCamera");
        var sourceProperty = serializedVideo.FindProperty("videoRenderTexture");
        var backendProperty = serializedVideo.FindProperty("encoderBackend");
        var sessionProperty = serializedVideo.FindProperty("sessionRecorder");
        if (cameraProperty == null || sourceProperty == null || backendProperty == null || sessionProperty == null ||
            cameraProperty.objectReferenceValue == null || sourceProperty.objectReferenceValue == null ||
            backendProperty.objectReferenceValue == null || sessionProperty.objectReferenceValue == null)
        {
            Debug.LogError("IE_DIRECT_MP4|AUTO_LINK_ABORT: video recorder is missing Camera, RenderTexture, " +
                "encoderBackend, or sessionRecorder. Fix these Inspector references first. Scene unchanged.");
            return;
        }
        // v5 assigned the backend reference but did not enable a previously
        // disabled Android backend; without this LateUpdate cannot submit frames.
        var backend = backendProperty.objectReferenceValue as MonoBehaviour;
        if (backend == null)
        {
            Debug.LogError("IE_DIRECT_MP4|AUTO_LINK_ABORT: Backend reference is not a MonoBehaviour.");
            return;
        }
        if (!video.gameObject.activeInHierarchy)
        {
            if (!video.gameObject.activeSelf)
            {
                Undo.RecordObject(video.gameObject, "Activate existing video recorder");
                video.gameObject.SetActive(true);
            }
            if (!video.gameObject.activeInHierarchy)
            {
                Debug.LogError("IE_DIRECT_MP4|AUTO_LINK_ABORT: Recorder parent GameObject inactive: " + video.name);
                return;
            }
        }
        if (!backend.enabled)
        {
            Undo.RecordObject(backend, "Enable direct MP4 backend");
            backend.enabled = true;
            EditorUtility.SetDirty(backend);
        }
        var bridge = video.GetComponent<ImmersiveEyeAutoVideoFromGaze>();
        if (bridge == null) bridge = Undo.AddComponent<ImmersiveEyeAutoVideoFromGaze>(video.gameObject);
        var serializedBridge = new SerializedObject(bridge);
        serializedBridge.FindProperty("gazeRecorder").objectReferenceValue = gaze;
        serializedBridge.FindProperty("videoRecorder").objectReferenceValue = video;
        serializedBridge.ApplyModifiedProperties();
        bridge.enabled = true;
        video.enabled = true;
        EditorUtility.SetDirty(bridge);
        EditorUtility.SetDirty(video);

        var captureCamera = cameraProperty.objectReferenceValue as Camera;
        if (captureCamera != null && captureCamera != Camera.main)
        {
            if (!captureCamera.gameObject.activeSelf)
            {
                Undo.RecordObject(captureCamera.gameObject, "Activate dedicated recording camera GameObject");
                captureCamera.gameObject.SetActive(true);
            }
            if (!captureCamera.gameObject.activeInHierarchy)
            {
                Debug.LogError("IE_DIRECT_MP4|Recording camera parent is inactive; video will not render: " + captureCamera.name);
            }
            captureCamera.enabled = false; // camera only renders when video backend requests it
            var follow = captureCamera.GetComponent<ImmersiveEyeCameraFollowHead>();
            if (follow == null) follow = Undo.AddComponent<ImmersiveEyeCameraFollowHead>(captureCamera.gameObject);
            var serializedFollow = new SerializedObject(follow);
            serializedFollow.FindProperty("videoRecorder").objectReferenceValue = video;
            serializedFollow.ApplyModifiedProperties();
            follow.enabled = true;
            EditorUtility.SetDirty(follow);
        }
        EditorSceneManager.MarkSceneDirty(video.gameObject.scene);
        Debug.Log("IE_DIRECT_MP4|AUTO_GAZE_LINKED|" + video.name + "|" + gaze.name + "|backendEnabled=" + backend.enabled +
                  "| Blocks A-D will start/stop MP4 automatically with GazeRecorder. SAVE SCENE (Cmd+S).");
    }
}
#endif
