using UnityEngine;

// This tag forces Unity to run this script AFTER everything else (like DynamicCamera) has finished moving!
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Camera))]
public class CameraCage : MonoBehaviour
{
    #region Variables & Settings

    [Header("Cage Settings")]
    [Tooltip("How tall the invisible walls are")]
    public float wallHeight = 50f;
    [Tooltip("How thick the walls are")]
    public float wallThickness = 10f;
    [Tooltip("The MAX height players can fly. Calculates walls here so they can't fly over them.")]
    public float maxFlightHeight = 4f; // <-- NEW: Fixes the 3D escape flaw

    private GameObject leftWall, rightWall, topWall, bottomWall;
    private BoxCollider leftCol, rightCol, topCol, bottomCol;
    private Camera cam;
    private Plane calculationPlane;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        cam = GetComponent<Camera>();

        // Build the plane at the player's flight height, not the floor!
        calculationPlane = new Plane(Vector3.up, new Vector3(0, maxFlightHeight, 0));

        BuildPhysicsCage();
    }

    void LateUpdate()
    {
        UpdateCageTransform();
    }

    #endregion

    #region Initialization

    private void BuildPhysicsCage()
    {
        CreateEdgeWall(out leftWall, out leftCol, "CameraWall_Left");
        CreateEdgeWall(out rightWall, out rightCol, "CameraWall_Right");
        CreateEdgeWall(out topWall, out topCol, "CameraWall_Top");
        CreateEdgeWall(out bottomWall, out bottomCol, "CameraWall_Bottom");
    }

    private void CreateEdgeWall(out GameObject wallObj, out BoxCollider col, string name)
    {
        wallObj = new GameObject(name);
        col = wallObj.AddComponent<BoxCollider>();

        Rigidbody rb = wallObj.AddComponent<Rigidbody>();
        rb.isKinematic = true; // Makes it so gravity/pushes don't affect the wall
        rb.interpolation = RigidbodyInterpolation.Interpolate; // Smooths out the movement

        PhysicsMaterial noFriction = new PhysicsMaterial("NoFriction");
        noFriction.dynamicFriction = 0f;
        noFriction.staticFriction = 0f;
        noFriction.frictionCombine = PhysicsMaterialCombine.Minimum;

        col.material = noFriction;
    }

    #endregion

    #region Wall Updates & Math

    private void UpdateCageTransform()
    {
        // Add a slight padding so the players bounce *just* before touching the edge of the screen
        float pad = 0.02f;

        Vector3 bottomLeft = GetPlanePoint(new Vector3(pad, pad, 0));
        Vector3 topLeft = GetPlanePoint(new Vector3(pad, 1 - pad, 0));
        Vector3 bottomRight = GetPlanePoint(new Vector3(1 - pad, pad, 0));
        Vector3 topRight = GetPlanePoint(new Vector3(1 - pad, 1 - pad, 0));

        UpdateWall(leftWall.transform, leftCol, bottomLeft, topLeft);
        UpdateWall(rightWall.transform, rightCol, bottomRight, topRight);
        UpdateWall(topWall.transform, topCol, topLeft, topRight);
        UpdateWall(bottomWall.transform, bottomCol, bottomLeft, bottomRight);
    }

    private Vector3 GetPlanePoint(Vector3 viewportPoint)
    {
        Ray ray = cam.ViewportPointToRay(viewportPoint);
        if (calculationPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return Vector3.zero;
    }

    private void UpdateWall(Transform wallTrans, BoxCollider col, Vector3 p1, Vector3 p2)
    {
        Vector3 centerPoint = (p1 + p2) / 2f;

        // Center the wall exactly on the flight height
        centerPoint.y = maxFlightHeight;

        wallTrans.position = centerPoint;
        wallTrans.rotation = Quaternion.LookRotation(p2 - p1);

        float length = Vector3.Distance(p1, p2);

        // Add extra padding to the length so the corners overlap securely
        col.size = new Vector3(wallThickness, wallHeight, length + 10f);
    }

    #endregion

    #region Editor Debug

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            cam = GetComponent<Camera>();
            calculationPlane = new Plane(Vector3.up, new Vector3(0, maxFlightHeight, 0));
        }

        if (cam == null) return;

        float pad = 0.02f;
        Vector3 bottomLeft = GetPlanePoint(new Vector3(pad, pad, 0));
        Vector3 topLeft = GetPlanePoint(new Vector3(pad, 1 - pad, 0));
        Vector3 topRight = GetPlanePoint(new Vector3(1 - pad, 1 - pad, 0));
        Vector3 bottomRight = GetPlanePoint(new Vector3(1 - pad, pad, 0));

        Gizmos.color = Color.red;
        Gizmos.DrawLine(bottomLeft, topLeft);
        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);

        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Vector3 center = (bottomLeft + topRight) / 2f;
        center.y = maxFlightHeight;
        Vector3 size = new Vector3(Vector3.Distance(bottomLeft, bottomRight), wallHeight, Vector3.Distance(bottomLeft, topLeft));
        Gizmos.DrawWireCube(center, size);
    }
#endif

    #endregion
}