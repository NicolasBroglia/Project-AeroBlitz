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

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 40f;
    [SerializeField] private float maxSpeed = 15f;

    [Header("Dash Settings")]
    [SerializeField] private float dashForce = 25f;
    [SerializeField] private float dashCooldown = 1.5f;
    [Tooltip("How long the dash ignores the maxSpeed clamp (in seconds)")]
    [SerializeField] private float dashDuration = 0.2f;

    private float dashCooldownTimer = 0f;
    private float activeDashTimer = 0f;

    [Header("Flight Boundaries")]
    [SerializeField] private float maxHeight = 4f;
    [SerializeField] private float minHeight = 0.5f;

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

        // Allow actions in ActiveMatch AND Lobby
        bool canPlay = GameManager.Instance.CurrentState == GameState.ActiveMatch || GameManager.Instance.CurrentState == GameState.Lobby;

        if (currentState == SnitchState.Fleeing && canPlay)
        {
            if (inputManager.GetDash() && dashCooldownTimer <= 0f)
            {
                PerformDash();
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
            }
            else
            {
                // Drift to a stop during clashes and countdowns
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
        Vector2 planeInput = inputManager.GetMovement();
        float elevationInput = inputManager.GetElevation();
        Vector3 moveDirection = new Vector3(planeInput.x, elevationInput, planeInput.y).normalized;

        rb.AddForce(moveDirection * moveSpeed, ForceMode.Acceleration);

        // ONLY clamp the speed if we are NOT currently in the middle of a dash
        if (activeDashTimer <= 0f)
        {
            if (rb.linearVelocity.magnitude > maxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }
        }
    }

    private void ApplyHeightClamp()
    {
        Vector3 clampedPosition = rb.position;
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, minHeight, maxHeight);

        if (rb.position.y != clampedPosition.y)
        {
            rb.position = clampedPosition;
            // Kill vertical momentum so they don't bounce against the ceiling/floor
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
    }
    public void SetSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }
    public void SetMaxSpeed(float newMaxSpeed)
    {
        maxSpeed = newMaxSpeed;
    }
    public float GetSpeed()
    {
        return moveSpeed;
    }
    public float GetMaxSpeed()
    {
        return maxSpeed;
    }
    #endregion

    #region Dash Logic

    private void PerformDash()
    {
        dashCooldownTimer = dashCooldown;
        activeDashTimer = dashDuration;

        Vector2 planeInput = inputManager.GetMovement();
        float elevationInput = inputManager.GetElevation();
        Vector3 dashDir = new Vector3(planeInput.x, elevationInput, planeInput.y).normalized;

        // If no input, dash in the direction of current momentum
        if (dashDir == Vector3.zero)
        {
            dashDir = rb.linearVelocity.sqrMagnitude > 0.1f ? rb.linearVelocity.normalized : transform.forward;
        }

        // Wipe current velocity so the dash is explosive
        rb.linearVelocity = Vector3.zero;
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

        string label = $"Device: {inputManager.deviceID}\nState: {currentState}";

        // Draws the text 2.5 units above the player's head in the Scene View
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2.5f, label, style);
    }
#endif

    #endregion
}