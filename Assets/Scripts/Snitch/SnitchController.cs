using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SnitchController : MonoBehaviour
{
    #region Enums & State

    public enum SnitchState
    {
        Idle,
        Fleeing,
        Clashing,
        Captured
    }

    [Header("State")]
    public SnitchState currentState = SnitchState.Idle;

    #endregion

    #region Variables & Settings

    [Header("References")]
    [SerializeField] private InputSystemManager inputManager;
    private Rigidbody rb;

    [Header("Movement Settings (Horizontal)")]
    [SerializeField] private float moveSpeed = 40f;
    [SerializeField] private float maxSpeed = 15f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Lane Settings (Vertical)")]
    [SerializeField] private float laneChangeSpeed = 15f;
    [SerializeField] private int currentLane = 2;
    private bool isElevationNeutral = true;

    [Header("Dash Settings")]
    [SerializeField] private float dashForce = 25f;
    [SerializeField] private float dashCooldown = 1.5f;
    [Tooltip("How long the dash ignores the maxSpeed clamp (in seconds)")]
    [SerializeField] private float dashDuration = 0.2f;

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

        ChangeState(SnitchState.Idle);
    }

    private void Update()
    {
        ManageStateMachine();

        if (dashCooldownTimer > 0) dashCooldownTimer -= Time.deltaTime;
        if (activeDashTimer > 0) activeDashTimer -= Time.deltaTime;

        bool canPlay = GameManager.Instance.CurrentState == GameState.ActiveMatch || GameManager.Instance.CurrentState == GameState.Lobby;

        if (currentState == SnitchState.Fleeing && canPlay)
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
        if (currentState == SnitchState.Fleeing)
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
        }
    }

    #endregion

    #region State Machine

    public void ChangeState(SnitchState newState)
    {
        currentState = newState;
    }

    private void ManageStateMachine()
    {
        switch (currentState)
        {
            case SnitchState.Idle:
            case SnitchState.Fleeing:
                break;

            case SnitchState.Clashing:
            case SnitchState.Captured:
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                break;
        }
    }

    #endregion

    #region Physics & Movement

    private void HandleMovement()
    {
        // 1. HORIZONTAL MOVEMENT (X & Z)
        Vector2 planeInput = inputManager.GetMovement();
        Vector3 moveDirection = new Vector3(planeInput.x, 0f, planeInput.y).normalized;

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
        Vector3 dashDir = new Vector3(planeInput.x, 0f, planeInput.y).normalized;

        if (dashDir == Vector3.zero)
        {
            Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            dashDir = flatVelocity.sqrMagnitude > 0.1f ? flatVelocity.normalized : transform.forward;
        }

        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        rb.AddForce(dashDir * dashForce, ForceMode.Impulse);
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
}