#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Use once with the ImmersiveEye scene open; preserves experiment event wiring.</summary>
public static class ImmersiveEyeConfigureDirectRecording
{
    [MenuItem("Tools/ImmersiveEye/Configure Direct MP4 (No Permission Button)")]
    public static void Configure()
    {
        var all = Object.FindObjectsByType<ImmersiveEyeVideoRecorder>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all.Length == 0)
        {
            Debug.LogError("IE_DIRECT_MP4|No ImmersiveEyeVideoRecorder found. Open the experiment scene first.");
            return;
        }
        int count = 0;
        foreach (var r in all)
        {
            if (!r.gameObject.scene.IsValid()) continue;
            var so = new SerializedObject(r);
            var cameraProp = so.FindProperty("videoCaptureCamera");
            var backendProp = so.FindProperty("encoderBackend");
            if (cameraProp == null || backendProp == null)
            {
                Debug.LogError("IE_DIRECT_MP4|Recorder fields not found; not modifying " + r.name);
                continue;
            }
            var captureCamera = cameraProp.objectReferenceValue as Camera;
            if (captureCamera == null)
            {
                Debug.LogError("IE_DIRECT_MP4|Missing VideoCaptureCamera on " + r.name);
                continue;
            }
            var backend = r.GetComponent<ImmersiveEyeAndroidMp4Backend>();
            if (backend == null) backend = Undo.AddComponent<ImmersiveEyeAndroidMp4Backend>(r.gameObject);
            var backendObject = new SerializedObject(backend);
            backendObject.FindProperty("videoCaptureCamera").objectReferenceValue = captureCamera;
            backendObject.ApplyModifiedProperties();
            backendProp.objectReferenceValue = backend;
            so.ApplyModifiedProperties();
            Undo.RecordObject(r, "Enable MP4 recorder");
            r.enabled = true;
            captureCamera.enabled = false;
            EditorUtility.SetDirty(r);
            EditorUtility.SetDirty(backend);
            count++;
        }
        int removedButtons = 0;
        var objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in objects)
        {
            if (!t || !t.gameObject.scene.IsValid()) continue;
            if (t.name == "TestMediaProjectionPermissionButton")
            {
                Undo.DestroyObjectImmediate(t.gameObject);
                removedButtons++;
            }
        }
        Debug.Log("IE_DIRECT_MP4|CONFIGURED recorders=" + count
                  + " removed test buttons=" + removedButtons
                  + " | Existing block events and CSV recorder unchanged. SAVE SCENE (Cmd+S).");
    }
}
#endif
