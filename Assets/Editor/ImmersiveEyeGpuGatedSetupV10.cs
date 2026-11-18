#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ImmersiveEyeGpuGatedSetupV10
{
    [MenuItem("Tools/ImmersiveEye/Enable Gated GPU Camera MP4 v10")]
    public static void Enable()
    {
        if (PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Length != 1 ||
            PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)[0] != GraphicsDeviceType.OpenGLES3)
        { Debug.LogError("IE_GPU_SURFACE|V10_ABORT|Android must use OpenGLES3"); return; }
        var video = Object.FindFirstObjectByType<ImmersiveEyeVideoRecorder>(FindObjectsInactive.Include);
        var backend = Object.FindFirstObjectByType<ImmersiveEyeAndroidMp4Backend>(FindObjectsInactive.Include);
        var link = Object.FindFirstObjectByType<ImmersiveEyeAutoVideoFromGaze>(FindObjectsInactive.Include);
        if (video == null || backend == null || link == null || video.VideoCaptureCamera == null)
        { Debug.LogError("IE_GPU_SURFACE|V10_ABORT|Video recorder/backend/gaze link/camera missing"); return; }
        Undo.RecordObjects(new Object[] {video, backend, link, video.VideoCaptureCamera}, "Configure GPU Camera v10");
        var v = new SerializedObject(video);
        v.FindProperty("frameRate").intValue = 8;
        v.ApplyModifiedProperties();
        var b = new SerializedObject(backend);
        b.FindProperty("outputWidth").intValue = 512;
        b.FindProperty("outputHeight").intValue = 288;
        b.FindProperty("maximumFps").intValue = 8;
        b.ApplyModifiedProperties();
        var cam = video.VideoCaptureCamera;
        cam.enabled = false;
        cam.stereoTargetEye = StereoTargetEyeMask.None;
        backend.enabled = true;
        link.enabled = true;
        EditorUtility.SetDirty(video);
        EditorUtility.SetDirty(backend);
        EditorUtility.SetDirty(link);
        EditorUtility.SetDirty(cam);
        EditorSceneManager.MarkSceneDirty(video.gameObject.scene);
        Debug.Log("IE_GPU_SURFACE|V10_CONFIGURED|gated camera 512x288 8fps, CSV and Blocks A-D unchanged; Save then Build and Run");
    }
}
#endif
