using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HunterController : MonoBehaviour
{
    #region Enums & State

    public enum HunterState
    {
        Idle,
        Chasing,
        Stunned,
        Clashing,
        CapturedSnitch
    }

    [Header("State")]
    public HunterState currentState = HunterState.Idle;

    #endregion

    #region Variables & Settings

    [Header("References")]
    [SerializeField] private InputSystemManager inputManager;
    private Rigidbody rb;
    private Transform mainCam; // Cached camera transform

    [Header("Movement Settings (Horizontal)")]
    [SerializeField] private float moveSpeed = 50f;
    [SerializeField] private float maxSpeed = 18f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Lane Settings (Vertical)")]
    [SerializeField] private float laneChangeSpeed = 15f;
    [SerializeField] private int currentLane = 2;
    private bool isElevationNeutral = true;

    [Header("Dash Settings")]
    [SerializeField] private float dashForce = 35f;
    [SerializeField] private float dashCooldown = 3f;
    [Tooltip("How long the dash ignores the maxSpeed clamp (in seconds)")]
    [SerializeField] private float dashDuration = 0.25f;

    [Header("Depth Color Settings")]
    [Tooltip("How dark the color gets at the bottom lane")]
    [SerializeField] private float minBrightness = 0.4f;
    [Tooltip("How bright the color gets at the top lane")]
    [SerializeField] private float maxBrightness = 1.0f;
    private Color baseColor = Color.white;
    private Renderer[] characterRenderers;

    private float dashCooldownTimer = 0f;
    private float activeDashTimer = 0f;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.useGravity = false;

        inputManager = GetComponent<InputSystemManager>();

        // Cache camera for performance
        mainCam = Camera.main.transform;

        ChangeState(HunterState.Idle);
    }

    private void Update()
    {
        ManageStateMachine();

        if (dashCooldownTimer > 0) dashCooldownTimer -= Time.deltaTime;
        if (activeDashTimer > 0) activeDashTimer -= Time.deltaTime;

        bool canPlay = GameManager.Instance.CurrentState == GameState.ActiveMatch || GameManager.Instance.CurrentState == GameState.Lobby;

        if (currentState == HunterState.Chasing && canPlay)
        {
            if (inputManager.GetDash() && dashCooldownTimer <= 0f)
            {
                PerformDash();
            }

            // --- DISCRETE LANE SWITCHING LOGIC ---
            float elevationInput = inputManager.GetElevation();

            if (isElevationNeutral)
            {
                if (elevationInput > 0.5f)
                {
                    currentLane = Mathf.Min(currentLane + 1, SpawnManager.Instance.maxLanes - 1);
                    isElevationNeutral = false;
                }
                else if (elevationInput < -0.5f)
                {
                    currentLane = Mathf.Max(currentLane - 1, 0);
                    isElevationNeutral = false;
                }
            }
            else if (Mathf.Abs(elevationInput) < 0.2f)
            {
                isElevationNeutral = true;
            }
        }
    }

    private void FixedUpdate()
    {
        if (currentState == HunterState.Chasing)
        {
            bool canPlay = GameManager.Instance.CurrentState == GameState.ActiveMatch || GameManager.Instance.CurrentState == GameState.Lobby;

            if (canPlay)
            {
                HandleMovement();
                HandleRotation();
            }
            else
            {
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 5f);
            }

            ApplyHeightClamp();
            UpdateDepthColor();
        }
    }

    #endregion

    #region State Machine

    public void ChangeState(HunterState newState)
    {
        currentState = newState;
    }

    private void ManageStateMachine()
    {
        switch (currentState)
        {
            case HunterState.Idle:
            case HunterState.Chasing:
                break;

            case HunterState.Stunned:
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.deltaTime * 5f);
                break;

            case HunterState.Clashing:
            case HunterState.CapturedSnitch:
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                break;
        }
    }

    #endregion

    #region Physics & Movement

    private void HandleMovement()
    {
        // 1. HORIZONTAL MOVEMENT (X & Z) - Camera Relative
        Vector2 planeInput = inputManager.GetMovement();

        // Grab the camera's directions
        Vector3 camForward = mainCam.forward;
        Vector3 camRight = mainCam.right;

        // Flatten them so movement strictly stays on the horizontal plane
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        // Multiply the input by the camera's directions
        Vector3 moveDirection = (camForward * planeInput.y + camRight * planeInput.x).normalized;

        rb.AddForce(moveDirection * moveSpeed, ForceMode.Acceleration);

        if (activeDashTimer <= 0f)
        {
            Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            if (flatVelocity.magnitude > maxSpeed)
            {
                flatVelocity = flatVelocity.normalized * maxSpeed;
                rb.linearVelocity = new Vector3(flatVelocity.x, rb.linearVelocity.y, flatVelocity.z);
            }
        }

        // 2. VERTICAL MOVEMENT (Y - Lane Snapping)
        float targetHeight = SpawnManager.Instance.GetLaneHeight(currentLane);

        float heightDifference = targetHeight - rb.position.y;
        float newVerticalVelocity = heightDifference * laneChangeSpeed;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, newVerticalVelocity, rb.linearVelocity.z);
    }

    private void HandleRotation()
    {
        // 1. Calculate the Horizontal Facing (Yaw)
        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Quaternion targetHorizontal;

        if (flatVelocity.sqrMagnitude > 0.1f)
        {
            targetHorizontal = Quaternion.LookRotation(flatVelocity.normalized);
        }
        else
        {
            targetHorizontal = Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);
        }

        // 2. Calculate the Vertical Tilt (Pitch)
        float pitchAngle = -rb.linearVelocity.y * 2.5f;
        pitchAngle = Mathf.Clamp(pitchAngle, -45f, 45f);

        // 3. Combine horizontal and vertical rotation
        Quaternion finalRotation = targetHorizontal * Quaternion.Euler(pitchAngle, 0f, 0f);

        rb.MoveRotation(Quaternion.Slerp(rb.rotation, finalRotation, Time.fixedDeltaTime * rotationSpeed));
    }

    private void ApplyHeightClamp()
    {
        Vector3 clampedPosition = rb.position;
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, SpawnManager.Instance.minHeight, SpawnManager.Instance.maxHeight);

        if (rb.position.y != clampedPosition.y)
        {
            rb.position = clampedPosition;
        }
    }

    public void SetSpeed(float newSpeed) { moveSpeed = newSpeed; }
    public void SetMaxSpeed(float newMaxSpeed) { maxSpeed = newMaxSpeed; }
    public float GetSpeed() { return moveSpeed; }
    public float GetMaxSpeed() { return maxSpeed; }

    #endregion

    #region Dash Logic

    private void PerformDash()
    {
        dashCooldownTimer = dashCooldown;
        activeDashTimer = dashDuration;

        Vector2 planeInput = inputManager.GetMovement();

        // Camera-Relative Dash
        Vector3 camForward = mainCam.forward;
        Vector3 camRight = mainCam.right;
        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 dashDir = (camForward * planeInput.y + camRight * planeInput.x).normalized;

        if (dashDir == Vector3.zero)
        {
            Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            dashDir = flatVelocity.sqrMagnitude > 0.1f ? flatVelocity.normalized : transform.forward;
        }

        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        rb.AddForce(dashDir * dashForce, ForceMode.Impulse);
    }

    #endregion

    #region Collision & Combat

    private void OnCollisionEnter(Collision collision)
    {
        if (currentState != HunterState.Chasing) return;

        if (collision.gameObject.CompareTag("Snitch"))
        {
            SnitchController snitch = collision.gameObject.GetComponent<SnitchController>();
            if (snitch != null && GameManager.Instance != null)
            {
                GameManager.Instance.StartClash(this, snitch);
            }
            return;
        }

        if (collision.gameObject.CompareTag("Hunter"))
        {
            HandleHunterCollision(collision);
        }
    }

    private void HandleHunterCollision(Collision collision)
    {
        ChangeState(HunterState.Stunned);
        Vector3 bounceDirection = (transform.position - collision.transform.position);
        bounceDirection.y = 0;

        rb.AddForce(bounceDirection.normalized * 10f, ForceMode.Impulse);
        StartCoroutine(StunRecoveryRoutine());
    }

    public void ApplyKnockbackFrom(Vector3 sourcePosition)
    {
        ChangeState(HunterState.Stunned);
        Vector3 bounceDirection = (transform.position - sourcePosition);
        bounceDirection.y = 0;

        rb.AddForce(bounceDirection.normalized * 20f, ForceMode.Impulse);
        StartCoroutine(StunRecoveryRoutine());
    }

    private System.Collections.IEnumerator StunRecoveryRoutine()
    {
        yield return new WaitForSeconds(1.5f);
        if (currentState == HunterState.Stunned)
        {
            ChangeState(HunterState.Chasing);
        }
    }

    #endregion

    #region Editor Debug

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || inputManager == null) return;

        GUIStyle style = new GUIStyle();
        style.normal.textColor = gameObject.CompareTag("Snitch") ? Color.green : Color.red;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;

        string label = $"Device: {inputManager.deviceID}\nLANE: {currentLane + 1}";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f, label, style);
    }
