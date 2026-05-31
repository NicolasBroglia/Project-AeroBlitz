using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class DepthIndicator : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Optional: Drop a flat 2D Sprite prefab here to act as a shadow")]
    [SerializeField] private GameObject shadowPrefab;

    [Tooltip("If false, the connecting line will be invisible during gameplay.")]
    [SerializeField] private bool showTetherLine = false;

    [Tooltip("Multiplier to make the shadow bigger or smaller for this specific character.")]
    [SerializeField][Range(0.1f, 5f)] private float baseShadowScale = 1f;

    private LineRenderer tetherLine;
    private GameObject activeShadow;
    private Quaternion baseShadowRotation;

    private void Awake()
    {
        tetherLine = GetComponent<LineRenderer>();
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

        Vector3 rayOrigin = playerPos + (Vector3.down * 0.5f);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100f))
        {
            floorPos = hit.point;
        }
        else
        {
            floorPos = new Vector3(playerPos.x, -10f, playerPos.z);
        }

        if (showTetherLine)
        {
            tetherLine.SetPosition(0, playerPos);
            tetherLine.SetPosition(1, floorPos);
        }

        if (activeShadow != null)
        {
            activeShadow.transform.position = floorPos + (Vector3.up * 0.02f);

            if (hit.collider != null)
            {
                activeShadow.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * baseShadowRotation;
            }

            // Calculate the altitude shrink effect, then multiply it by your custom Inspector scale!
            float altitude = playerPos.y - floorPos.y;
            float baseCalculation = Mathf.Clamp(1f - (altitude * 0.15f), 0.3f, 1f);

            float finalScale = baseCalculation * baseShadowScale; // <--- APPLIED HERE

            activeShadow.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
        }
    }

    private void OnDestroy()
    {
        if (activeShadow != null) Destroy(activeShadow);
    }
}