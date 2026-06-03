using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class IsometricCameraRig : MonoBehaviour
{
    #region Settings & Variables

    [Header("Target Tracking")]
    public List<string> targetTags = new List<string> { "Hunter", "Snitch" };
    [HideInInspector] public List<Transform> targets = new List<Transform>();
    [SerializeField] private float smoothTime = 0.3f;

    [Header("Isometric Settings")]
    [SerializeField] private Vector3 isometricRotation = new Vector3(30f, 45f, 0f);

    [Tooltip("How far the camera sits backwards along its own rotation path.")]
    [SerializeField] private float cameraDistance = 50f; // <-- NEW: Replaces the Vector3 offset

    [Header("Orthographic Zooming")]
    [SerializeField] private float minZoom = 10f;
    [SerializeField] private float maxZoom = 25f;
    [SerializeField] private float zoomPadding = 4f;
    [SerializeField] private float zoomSmoothTime = 0.3f;

    [Header("Screen Boundaries")]
    [Tooltip("Negative values let players go slightly OUTSIDE the screen. -0.05 = 5% outside, perfect for showing tails.")]
    [SerializeField] private float edgePadding = -0.05f;

    // Components
    private Camera cam;
    private Vector3 moveVelocity;
    private float zoomVelocity;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.nearClipPlane = -50f;
        transform.rotation = Quaternion.Euler(isometricRotation);
    }

    private void FixedUpdate()
    {
        CleanDeadTargets();

        if (targets.Count == 0)
        {
            FindTargets();
            if (targets.Count == 0) return;
        }

        Vector3 centroid = GetCentroid();

        EnforceGroupLeash(centroid);
        HandleCameraMovementAndZoom(centroid);
        EnforceMathematicalLines();
    }

    #endregion

    #region Target Acquisition & Centroid

    private void FindTargets()
    {
        targets.Clear();
        foreach (string tag in targetTags)
        {
            GameObject[] foundObjects = GameObject.FindGameObjectsWithTag(tag);
            foreach (GameObject obj in foundObjects)
            {
                targets.Add(obj.transform);
            }
        }
    }

    private void CleanDeadTargets()
    {
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            if (targets[i] == null) targets.RemoveAt(i);
        }
    }

    private Vector3 GetCentroid()
    {
        Vector3 centroid = Vector3.zero;
        foreach (Transform t in targets)
        {
            centroid += t.position;
        }
        return centroid / targets.Count;
    }

    private Vector2 CalculateMaxLeash()
    {
        if (cam == null) return Vector2.zero;

        float maxHalfWidth = maxZoom * cam.aspect;
        float maxHalfHeight = maxZoom;

        float leashX = maxHalfWidth;

        // UPGRADE: Uses the exact forward vector Y instead of Euler angles to prevent Unity 360-degree math bugs
        float pitchSin = Mathf.Abs(cam.transform.forward.y);
        float leashZ = maxHalfHeight / pitchSin;

        float paddingOffset = edgePadding < 0 ? Mathf.Abs(edgePadding) : -edgePadding;
        float multiplier = 1f + (paddingOffset * 2f);

        return new Vector2(leashX * multiplier, leashZ * multiplier);
    }

    #endregion

    #region Camera Logic

    private void HandleCameraMovementAndZoom(Vector3 centroid)
    {
        Quaternion inverseCamRotation = Quaternion.Inverse(Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f));
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (Transform t in targets)
        {
            localBounds.Encapsulate(inverseCamRotation * (t.position - centroid));
        }

        // MOVE (Calculates the mathematically perfect offset)
        Vector3 perfectOffset = -cam.transform.forward * cameraDistance;
        Vector3 targetPosition = centroid + perfectOffset;

        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref moveVelocity, smoothTime, Mathf.Infinity, Time.fixedDeltaTime);

        // ZOOM 
        float pitchSin = Mathf.Abs(cam.transform.forward.y);
        float sizeBasedOnWidth = (localBounds.size.x / cam.aspect) * 0.5f;
        float sizeBasedOnHeight = (localBounds.size.z * pitchSin) * 0.5f;

        float requiredSize = Mathf.Max(sizeBasedOnWidth, sizeBasedOnHeight) + zoomPadding;
        float targetZoom = Mathf.Clamp(requiredSize, minZoom, maxZoom);

        float appliedZoomSmooth = targetZoom > cam.orthographicSize ? zoomSmoothTime / 4f : zoomSmoothTime;
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, appliedZoomSmooth, Mathf.Infinity, Time.fixedDeltaTime);
    }

    #endregion

    #region Boundary Systems

    private void EnforceGroupLeash(Vector3 centroid)
    {
        if (targets.Count <= 1) return;

        Quaternion camRotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
        Quaternion inverseCamRotation = Quaternion.Inverse(camRotation);

        Vector2 maxLeash = CalculateMaxLeash();

        foreach (Transform t in targets)
        {
            Rigidbody rb = t.GetComponent<Rigidbody>();
            if (rb == null) continue;

            Vector3 pos = rb.position;
            Vector3 offsetFromCentroid = pos - centroid;
            Vector3 localOffset = inverseCamRotation * offsetFromCentroid;

            bool clamped = false;

            if (localOffset.x > maxLeash.x) { localOffset.x = maxLeash.x; clamped = true; }
            else if (localOffset.x < -maxLeash.x) { localOffset.x = -maxLeash.x; clamped = true; }

            if (localOffset.z > maxLeash.y) { localOffset.z = maxLeash.y; clamped = true; }
            else if (localOffset.z < -maxLeash.y) { localOffset.z = -maxLeash.y; clamped = true; }

            if (clamped)
            {
                Vector3 clampedWorldOffset = camRotation * localOffset;
                Vector3 finalPos = centroid + clampedWorldOffset;
                finalPos.y = pos.y;
                rb.MovePosition(finalPos);
            }
        }
    }

    private void EnforceMathematicalLines()
    {
        foreach (Transform t in targets)
        {
            Rigidbody rb = t.GetComponent<Rigidbody>();
            if (rb == null) continue;

            Vector3 screenPos = cam.WorldToViewportPoint(rb.position);
            bool hitTheLine = false;

            if (screenPos.x < edgePadding) { screenPos.x = edgePadding; hitTheLine = true; }
            else if (screenPos.x > 1f - edgePadding) { screenPos.x = 1f - edgePadding; hitTheLine = true; }

            if (screenPos.y < edgePadding) { screenPos.y = edgePadding; hitTheLine = true; }
            else if (screenPos.y > 1f - edgePadding) { screenPos.y = 1f - edgePadding; hitTheLine = true; }

            if (hitTheLine)
            {
                Vector3 clampedWorldPos = cam.ViewportToWorldPoint(screenPos);
                clampedWorldPos.y = rb.position.y;
                rb.MovePosition(clampedWorldPos);
            }
        }
    }

    #endregion

    #region Editor Debug

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        // 1. Draw Current Red Screen Bounds
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        Vector3 bl = GetGroundIntersection(new Vector3(edgePadding, edgePadding, 0), groundPlane);
        Vector3 tl = GetGroundIntersection(new Vector3(edgePadding, 1f - edgePadding, 0), groundPlane);
        Vector3 tr = GetGroundIntersection(new Vector3(1f - edgePadding, 1f - edgePadding, 0), groundPlane);
        Vector3 br = GetGroundIntersection(new Vector3(1f - edgePadding, edgePadding, 0), groundPlane);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(bl, tl);
        Gizmos.DrawLine(tl, tr);
        Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl);

        // 2. Draw Cyan Leash Bounds
        if (Application.isPlaying && targets != null && targets.Count > 0)
        {
            Vector3 centroid = GetCentroid();
            Vector2 maxLeash = CalculateMaxLeash();

            Gizmos.color = Color.cyan;
            Matrix4x4 oldMatrix = Gizmos.matrix;

            Gizmos.matrix = Matrix4x4.TRS(new Vector3(centroid.x, 0f, centroid.z), Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f), Vector3.one);

            Vector3 leashSize = new Vector3(maxLeash.x * 2f, 0.1f, maxLeash.y * 2f);
            Gizmos.DrawWireCube(Vector3.zero, leashSize);

            Gizmos.matrix = oldMatrix;
            Gizmos.DrawSphere(new Vector3(centroid.x, 0f, centroid.z), 1f);
        }
    }

    private Vector3 GetGroundIntersection(Vector3 viewportPoint, Plane plane)
    {
        Ray ray = cam.ViewportPointToRay(viewportPoint);
        if (plane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return Vector3.zero;
    }
#endif

    #endregion
}