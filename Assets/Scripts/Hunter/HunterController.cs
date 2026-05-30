using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HunterController : MonoBehaviour
{
    public enum HunterState
    {
        Idle,
        Chasing,
        Stunned,          // Preparado para cuando agreguemos el choque entre cazadores
        CapturedSnitch    // Estado de victoria
    }

    [Header("State")]
    public HunterState currentState = HunterState.Idle;

    [Header("References")]
    [Tooltip("Cada cazador debe tener su propio InputManager asignado")]
    [SerializeField] private InputSystemManager inputManager;
    private Rigidbody rb;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 50f;
    [SerializeField] private float maxSpeed = 18f; // Ligeramente superior a la Snitch para poder alcanzarla
    [Tooltip("Qué tan rápido gira el cazador para mirar hacia su dirección")]
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Flight Boundaries")]
    [SerializeField] private float minHeight = 0.5f;
    [SerializeField] private float maxHeight = 4f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Confirmamos que vuelan sin gravedad
        rb.useGravity = false;

        inputManager = GetComponent<InputSystemManager>();

    }

    private void Update()
    {
        ManageStateMachine();
    }

    private void FixedUpdate()
    {
        // Solo nos movemos y aplicamos límites si estamos activamente cazando
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
                if (inputManager.GetMovement().sqrMagnitude > 0.01f || Mathf.Abs(inputManager.GetElevation()) > 0.01f)
                {
                    ChangeState(HunterState.Chasing);
                }
                break;

            case HunterState.Chasing:
                // Lógica delegada al FixedUpdate
                break;

            case HunterState.Stunned:
                // Si otro cazador lo choca, frenamos su velocidad suavemente con Lerp
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.deltaTime * 5f);
                break;

            case HunterState.CapturedSnitch:
                // Freno total al ganar
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

        // Limitamos la velocidad máxima
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }
    private void HandleRotation()
    {
        // Solo rotamos si el cazador se está moviendo a una velocidad mínima
        // Esto evita que vuelva a una rotación por defecto (0,0,0) cuando se frena
        if (rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            // Calculamos la rotación objetivo mirando hacia el vector de velocidad
            Quaternion targetRotation = Quaternion.LookRotation(rb.linearVelocity.normalized);

            // Suavizamos la rotación actual hacia la objetivo usando MoveRotation para respetar las físicas
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * rotationSpeed));
        }
    }
    private void ApplyHeightClamp()
    {
        Vector3 clampedPosition = rb.position;
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, minHeight, maxHeight);

        // Si la posición fue limitada, corregimos el Rigidbody y matamos la velocidad vertical
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


    private void HandleHunterCollision(Collision collision)
    {
        Debug.Log("¡Choque entre cazadores!");
        ChangeState(HunterState.Stunned);

        // Opcional: Aplicar una fuerza de rebote extra para separarlos
        Vector3 bounceDirection = (transform.position - collision.transform.position).normalized;
        rb.AddForce(bounceDirection * 10f, ForceMode.Impulse);

        // Iniciar el temporizador para salir del stun
        StartCoroutine(StunRecoveryRoutine());
    }

    private System.Collections.IEnumerator StunRecoveryRoutine()
    {
        // Queda paralizado por 1.5 segundos
        yield return new WaitForSeconds(1.5f);

        // Volver a la persecución si el juego sigue activo
        if (currentState == HunterState.Stunned)
        {
            ChangeState(HunterState.Chasing);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Solo interactuamos si estamos cazando activamente
        if (currentState != HunterState.Chasing) return;

        // --- 1. HUNTER VS SNITCH (Catch) ---
        if (collision.gameObject.CompareTag("Snitch"))
        {
            ChangeState(HunterState.CapturedSnitch);
            Debug.Log($"¡{gameObject.name} atrapó la Snitch!");

            // Nico's Hook: Avisar al Game Manager que hubo un tag
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerTagged();
            }

            // Cambiamos el estado de la Snitch a capturada
            SnitchController snitch = collision.gameObject.GetComponent<SnitchController>();
            if (snitch != null)
            {
                snitch.ChangeState(SnitchController.SnitchState.Captured);
            }
            return;
        }

        // --- 2. HUNTER VS HUNTER (Stun) ---
        if (collision.gameObject.CompareTag("Hunter"))
        {
            HandleHunterCollision(collision);
        }
    }
}