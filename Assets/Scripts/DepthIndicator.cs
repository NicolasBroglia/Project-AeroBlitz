using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class DepthIndicator : MonoBehaviour
{
    #region Settings & References

    [Header("Settings")]
    [Tooltip("Optional: Drop a flat 2D Sprite prefab here to act as a shadow")]
    [SerializeField] private GameObject shadowPrefab;
    
    [Tooltip("Selecciona la capa que corresponde al piso de tu mapa")]
    [SerializeField] private LayerMask floorLayerMask;

    [Tooltip("If false, the connecting line will be invisible during gameplay.")]
    [SerializeField] private bool showTetherLine = false;

    [Tooltip("Multiplier to make the shadow bigger or smaller for this specific character.")]
    [SerializeField][Range(0.1f, 5f)] private float baseShadowScale = 1f;

    private LineRenderer tetherLine;
    private GameObject activeShadow;
    private Quaternion baseShadowRotation;

    // Cache the hit data so we don't need to pass it around constantly
    private RaycastHit floorHit;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        floorLayerMask = LayerMask.GetMask("Floor");
        InitializeTether();
        InitializeShadow();
    }

    // --- AGREGAR ESTOS DOS MÉTODOS ---
    private void OnEnable()
    {
        // Cuando el Power-Up se prende (sale del pool), prendemos la sombra
        if (activeShadow != null) activeShadow.SetActive(true);
    }

    private void OnDisable()
    {
        // Cuando el Power-Up se apaga (vuelve al pool), apagamos la sombra
        if (activeShadow != null) activeShadow.SetActive(false);
    }
    // ---------------------------------

    private void LateUpdate()
    {
        Vector3 playerPos = transform.position;
        Vector3 floorPos = CalculateFloorPosition(playerPos);

        if (showTetherLine) UpdateTether(playerPos, floorPos);
        if (activeShadow != null) UpdateShadow(playerPos, floorPos);
    }

    private void OnDestroy()
    {
        if (activeShadow != null) Destroy(activeShadow);
    }

    #endregion
    #region Setup

    private void InitializeTether()
    {
        tetherLine = GetComponent<LineRenderer>();
        tetherLine.enabled = showTetherLine;
        tetherLine.positionCount = 2;
        tetherLine.startWidth = 0.05f;
        tetherLine.endWidth = 0.05f;
        tetherLine.useWorldSpace = true;
        tetherLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tetherLine.receiveShadows = false;
    }

    private void InitializeShadow()
    {
        if (shadowPrefab != null)
        {
            activeShadow = Instantiate(shadowPrefab);
            activeShadow.transform.SetParent(null); // Keep it strictly in world space
            baseShadowRotation = activeShadow.transform.rotation;
        }
    }

    #endregion

    #region Math & Visual Updates

    private Vector3 CalculateFloorPosition(Vector3 playerPos)
    {
        Vector3 rayOrigin = playerPos + (Vector3.down * 0.5f);

        // Ahora el Raycast es ultra liviano porque ignora el 90% de los objetos
        if (Physics.Raycast(rayOrigin, Vector3.down, out floorHit, 100f, floorLayerMask))
        {
            return floorHit.point;
        }

        return new Vector3(playerPos.x, -10f, playerPos.z);
    }

    private void UpdateTether(Vector3 playerPos, Vector3 floorPos)
    {
        tetherLine.SetPosition(0, playerPos);
        tetherLine.SetPosition(1, floorPos);
    }

    private void UpdateShadow(Vector3 playerPos, Vector3 floorPos)
    {
        // Keep the shadow slightly above the physical floor to prevent Z-fighting
        activeShadow.transform.position = floorPos + (Vector3.up * 0.02f);

        // Align the shadow with slopes/ramps
        if (floorHit.collider != null)
        {
            activeShadow.transform.rotation = Quaternion.FromToRotation(Vector3.up, floorHit.normal) * baseShadowRotation;
        }

        // Calculate the altitude shrink effect, then multiply it by your custom Inspector scale
        float altitude = playerPos.y - floorPos.y;
        float baseCalculation = Mathf.Clamp(1f - (altitude * 0.15f), 0.3f, 1f);
        float finalScale = baseCalculation * baseShadowScale;

        activeShadow.transform.localScale = new Vector3(finalScale, finalScale, finalScale);
    }

    #endregion
}