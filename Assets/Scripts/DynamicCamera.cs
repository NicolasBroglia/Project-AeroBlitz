using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class DynamicCamera : MonoBehaviour
{
    public List<Transform> targets = new List<Transform>();

    [Header("Positioning")]
    [SerializeField] private Vector3 offset;
    [SerializeField] private float smoothTime = 0.5f;
    [SerializeField] private Vector3 moveVelocity;

    [Header("Zooming Bounds")]
    [SerializeField] private float minZoom = 40f;
    [SerializeField] private float maxZoom = 85f;
    [SerializeField] private float zoomLimiter = 40f;

    [Header("Zoom Speed")]
    [SerializeField] private float zoomSmoothTime = 0.5f;
    [SerializeField] private float zoomVelocity;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        targets.RemoveAll(t => t == null);
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
        float targetZoom = Mathf.Lerp(minZoom, maxZoom, greatestDistance / zoomLimiter);

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

    public void SnapToTargets()
    {
        targets.RemoveAll(t => t == null);
        if (targets.Count == 0) return;

        Vector3 centerPoint = GetCenterPoint();
        transform.position = centerPoint + offset;

        float greatestDistance = GetGreatestDistance();
        float targetZoom = Mathf.Lerp(minZoom, maxZoom, greatestDistance / zoomLimiter);

        if (cam.orthographic)
        {
            cam.orthographicSize = targetZoom;
        }
        else
        {
            cam.fieldOfView = targetZoom;
        }
    }
}