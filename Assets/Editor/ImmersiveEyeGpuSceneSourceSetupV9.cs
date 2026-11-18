#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ImmersiveEyeGpuSceneSourceSetupV9
{
    [MenuItem("Tools/ImmersiveEye/Use Actual Scene Frames (GPU v9)")]
    public static void Configure()
    {
        if (PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Length != 1 ||
            PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)[0] != GraphicsDeviceType.OpenGLES3)
        {
            Debug.LogError("IE_GPU_SURFACE|V9_ABORT|Project must remain OpenGLES3. No changes made.");
            return;
        }
        var video = Object.FindFirstObjectByType<ImmersiveEyeVideoRecorder>(FindObjectsInactive.Include);
        var backend = Object.FindFirstObjectByType<ImmersiveEyeAndroidMp4Backend>(FindObjectsInactive.Include);
        var link = Object.FindFirstObjectByType<ImmersiveEyeAutoVideoFromGaze>(FindObjectsInactive.Include);
        if (video == null || backend == null || link == null || video.VideoCaptureCamera == null)
        {
            Debug.LogError("IE_GPU_SURFACE|V9_ABORT|Missing existing video recorder, backend, gaze link, or observation camera.");
            return;
        }
        var camera = video.VideoCaptureCamera;
        var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
        {
            Debug.LogError("IE_GPU_SURFACE|V9_ABORT|VideoCaptureCamera requires URP camera data.");
            return;
        }
        Undo.RecordObjects(new Object[]{video, backend, link, camera}, "Configure ImmersiveEye GPU Scene Source v9");
        var videoSO = new SerializedObject(video);
        var frameRate = videoSO.FindProperty("frameRate");
        if (frameRate != null) frameRate.intValue = 8;
        videoSO.ApplyModifiedProperties();
        var backendSO = new SerializedObject(backend);
        backendSO.FindProperty("outputWidth").intValue = 512;
        backendSO.FindProperty("outputHeight").intValue = 288;
        backendSO.FindProperty("maximumFps").intValue = 8;
        backendSO.ApplyModifiedProperties();
        camera.enabled = false;
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        backend.enabled = true;
        link.enabled = true;
        EditorUtility.SetDirty(video);
        EditorUtility.SetDirty(backend);
        EditorUtility.SetDirty(link);
        EditorUtility.SetDirty(camera);
        EditorSceneManager.MarkSceneDirty(video.gameObject.scene);
        Debug.Log("IE_GPU_SURFACE|V9_CONFIGURED|scene source=VideoCaptureCamera|camera remains disabled|512x288|8fps|CSV unchanged. Cmd+S then Build and Run.");
    }
}
#endif
