using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HunterController : MonoBehaviour
{
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

    [Header("References")]
    [SerializeField] private InputSystemManager inputManager;
    private Rigidbody rb;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 50f;
    [SerializeField] private float maxSpeed = 18f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Dash Settings")]
    [SerializeField] private float dashForce = 35f;
    [SerializeField] private float dashCooldown = 3f;
    [Tooltip("How long the dash ignores the maxSpeed clamp (in seconds)")]
    [SerializeField] private float dashDuration = 0.25f;

    private float dashCooldownTimer = 0f;
    private float activeDashTimer = 0f;

    [Header("Flight Boundaries")]
    [SerializeField] private float minHeight = 0.5f;
    [SerializeField] private float maxHeight = 4f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.useGravity = false;
        inputManager = GetComponent<InputSystemManager>();

        ChangeState(HunterState.Idle);
    }

    private void Update()
    {
        ManageStateMachine();

        // Handle Dash Timers
        if (dashCooldownTimer > 0) dashCooldownTimer -= Time.deltaTime;
        if (activeDashTimer > 0) activeDashTimer -= Time.deltaTime;

        // Check for Dash Input
        if (currentState == HunterState.Chasing)
        {
            if (inputManager.GetDash() && dashCooldownTimer <= 0f)
            {
                PerformDash();
            }
        }
    }

    private void FixedUpdate()
    {
        if (currentState == HunterState.Chasing)
        {
            HandleMovement();
            ApplyHeightClamp();
            HandleRotation();
        }
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

    private void PerformDash()
    {
        dashCooldownTimer = dashCooldown;
        activeDashTimer = dashDuration;

        Vector2 planeInput = inputManager.GetMovement();
        float elevationInput = inputManager.GetElevation();
        Vector3 dashDir = new Vector3(planeInput.x, elevationInput, planeInput.y).normalized;

        // If the player isn't touching the joystick, dash in the direction they are currently moving/facing
        if (dashDir == Vector3.zero)
        {
            dashDir = rb.linearVelocity.sqrMagnitude > 0.1f ? rb.linearVelocity.normalized : transform.forward;
        }

        // Wipe current velocity so the dash is explosive and doesn't compound awkwardly
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(dashDir * dashForce, ForceMode.Impulse);
    }

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

    private void HandleRotation()
    {
        if (rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(rb.linearVelocity.normalized);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed));
        }
        else
        {
            Quaternion flatRotation = Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);

            rb.MoveRotation(Quaternion.Slerp(rb.rotation, flatRotation, Time.fixedDeltaTime * rotationSpeed));
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

    public void ChangeState(HunterState newState)
    {
        currentState = newState;
    }

    public void ApplyKnockbackFrom(Vector3 sourcePosition)
    {
        ChangeState(HunterState.Stunned);
        Vector3 bounceDirection = (transform.position - sourcePosition).normalized;
        rb.AddForce(bounceDirection * 20f, ForceMode.Impulse);
        StartCoroutine(StunRecoveryRoutine());
    }

    private void HandleHunterCollision(Collision collision)
    {
        ChangeState(HunterState.Stunned);
        Vector3 bounceDirection = (transform.position - collision.transform.position).normalized;
        rb.AddForce(bounceDirection * 10f, ForceMode.Impulse);
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

}