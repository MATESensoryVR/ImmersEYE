using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Return from the final experiment screen to a fresh ImmersiveEye welcome screen.
/// Reloads the active scene, then explicitly restores the HomePanel. Already-saved
/// CSV files in Application.persistentDataPath are never deleted.
/// 
/// Replace the existing ImmersiveEyeReturnHomeButton.cs with this file.
/// Keep the existing Button GameObject and its component references unchanged.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class ImmersiveEyeReturnHomeButton : MonoBehaviour
{
    private Button button;
    private bool returning;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ReturnToHome);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(ReturnToHome);
    }

    public void ReturnToHome()
    {
        if (returning)
            return;
        returning = true;

        Debug.Log("IMMERSIVEEYE_RETURN_HOME|clicked=true|reloading=true");
        Time.timeScale = 1f;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || string.IsNullOrEmpty(activeScene.name))
        {
            Debug.LogError("IMMERSIVEEYE_RETURN_HOME|error=no_valid_active_scene");
            returning = false;
            return;
        }

        // Static callback survives unloading the old scene. It is removed after
        // the new scene loads, avoiding a persistent or duplicate listener.
        SceneManager.sceneLoaded -= RestoreWelcomeAfterReload;
        SceneManager.sceneLoaded += RestoreWelcomeAfterReload;

        if (activeScene.buildIndex >= 0)
            SceneManager.LoadScene(activeScene.buildIndex, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(activeScene.name, LoadSceneMode.Single);
    }

    private static void RestoreWelcomeAfterReload(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= RestoreWelcomeAfterReload;

        // Hide any saved end-of-session UI and experimental content first.
        string[] hideAtWelcome =
        {
            "EndScreenUI", "EndPanel", "BlockDConfirmPanel",
            "InstructionPanel", "CalibrationPanel", "CalibrationResultPanel",
            "BlockAIntroPanel", "BlockBIntroPanel", "BlockCIntroPanel", "BlockDIntroPanel",
            "BlockA_Plate", "BlockB_Root", "BlockC_Root", "BlockD_Root"
        };

        Transform[] all = FindSceneTransforms(scene);
        foreach (string targetName in hideAtWelcome)
        {
            foreach (Transform t in all)
            {
                if (t != null && t.name == targetName)
                    t.gameObject.SetActive(false);
            }
        }

        // Keep the nine-point validation parent ACTIVE for the next participant.
        // CalibrationValidationManager manages the individual targets itself.
        // Hiding this parent makes the targets invisible after returning Home.
        bool validationRootRestored = false;
        foreach (Transform t in all)
        {
            if (t != null && t.name == "ValidationTargetRoot")
            {
                t.gameObject.SetActive(true);
                validationRootRestored = true;
            }
        }
        if (!validationRootRestored)
            Debug.LogWarning("IMMERSIVEEYE_RETURN_HOME|ValidationTargetRoot=not_found");

        // Explicitly return to participant registration, even if the scene was
        // accidentally saved with HomePanel inactive.
        bool restored = false;
        foreach (Transform t in all)
        {
            if (t != null && t.name == "HomePanel")
            {
                t.gameObject.SetActive(true);
                restored = true;
            }
        }

        if (restored)
            Debug.Log("IMMERSIVEEYE_RETURN_HOME|sceneReloaded=true|HomePanel=active");
        else
            Debug.LogError("IMMERSIVEEYE_RETURN_HOME|sceneReloaded=true|HomePanel=not_found");
    }

    private static Transform[] FindSceneTransforms(Scene scene)
    {
        System.Collections.Generic.List<Transform> results =
            new System.Collections.Generic.List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
            results.AddRange(root.GetComponentsInChildren<Transform>(true));
        return results.ToArray();
    }
}
