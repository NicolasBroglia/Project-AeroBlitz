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
    [SerializeField] private bool showTetherLine = true;

    [Header("Shadow Adjustments")]
    [Tooltip("Multiplier to make the shadow bigger or smaller for this specific character.")]
    [SerializeField][Range(0.1f, 5f)] private float baseShadowScale = 1f;
    [Tooltip("Use this to manually align the shadow if your 3D model's pivot is off-center.")]
    [SerializeField] private Vector3 centerOffset = Vector3.zero;
    [Tooltip("Stretch or thin the shadow. (X = Width, Y = Height/Length for 2D sprites)")]
    [SerializeField] private Vector3 shadowProportions = Vector3.one;

    [Header("Tether Settings")]
    [Tooltip("How transparent the tether line should be (0 = invisible, 1 = solid color).")]
    [SerializeField][Range(0f, 1f)] private float tetherAlpha = 0.15f;

    // Components & State
    private LineRenderer tetherLine;
    private GameObject activeShadow;
    private RaycastHit floorHit;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        floorLayerMask = LayerMask.GetMask("Floor");
        InitializeTether();
        InitializeShadow();
    }

    private void OnEnable()
    {
        if (activeShadow != null) activeShadow.SetActive(true);
    }

    private void OnDisable()
    {
        if (activeShadow != null) activeShadow.SetActive(false);
    }

    private void LateUpdate()
    {
        Vector3 trueCenter = transform.position + transform.TransformVector(centerOffset);
        Vector3 floorPos = CalculateFloorPosition(trueCenter);

        float currentAltitude = trueCenter.y - floorPos.y;

        if (showTetherLine) UpdateTether(trueCenter, floorPos);
        if (activeShadow != null) UpdateShadow(floorPos, currentAltitude);
    }

    private void OnDestroy()
    {
        if (activeShadow != null) Destroy(activeShadow);
    }

    #endregion

    #region Setup & Color Initialization

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

        // Force a shader that supports transparency
        tetherLine.material = new Material(Shader.Find("Sprites/Default"));
    }

    private void InitializeShadow()
    {
        if (shadowPrefab != null)
        {
            activeShadow = Instantiate(shadowPrefab);
            activeShadow.transform.SetParent(null);
        }
    }

    // Called ONCE by GameManager.cs when the player connects
    public void SetBaseColor(Color color)
    {
        Renderer[] characterRenderers = GetComponentsInChildren<Renderer>();

        // 1. Apply the solid, unchanging color to the 3D model
        foreach (Renderer rend in characterRenderers)
        {
            // Do not paint the shadow object or the line renderer
            if (activeShadow != null && rend.gameObject == activeShadow) continue;
            if (rend.gameObject == this.gameObject) continue;

            rend.material.color = color;
        }

        // 2. Apply the transparent version of the color to the tether line
        if (tetherLine != null)
        {
            Color transparentTetherColor = new Color(color.r, color.g, color.b, tetherAlpha);
            tetherLine.startColor = transparentTetherColor;
            tetherLine.endColor = transparentTetherColor;
        }
    }

    #endregion

    #region Math & Visual Updates

    private Vector3 CalculateFloorPosition(Vector3 originPos)
    {
        Vector3 rayOrigin = originPos + (Vector3.down * 0.5f);

        if (Physics.Raycast(rayOrigin, Vector3.down, out floorHit, 100f, floorLayerMask))
        {
            return floorHit.point;
        }

        // Failsafe if they fly off the map
        return new Vector3(originPos.x, -10f, originPos.z);
    }

    private void UpdateTether(Vector3 playerPos, Vector3 floorPos)
    {
        tetherLine.SetPosition(0, playerPos);
        tetherLine.SetPosition(1, floorPos);
    }

    private void UpdateShadow(Vector3 floorPos, float altitude)
    {
        activeShadow.transform.position = floorPos + (Vector3.up * 0.02f);

        Vector3 parentEuler = transform.eulerAngles;
        Quaternion inheritedRotation = Quaternion.Euler(90f, parentEuler.y, parentEuler.z);

        if (floorHit.collider != null)
        {
            Quaternion floorAlignment = Quaternion.FromToRotation(Vector3.up, floorHit.normal);
            activeShadow.transform.rotation = floorAlignment * inheritedRotation;
        }
        else
        {
            activeShadow.transform.rotation = inheritedRotation;
        }

        // Scale shadow based on altitude
        float baseCalculation = Mathf.Clamp(1f - (altitude * 0.15f), 0.3f, 1f);
        float finalScale = baseCalculation * baseShadowScale;

        activeShadow.transform.localScale = new Vector3(
            finalScale * shadowProportions.x,
            finalScale * shadowProportions.y,
            finalScale * shadowProportions.z
        );
    }

    #endregion
}