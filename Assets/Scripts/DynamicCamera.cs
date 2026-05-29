using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class DynamicCamera : MonoBehaviour
{
    public List<Transform> targets;

    [Header("Positioning")]
    public Vector3 offset;
    public float smoothTime = 0.5f;
    private Vector3 moveVelocity;

    [Header("Zooming Bounds")]
    // Adjusted defaults for a typical perspective camera
    public float minZoom = 40f;  // The closest it will zoom in
    public float maxZoom = 85f;  // The furthest it will pull back
    public float zoomLimiter = 40f; // The distance where it hits maxZoom

    [Header("Zoom Speed")]
    public float zoomSmoothTime = 0.5f; // Matches movement smoothTime
    private float zoomVelocity;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (targets.Count == 0) return;

        MoveCamera();
        ZoomCamera();
    }

    void MoveCamera()
    {
        Vector3 centerPoint = GetCenterPoint();
        Vector3 newPosition = centerPoint + offset;

        transform.position = Vector3.SmoothDamp(transform.position, newPosition, ref moveVelocity, smoothTime);
    }

    void ZoomCamera()
    {
        float greatestDistance = GetGreatestDistance();

        // Calculate the target zoom based on distance
        float targetZoom = Mathf.Lerp(minZoom, maxZoom, greatestDistance / zoomLimiter);

        // Apply using SmoothDamp instead of Lerp for responsive, snappy zooming
        if (cam.orthographic)
        {
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetZoom, ref zoomVelocity, zoomSmoothTime);
        }
        else
        {
            cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetZoom, ref zoomVelocity, zoomSmoothTime);
        }
    }

    Vector3 GetCenterPoint()
    {
        if (targets.Count == 1) return targets[0].position;

        var bounds = new Bounds(targets[0].position, Vector3.zero);
        for (int i = 0; i < targets.Count; i++)
        {
            bounds.Encapsulate(targets[i].position);
        }
        return bounds.center;
    }

    float GetGreatestDistance()
    {
        if (targets.Count <= 1) return 0f;

        var bounds = new Bounds(targets[0].position, Vector3.zero);
        for (int i = 0; i < targets.Count; i++)
        {
            bounds.Encapsulate(targets[i].position);
        }

        return Mathf.Max(bounds.size.x, bounds.size.z);
    }
}