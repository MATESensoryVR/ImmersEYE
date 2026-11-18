using UnityEngine;


public class GazeRayVisualizer : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]

    [Tooltip("Drag the EyeTrackingProvider GameObject here.")]
    [SerializeField]
    private EyeTrackingProvider eyeTrackingProvider;


    [Tooltip("Drag this GameObject's Line Renderer here.")]
    [SerializeField]
    private LineRenderer gazeLine;


    [Tooltip("Drag the GazeCursor sphere here.")]
    [SerializeField]
    private Transform gazeCursor;


    [Tooltip(
        "Optional. Drag XR Origin > Camera Offset > Main Camera here. "
        + "Used only for Editor simulation."
    )]
    [SerializeField]
    private Transform headCamera;


    // ============================================================
    // VISUAL SETTINGS
    // ============================================================

    [Header("Visualization")]

    [Tooltip("Maximum gaze ray length in metres.")]
    [SerializeField]
    private float rayLength = 5.0f;


    [Tooltip("Size of the gaze cursor sphere.")]
    [SerializeField]
    private float cursorScale = 0.02f;


    [Tooltip(
        "If enabled, the visualizer uses the derived binocular "
        + "combined gaze when at least one eye is valid."
    )]
    [SerializeField]
    private bool useCombinedGaze = true;


    // ============================================================
    // EDITOR TESTING
    // ============================================================

    [Header("Editor Testing")]

    [Tooltip(
        "While testing on Mac, use the Main Camera forward direction "
        + "to verify that the visualizer itself works. "
        + "This is NOT eye-tracking data."
    )]
    [SerializeField]
    private bool simulateWithHeadForwardInEditor = true;


    // ============================================================
    // RUNTIME STATUS
    // ============================================================

    [Header("Runtime Status")]

    [SerializeField]
    private bool visualizerActive = false;


    [SerializeField]
    private Vector3 currentOrigin = Vector3.zero;


    [SerializeField]
    private Vector3 currentDirection = Vector3.forward;


    // ============================================================
    // UNITY
    // ============================================================

    private void Start()
    {
        ValidateReferences();

        ConfigureLineRenderer();

        UpdateCursorScale();
    }


    private void Update()
    {
        UpdateCursorScale();

#if UNITY_EDITOR

        if (simulateWithHeadForwardInEditor)
        {
            DrawEditorSimulation();

            return;
        }

#endif

        DrawRealEyeTracking();
    }


    // ============================================================
    // REAL EYE TRACKING
    // ============================================================

    private void DrawRealEyeTracking()
    {
        if (eyeTrackingProvider == null)
        {
            HideVisualizer();

            return;
        }


        if (
            useCombinedGaze
            &&
            eyeTrackingProvider.CombinedGazeValid
        )
        {
            currentOrigin =
                eyeTrackingProvider.CombinedGazeOriginWorld;


            currentDirection =
                eyeTrackingProvider
                .CombinedGazeDirectionWorld
                .normalized;


            DrawRay(
                currentOrigin,
                currentDirection
            );


            return;
        }


        if (eyeTrackingProvider.LeftGazeValid)
        {
            currentOrigin =
                eyeTrackingProvider.LeftGazeOriginWorld;


            currentDirection =
                eyeTrackingProvider
                .LeftGazeDirectionWorld
                .normalized;


            DrawRay(
                currentOrigin,
                currentDirection
            );


            return;
        }


        if (eyeTrackingProvider.RightGazeValid)
        {
            currentOrigin =
                eyeTrackingProvider.RightGazeOriginWorld;


            currentDirection =
                eyeTrackingProvider
                .RightGazeDirectionWorld
                .normalized;


            DrawRay(
                currentOrigin,
                currentDirection
            );


            return;
        }


        HideVisualizer();
    }


    // ============================================================
    // MAC EDITOR SIMULATION
    // ============================================================

    private void DrawEditorSimulation()
    {
        if (headCamera == null)
        {
            HideVisualizer();

            return;
        }


        currentOrigin =
            headCamera.position;


        currentDirection =
            headCamera.forward.normalized;


        DrawRay(
            currentOrigin,
            currentDirection
        );
    }


    // ============================================================
    // DRAW
    // ============================================================

    private void DrawRay(
        Vector3 origin,
        Vector3 direction
    )
    {
        if (gazeLine == null)
        {
            return;
        }


        if (
            direction.sqrMagnitude
            <
            0.000001f
        )
        {
            HideVisualizer();

            return;
        }


        Vector3 endPoint =
            origin
            +
            direction.normalized
            *
            rayLength;


        gazeLine.enabled =
            true;


        gazeLine.positionCount =
            2;


        gazeLine.SetPosition(
            0,
            origin
        );


        gazeLine.SetPosition(
            1,
            endPoint
        );


        if (gazeCursor != null)
        {
            gazeCursor.gameObject.SetActive(
                true
            );


            gazeCursor.position =
                endPoint;
        }


        visualizerActive =
            true;
    }


    // ============================================================
    // HIDE
    // ============================================================

    private void HideVisualizer()
    {
        if (gazeLine != null)
        {
            gazeLine.enabled =
                false;
        }


        if (gazeCursor != null)
        {
            gazeCursor.gameObject.SetActive(
                false
            );
        }


        visualizerActive =
            false;
    }


    // ============================================================
    // SETUP
    // ============================================================

    private void ConfigureLineRenderer()
    {
        if (gazeLine == null)
        {
            return;
        }


        gazeLine.positionCount =
            2;


        gazeLine.useWorldSpace =
            true;


        gazeLine.startWidth =
            0.005f;


        gazeLine.endWidth =
            0.005f;
    }


    private void UpdateCursorScale()
    {
        if (gazeCursor == null)
        {
            return;
        }


        gazeCursor.localScale =
            Vector3.one
            *
            cursorScale;
    }


    private void ValidateReferences()
    {
        if (eyeTrackingProvider == null)
        {
            Debug.LogWarning(
                "GazeRayVisualizer: EyeTrackingProvider is not assigned."
            );
        }


        if (gazeLine == null)
        {
            Debug.LogWarning(
                "GazeRayVisualizer: LineRenderer is not assigned."
            );
        }


        if (gazeCursor == null)
        {
            Debug.LogWarning(
                "GazeRayVisualizer: GazeCursor is not assigned."
            );
        }


        if (headCamera == null)
        {
            Debug.LogWarning(
                "GazeRayVisualizer: Head Camera is not assigned. "
                +
                "Mac Editor simulation will not work."
            );
        }
    }
}