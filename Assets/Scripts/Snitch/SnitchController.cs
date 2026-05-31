using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SnitchController : MonoBehaviour
{
    public enum SnitchState
    {
        Idle,
        Fleeing,
        Clashing,   // <--- NEW: Paralizado mientras ocurre el minijuego
        Captured
    }

    [Header("State")]
    public SnitchState currentState = SnitchState.Idle;

    [Header("References")]
    [SerializeField] private InputSystemManager inputManager;
    private Rigidbody rb;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 40f;
    [SerializeField] private float maxSpeed = 15f;
    [SerializeField] private float maxHeight = 4f;
    [SerializeField] private float minHeight = 0.5f;

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
    }

    private void FixedUpdate()
    {
        if (currentState == SnitchState.Fleeing)
        {
            HandleMovement();
        }
    }

    private void ManageStateMachine()
    {
        switch (currentState)
        {
            case SnitchState.Idle:
                // We do absolutely nothing here now.
                // We wait for the GameManager to explicitly change our state.
                break;

            case SnitchState.Fleeing:
                // La lógica de movimiento ocurre en FixedUpdate
                break;

            case SnitchState.Clashing: // <--- NUEVO
            case SnitchState.Captured:
                // Frenamos la bola en seco para el impacto y el minijuego
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

        Vector3 clampedPosition = transform.position;
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, minHeight, maxHeight);

        if (transform.position.y != clampedPosition.y)
        {
            rb.position = clampedPosition;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
    }

    public void ChangeState(SnitchState newState)
    {
        currentState = newState;
    }
}