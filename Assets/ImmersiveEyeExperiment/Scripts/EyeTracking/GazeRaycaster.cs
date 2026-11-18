using UnityEngine;


public class GazeRaycaster : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]

    [Tooltip("Drag the EyeTrackingProvider GameObject here.")]
    [SerializeField]
    private EyeTrackingProvider eyeTrackingProvider;


    [Tooltip(
        "Drag XR Origin > Camera Offset > Main Camera here. "
        + "This is only used for Mac Editor simulation."
    )]
    [SerializeField]
    private Transform headCamera;


    // ============================================================
    // RAYCAST SETTINGS
    // ============================================================

    [Header("Raycast Settings")]

    [Tooltip("Maximum distance, in metres, that gaze can hit.")]
    [SerializeField]
    private float maximumDistance = 10.0f;


    [Tooltip(
        "Choose which Unity layers can be detected by gaze."
    )]
    [SerializeField]
    private LayerMask raycastLayers = ~0;


    [Tooltip(
        "Determines whether trigger colliders are detected."
    )]
    [SerializeField]
    private QueryTriggerInteraction triggerInteraction =
        QueryTriggerInteraction.Ignore;


    // ============================================================
    // EDITOR TESTING
    // ============================================================

    [Header("Editor Testing")]

    [Tooltip(
        "When testing on Mac, use Main Camera forward direction "
        + "instead of real eye tracking."
    )]
    [SerializeField]
    private bool simulateWithHeadForwardInEditor = true;


    // ============================================================
    // RAY STATUS
    // ============================================================

    [Header("Runtime - Ray")]

    [SerializeField]
    private bool gazeRayAvailable = false;


    [SerializeField]
    private Vector3 rayOrigin = Vector3.zero;


    [SerializeField]
    private Vector3 rayDirection = Vector3.forward;


    // ============================================================
    // HIT STATUS
    // ============================================================

    [Header("Runtime - Hit")]

    [SerializeField]
    private bool gazeHit = false;


    [SerializeField]
    private string hitObjectName = "";


    [SerializeField]
    private Vector3 hitPosition = Vector3.zero;


    [SerializeField]
    private Vector3 hitNormal = Vector3.zero;


    [SerializeField]
    private float hitDistance = 0.0f;


    // ============================================================
    // AOI STATUS
    // ============================================================

    [Header("Runtime - AOI")]

    [SerializeField]
    private bool aoiFound = false;


    [SerializeField]
    private string aoiName = "";


    [SerializeField]
    private string aoiCategory = "";


    [SerializeField]
    private string aoiId = "";


    [SerializeField]
    private bool aoiActive = false;


    // ============================================================
    // PRIVATE REFERENCES
    // ============================================================

    private GameObject hitObject;

    private Collider hitCollider;

    private AOI currentAOI;


    // ============================================================
    // PUBLIC READ-ONLY ACCESS
    // ============================================================

    public bool GazeRayAvailable =>
        gazeRayAvailable;


    public Vector3 RayOrigin =>
        rayOrigin;


    public Vector3 RayDirection =>
        rayDirection;


    public bool GazeHit =>
        gazeHit;


    public GameObject HitObject =>
        hitObject;


    public Collider HitCollider =>
        hitCollider;


    public string HitObjectName =>
        hitObjectName;


    public Vector3 HitPosition =>
        hitPosition;


    public Vector3 HitNormal =>
        hitNormal;


    public float HitDistance =>
        hitDistance;


    public bool AOIFound =>
        aoiFound;


    public AOI CurrentAOI =>
        currentAOI;


    public string AOIName =>
        aoiName;


    public string AOICategory =>
        aoiCategory;


    public string AOIId =>
        aoiId;


    public bool AOIActive =>
        aoiActive;


    // ============================================================
    // UNITY
    // ============================================================

    private void Update()
    {
#if UNITY_EDITOR

        if (simulateWithHeadForwardInEditor)
        {
            UpdateEditorSimulation();

            return;
        }

#endif

        UpdateRealGaze();
    }


    // ============================================================
    // REAL GAZE
    // ============================================================

    private void UpdateRealGaze()
    {
        if (eyeTrackingProvider == null)
        {
            ClearRay();

            return;
        }


        if (!eyeTrackingProvider.CombinedGazeValid)
        {
            ClearRay();

            return;
        }


        PerformRaycast(
            eyeTrackingProvider.CombinedGazeOriginWorld,
            eyeTrackingProvider.CombinedGazeDirectionWorld
        );
    }


    // ============================================================
    // MAC EDITOR SIMULATION
    // ============================================================

    private void UpdateEditorSimulation()
    {
        if (headCamera == null)
        {
            ClearRay();

            return;
        }


        PerformRaycast(
            headCamera.position,
            headCamera.forward
        );
    }


    // ============================================================
    // PERFORM RAYCAST
    // ============================================================

    private void PerformRaycast(
        Vector3 origin,
        Vector3 direction
    )
    {
        if (
            direction.sqrMagnitude
            <
            0.000001f
        )
        {
            ClearRay();

            return;
        }


        rayOrigin =
            origin;


        rayDirection =
            direction.normalized;


        gazeRayAvailable =
            true;


        Ray ray =
            new Ray(
                rayOrigin,
                rayDirection
            );


        bool didHit =
            Physics.Raycast(
                ray,
                out RaycastHit hit,
                maximumDistance,
                raycastLayers,
                triggerInteraction
            );


        if (didHit)
        {
            ProcessHit(
                hit
            );
        }
        else
        {
            ClearHit();
        }


        Debug.DrawRay(
            rayOrigin,
            rayDirection * maximumDistance,
            Color.green
        );
    }


    // ============================================================
    // PROCESS HIT
    // ============================================================

    private void ProcessHit(
        RaycastHit hit
    )
    {
        gazeHit =
            true;


        hitCollider =
            hit.collider;


        hitObject =
            hit.collider.gameObject;


        hitObjectName =
            hitObject.name;


        hitPosition =
            hit.point;


        hitNormal =
            hit.normal;


        hitDistance =
            hit.distance;


        FindAOI(
            hit.collider
        );
    }


    // ============================================================
    // FIND AOI
    // ============================================================

    private void FindAOI(
        Collider collider
    )
    {
        ClearAOI();


        if (collider == null)
        {
            return;
        }


        /*
         * First check the exact collider GameObject.
         */
        currentAOI =
            collider.GetComponent<AOI>();


        /*
         * If the collider itself does not contain an AOI,
         * search its parents.
         *
         * This allows structures such as:
         *
         * FoodAOI
         * ├── AOI component
         * └── ColliderChild
         *     └── Box Collider
         */
        if (currentAOI == null)
        {
            currentAOI =
                collider.GetComponentInParent<AOI>();
        }


        if (currentAOI == null)
        {
            return;
        }


        /*
         * Disabled AOIs are deliberately ignored.
         */
        if (!currentAOI.ActiveAOI)
        {
            currentAOI =
                null;

            return;
        }


        aoiFound =
            true;


        aoiActive =
            true;


        aoiName =
            currentAOI.AOIName;


        aoiCategory =
            currentAOI.AOICategory;


        aoiId =
            currentAOI.AOIId;
    }


    // ============================================================
    // CLEAR AOI
    // ============================================================

    private void ClearAOI()
    {
        aoiFound =
            false;


        currentAOI =
            null;


        aoiName =
            "";


        aoiCategory =
            "";


        aoiId =
            "";


        aoiActive =
            false;
    }


    // ============================================================
    // CLEAR HIT
    // ============================================================

    private void ClearHit()
    {
        gazeHit =
            false;


        hitObject =
            null;


        hitCollider =
            null;


        hitObjectName =
            "";


        hitPosition =
            Vector3.zero;


        hitNormal =
            Vector3.zero;


        hitDistance =
            0.0f;


        ClearAOI();
    }


    // ============================================================
    // CLEAR COMPLETE RAY
    // ============================================================

    private void ClearRay()
    {
        gazeRayAvailable =
            false;


        rayOrigin =
            Vector3.zero;


        rayDirection =
            Vector3.forward;


        ClearHit();
    }
}