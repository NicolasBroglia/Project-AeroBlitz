using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class DynamicCamera : MonoBehaviour
{
    #region Variables & Settings

    public List<Transform> targets = new List<Transform>();

    [Header("Isometric Settings")]
    [Tooltip("Standard Isometric Rotation is exactly X:30, Y:45, Z:0")]
    [SerializeField] private Vector3 isometricRotation = new Vector3(30f, 45f, 0f);

    [Tooltip("In Orthographic mode, offset sets the angle, not the zoom. Use (-20, 20, -20)")]
    [SerializeField] private Vector3 offset = new Vector3(-20f, 20f, -20f);
    [SerializeField] private float smoothTime = 0.5f;

    [Header("Orthographic Zooming")]
    [Tooltip("Orthographic size uses smaller numbers than FOV. Try 8 to 15 here.")]
    [SerializeField] private float minZoom = 10f;

    [Tooltip("The maximum size the camera can expand to. Try 20 to 35 here.")]
    [SerializeField] private float maxZoom = 25f;

    [Tooltip("How much extra screen space to leave around the players so they don't touch the exact edge.")]
    [SerializeField] private float zoomPadding = 4f;
    [SerializeField] private float zoomSmoothTime = 0.5f;

    private Vector3 moveVelocity;
    private float zoomVelocity;
    private Camera cam;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        cam = GetComponent<Camera>();

        // Force true Isometric orthographic projection
        cam.orthographic = true;
        transform.rotation = Quaternion.Euler(isometricRotation);
    }

    private void LateUpdate()
    {
        // Instantly scrub destroyed players
        targets.RemoveAll(t => t == null);
        if (targets.Count == 0) return;

        MoveCamera();
        ZoomCamera();
    }

    #endregion

    #region Camera Behavior

    public void SnapToTargets()
    {
        targets.RemoveAll(t => t == null);
        if (targets.Count == 0) return;

        Vector3 centerPoint = GetCenterPoint();
        transform.position = centerPoint + offset;

        float requiredSize = GetRequiredOrthographicSize();
        cam.orthographicSize = Mathf.Clamp(requiredSize, minZoom, maxZoom);
    }

    private void MoveCamera()
    {
        Vector3 centerPoint = GetCenterPoint();
        Vector3 newPosition = centerPoint + offset;

        transform.position = Vector3.SmoothDamp(transform.position, newPosition, ref moveVelocity, smoothTime);
    }

    private void ZoomCamera()
    {
        float requiredSize = GetRequiredOrthographicSize();
        float targetZoom = Mathf.Clamp(requiredSize, minZoom, maxZoom);

        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);
    }

    #endregion

    #region Mathematical Calculations

    private Vector3 GetCenterPoint()
    {
        if (targets.Count == 1) return targets[0].position;

        var bounds = new Bounds(targets[0].position, Vector3.zero);
        for (int i = 0; i < targets.Count; i++)
        {
            bounds.Encapsulate(targets[i].position);
        }
        return bounds.center;
    }

    private float GetRequiredOrthographicSize()
    {
        if (targets.Count <= 1) return minZoom;

        // Build a mathematical box that touches every player
        var bounds = new Bounds(targets[0].position, Vector3.zero);
        for (int i = 0; i < targets.Count; i++)
        {
            bounds.Encapsulate(targets[i].position);
        }

        // To keep everyone on screen, we find the longest distance they are spread apart.
        // We factor in the screen's aspect ratio so wide screens don't cut off the top/bottom.
        float boundsSize = Mathf.Max(bounds.size.x, bounds.size.z);
        float sizeBasedOnWidth = (boundsSize / cam.aspect) * 0.5f;
        float sizeBasedOnHeight = boundsSize * 0.5f;

        return Mathf.Max(sizeBasedOnWidth, sizeBasedOnHeight) + zoomPadding;
    }

    #endregion
}