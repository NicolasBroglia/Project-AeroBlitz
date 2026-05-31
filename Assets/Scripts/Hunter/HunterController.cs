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
    [Tooltip("Cada cazador debe tener su propio InputManager asignado")]
    [SerializeField] private InputSystemManager inputManager;
    private Rigidbody rb;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 50f;
    [SerializeField] private float maxSpeed = 18f;
    [SerializeField] private float rotationSpeed = 12f;

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

    private void HandleMovement()
    {
        Vector2 planeInput = inputManager.GetMovement();
        float elevationInput = inputManager.GetElevation();
        Vector3 moveDirection = new Vector3(planeInput.x, elevationInput, planeInput.y).normalized;

        rb.AddForce(moveDirection * moveSpeed, ForceMode.Acceleration);

        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
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
}