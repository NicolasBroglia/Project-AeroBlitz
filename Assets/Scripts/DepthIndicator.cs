using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class DepthIndicator : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Optional: Drop a flat 2D Sprite prefab here to act as a shadow")]
    [SerializeField] private GameObject shadowPrefab;

    [Tooltip("If false, the connecting line will be invisible during gameplay.")]
    [SerializeField] private bool showTetherLine = false;

    private LineRenderer tetherLine;
    private GameObject activeShadow;
    private Quaternion baseShadowRotation;

    private void Awake()
    {
        tetherLine = GetComponent<LineRenderer>();

        // Turn the line renderer component on or off based on your setting
        tetherLine.enabled = showTetherLine;

        tetherLine.positionCount = 2;
        tetherLine.startWidth = 0.05f;
        tetherLine.endWidth = 0.05f;
        tetherLine.useWorldSpace = true;
        tetherLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tetherLine.receiveShadows = false;

        if (shadowPrefab != null)
        {
            activeShadow = Instantiate(shadowPrefab);
            activeShadow.transform.SetParent(null);
            baseShadowRotation = activeShadow.transform.rotation;
        }
    }

    private void LateUpdate()
    {
        Vector3 playerPos = transform.position;
        Vector3 floorPos = playerPos;

        // Start the laser slightly below the player so they don't hit their own collider
        Vector3 rayOrigin = playerPos + (Vector3.down * 0.5f);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100f))
        {
            floorPos = hit.point;
        }
        else
        {
            // Fallback just in case they manage to fly completely off the map
            floorPos = new Vector3(playerPos.x, -10f, playerPos.z);
        }

        // Only calculate the line math if we actually want to see the line
        if (showTetherLine)
        {
            tetherLine.SetPosition(0, playerPos);
            tetherLine.SetPosition(1, floorPos);
        }

        // Manage the Shadow
        if (activeShadow != null)
        {
            // Place shadow slightly above the floor to prevent Z-fighting
            activeShadow.transform.position = floorPos + (Vector3.up * 0.02f);

            if (hit.collider != null)
            {
                activeShadow.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * baseShadowRotation;
            }

            // Shrink the shadow the higher the player flies
            float altitude = playerPos.y - floorPos.y;
            float scale = Mathf.Clamp(1f - (altitude * 0.15f), 0.3f, 1f);
            activeShadow.transform.localScale = new Vector3(scale, scale, scale);
        }
    }

    private void OnDestroy()
    {
        // Prevent orphaned shadows floating around if the player gets deleted
        if (activeShadow != null) Destroy(activeShadow);
    }
}