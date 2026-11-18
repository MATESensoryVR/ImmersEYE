#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ImmersiveEyeV7PerformanceSetup
{
    [MenuItem("Tools/ImmersiveEye/Apply Direct MP4 Performance Fix v7")]
    public static void Apply()
    {
        var backends = Object.FindObjectsByType<ImmersiveEyeAndroidMp4Backend>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (backends.Length != 1 || !backends[0].gameObject.scene.IsValid())
        {
            Debug.LogError("IE_DIRECT_MP4|V7_SETUP_ABORT|Expected exactly one direct MP4 backend in open scene; found " + backends.Length);
            return;
        }
        var backend = backends[0];
        var so = new SerializedObject(backend);
        so.FindProperty("outputWidth").intValue = 480;
        so.FindProperty("outputHeight").intValue = 270;
        so.FindProperty("maximumFps").intValue = 8;
        so.FindProperty("maxInFlightReadbacks").intValue = 3;
        so.ApplyModifiedProperties();
        Undo.RecordObject(backend, "Enable optimized video backend");
        backend.enabled = true;
        EditorUtility.SetDirty(backend);
        EditorSceneManager.MarkSceneDirty(backend.gameObject.scene);
        Debug.Log("IE_DIRECT_MP4|V7_CONFIGURED|480x270|fps=8|inFlight=3|Block/CSV logic unchanged. SAVE SCENE (Cmd+S).");
    }
}
#endif