#endif

    #endregion

    #region Spawning & Initialization

    public void InitializeAtSpawn(Vector3 exactPosition, Quaternion exactRotation)
    {
        // 1. Wipe forces
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 2. Figure out which lane index this exact Y coordinate belongs to
        float heightPercentage = Mathf.InverseLerp(SpawnManager.Instance.minHeight, SpawnManager.Instance.maxHeight, exactPosition.y);
        currentLane = Mathf.RoundToInt(heightPercentage * (SpawnManager.Instance.maxLanes - 1));
        currentLane = Mathf.Clamp(currentLane, 0, SpawnManager.Instance.maxLanes - 1);

        // 3. Force the physical location
        rb.position = exactPosition;
        rb.rotation = exactRotation;
        transform.position = exactPosition;
        transform.rotation = exactRotation;
    }

    #endregion

    public void SetBaseColor(Color color)
    {
        baseColor = color;
        characterRenderers = GetComponentsInChildren<Renderer>();
        UpdateDepthColor(); // Apply immediately so they don't flash white on spawn
    }

    private void UpdateDepthColor()
    {
        if (characterRenderers == null || characterRenderers.Length == 0) return;

        // 1. Calculate exact height percentage (0.0 is floor, 1.0 is ceiling)
        float heightPct = Mathf.InverseLerp(SpawnManager.Instance.minHeight, SpawnManager.Instance.maxHeight, rb.position.y);

        // 2. Convert the base color to HSV so we can safely edit brightness without ruining the hue
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);

        // 3. Calculate the new brightness multiplier
        float targetBrightness = Mathf.Lerp(minBrightness, maxBrightness, heightPct);
        v = Mathf.Clamp01(v * targetBrightness);

        // 4. Convert back to RGB and apply
        Color depthColor = Color.HSVToRGB(h, s, v);
        foreach (Renderer rend in characterRenderers)
        {
            rend.material.color = depthColor;
        }
    }
}