#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ImmersiveEyeGpuSurfaceSetupV8
{
    [MenuItem("Tools/ImmersiveEye/Enable GPU Surface MP4 v8 (Automatic A-D)")]
    public static void Configure()
    {
        var devices = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        bool gles3 = devices != null && devices.Length == 1 && devices[0] == GraphicsDeviceType.OpenGLES3;
        if (!gles3)
        {
            Debug.LogError("IE_GPU_SURFACE|SETUP_ABORT|Android must use OpenGLES3 ONLY. No graphics setting was modified.");
            return;
        }
        var links = Object.FindObjectsByType<ImmersiveEyeAutoVideoFromGaze>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var videos = Object.FindObjectsByType<ImmersiveEyeVideoRecorder>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var backs = Object.FindObjectsByType<ImmersiveEyeAndroidMp4Backend>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (links.Length != 1 || videos.Length != 1 || backs.Length != 1)
        {
            Debug.LogError("IE_GPU_SURFACE|SETUP_ABORT|Expected one auto-gaze link, video recorder, and MP4 backend. " +
                "Found " + links.Length + "/" + videos.Length + "/" + backs.Length + ". No scene changes.");
            return;
        }
        if (links[0].gameObject.scene != videos[0].gameObject.scene ||
            videos[0].gameObject.scene != backs[0].gameObject.scene)
        {
            Debug.LogError("IE_GPU_SURFACE|SETUP_ABORT|Components are in different scenes.");
            return;
        }
        var video = videos[0];
        var link = links[0];
        var back = backs[0];
        var videoSo = new SerializedObject(video);
        var fps = videoSo.FindProperty("frameRate");
        if (fps != null) fps.intValue = 15;
        videoSo.ApplyModifiedProperties();
        Undo.RecordObject(back, "Enable GLES hardware MP4 backend");
        Undo.RecordObject(link, "Auto-record GPU MP4 with eye-tracking block");
        back.enabled = true;
        link.enabled = true;
        var camera = video.VideoCaptureCamera;
        if (camera != null && camera != Camera.main)
        {
            Undo.RecordObject(camera, "Disable duplicate VR recording camera");
            camera.enabled = false; // Permanent: v8 captures final XR screen output.
            EditorUtility.SetDirty(camera);
        }
        EditorUtility.SetDirty(video);
        EditorUtility.SetDirty(back);
        EditorUtility.SetDirty(link);
        EditorSceneManager.MarkSceneDirty(video.gameObject.scene);
        Debug.Log("IE_GPU_SURFACE|V8_CONFIGURED|autoVideo=ON|fps=15|secondCamera=OFF|CSV unchanged. Cmd+S then Build and Run.");
    }

    [MenuItem("Tools/ImmersiveEye/Disable Automatic MP4 (Safe CSV Only)")]
    public static void DisableMp4()
    {
        var links = Object.FindObjectsByType<ImmersiveEyeAutoVideoFromGaze>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var link in links)
        {
            Undo.RecordObject(link, "Disable MP4 and preserve CSV");
            link.enabled = false;
            EditorUtility.SetDirty(link);
            if (link.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(link.gameObject.scene);
        }
        Debug.Log("IE_GPU_SURFACE|MP4_DISABLED|CSV eye tracking stays active. Cmd+S then Build and Run.");
    }
}
#endif
