using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraCage : MonoBehaviour
{
    [Header("Cage Settings")]
    [Tooltip("How tall the invisible walls are")]
    public float wallHeight = 50f;
    [Tooltip("How thick the walls are (keeps players from glitching through at high speeds)")]
    public float wallThickness = 10f;

    private GameObject leftWall, rightWall, topWall, bottomWall;
    private BoxCollider leftCol, rightCol, topCol, bottomCol;
    private Camera cam;

    // An invisible, infinite flat plane at Y=0 used for mathematical calculations
    private Plane groundPlane;

    void Start()
    {
        cam = GetComponent<Camera>();
        groundPlane = new Plane(Vector3.up, Vector3.zero);

        // Build the 4 invisible physics walls when the game starts
        CreateEdgeWall(out leftWall, out leftCol, "CameraWall_Left");
        CreateEdgeWall(out rightWall, out rightCol, "CameraWall_Right");
        CreateEdgeWall(out topWall, out topCol, "CameraWall_Top");
        CreateEdgeWall(out bottomWall, out bottomCol, "CameraWall_Bottom");
    }

    private void CreateEdgeWall(out GameObject wallObj, out BoxCollider col, string name)
    {
        wallObj = new GameObject(name);
        // We leave them unparented in the root of the scene so their rotation math stays pure
        col = wallObj.AddComponent<BoxCollider>();

        PhysicsMaterial noFriction = new PhysicsMaterial("NoFriction");
        noFriction.dynamicFriction = 0f;
        noFriction.staticFriction = 0f;
        noFriction.frictionCombine = PhysicsMaterialCombine.Minimum;
        col.material = noFriction;
    }

    void LateUpdate()
    {
        // 1. Calculate exactly where the 4 corners of the screen touch the floor
        Vector3 bottomLeft = GetFloorPoint(new Vector3(0, 0, 0));
        Vector3 topLeft = GetFloorPoint(new Vector3(0, 1, 0));
        Vector3 bottomRight = GetFloorPoint(new Vector3(1, 0, 0));
        Vector3 topRight = GetFloorPoint(new Vector3(1, 1, 0));

        // 2. Snap the physics walls perfectly between those 4 points
        UpdateWall(leftWall.transform, leftCol, bottomLeft, topLeft);
        UpdateWall(rightWall.transform, rightCol, bottomRight, topRight);
        UpdateWall(topWall.transform, topCol, topLeft, topRight);
        UpdateWall(bottomWall.transform, bottomCol, bottomLeft, bottomRight);
    }

    private Vector3 GetFloorPoint(Vector3 viewportPoint)
    {
        // Shoots a mathematical ray from the camera lens through the corner of the screen
        Ray ray = cam.ViewportPointToRay(viewportPoint);
        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance); // Returns the exact X,Y,Z coordinate on the floor
        }
        return Vector3.zero;
    }

    private void UpdateWall(Transform wallTrans, BoxCollider col, Vector3 p1, Vector3 p2)
    {
        // Find the exact middle point between the two screen corners
        Vector3 centerPoint = (p1 + p2) / 2f;

        // Move the wall to the middle, and sink it slightly into the floor so players can't dash under it
        wallTrans.position = centerPoint + (Vector3.up * (wallHeight / 2f - 5f));

        // Rotate the wall so it perfectly connects Corner 1 and Corner 2
        wallTrans.rotation = Quaternion.LookRotation(p2 - p1);

        // Stretch the collider to cover the entire distance
        float length = Vector3.Distance(p1, p2);
        col.size = new Vector3(wallThickness, wallHeight, length);
    }
}